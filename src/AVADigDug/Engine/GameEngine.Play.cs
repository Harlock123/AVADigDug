using AVADigDug.Models;

namespace AVADigDug.Engine;

public sealed partial class GameEngine
{
    public const double DiggerSpeed = 44;
    public const double GhostSpeed = 20;
    public const double HarpoonSpeed = 220;
    public const double PumpRepeat = 0.22;
    public const double DeflateEvery = 0.6;
    public const double RockWobble = 0.6;
    public const double RockFallSpeed = 110;
    public const double ReadySeconds = 1.4;
    public const double FirstReadySeconds = 2.4;
    public const double DyingSeconds = 2.4;
    public const double RoundClearSeconds = 2.5;
    public const double VegetableSeconds = 10;
    public const double FireCharge = 0.6;
    public const double FireBreath = 0.8;

    public Ground Ground { get; private set; }
    public Digger Digger { get; } = new();
    public Harpoon? Harpoon { get; private set; }
    public List<Monster> Monsters { get; } = [];
    public List<Rock> Rocks { get; } = [];
    /// <summary>Seconds the bonus vegetable has left (0: none showing).</summary>
    public double VegetableLeft { get; private set; }
    public int RocksDropped { get; private set; }
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    private double _readyFor = ReadySeconds;
    private bool _noEscape;
    private bool _pumpPrev;
    private double _pumpRepeat;

    public double MonsterSpeed => Math.Min(52, 30 + 2 * (Round - 1));
    /// <summary>A monster in its tunnels turns ghost after about this long.</summary>
    private double GhostAfter => Math.Max(3, 9 - 0.4 * Round) + _rng.NextDouble() * 4;

    // ----- rounds -------------------------------------------------------------------------------

    private void StartRound(int round)
    {
        Round = round;
        Ground = Ground.Build(_seed, round);
        SetUpRound();
        SetPhase(Phase.Ready);
        _readyFor = round == 1 ? FirstReadySeconds : ReadySeconds;
    }

    private void SetUpRound()
    {
        Monsters.Clear();
        foreach (var s in Ground.Spawns)
            Monsters.Add(new Monster { Kind = s.Kind, X = s.X, Y = s.Y, Home = new Vec(s.X, s.Y), GhostClock = -_rng.NextDouble() * 3 });
        Rocks.Clear();
        foreach (var r in Ground.Rocks) Rocks.Add(new Rock { X = r.X, Y = r.Y });
        VegetableLeft = 0;
        RocksDropped = 0;
        PlaceDigger();
    }

    private void PlaceDigger()
    {
        Digger.X = Ground.Start.X;
        Digger.Y = Ground.Start.Y;
        Digger.Facing = Dir.Right;
        Digger.Moving = false;
        Harpoon = null;
    }

    /// <summary>After losing a life: the digger back in the middle, the monsters back home; the tunnels and rocks stay.</summary>
    private void Regroup()
    {
        PlaceDigger();
        foreach (var m in Monsters.Where(m => !m.Dead))
        {
            m.X = m.Home.X;
            m.Y = m.Home.Y;
            m.Ghost = false;
            m.GhostClock = -_rng.NextDouble() * 3;
            m.Inflate = 0;
            m.Fire = FireState.None;
            m.FireCooldown = 2;
            m.Escaping = false;
        }
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Ready:
                if (PhaseTime >= _readyFor) { SetPhase(Phase.Playing); Music(true); }
                return;
            case Phase.Dying:
                Music(false);
                if (PhaseTime < DyingSeconds) return;
                Lives--;
                if (Lives <= 0) { GameOver(); return; }
                Regroup();
                SetPhase(Phase.Ready);
                _readyFor = ReadySeconds;
                return;
            case Phase.RoundClear:
                Music(false);
                if (PhaseTime >= RoundClearSeconds) StartRound(Round + 1);
                return;
        }

        MoveDigger(dt, c);
        Pump(dt, c);
        MoveMonsters(dt);
        UpdateRocks(dt);
        if (Phase != Phase.Playing) return;
        Vegetable(dt);
        Collide();
        if (Phase != Phase.Playing) return;

        var alive = Monsters.Where(m => !m.Dead).ToList();
        if (alive.Count == 0)
        {
            SetPhase(Phase.RoundClear);
            Play(Sfx.RoundClear);
            return;
        }
        if (alive.Count == 1 && !_noEscape && !alive[0].Escaping && alive[0].Inflate <= 0 && alive[0].Crushed is null)
        {
            alive[0].Escaping = true;
            alive[0].Ghost = true;
            Play(Sfx.LastOne);
        }
    }

    private void Die(string why)
    {
        LastDeath = why;
        LossLog.Add($"R{Round}: {why} at {Digger.Pos}");
        SetPhase(Phase.Dying);
        Harpoon = null;
        Play(Sfx.Death);
        Music(false);
    }

    // ----- the digger ---------------------------------------------------------------------------

    private bool RockIn(double x, double y, Rock? except = null) =>
        Rocks.Any(r => r != except && !r.Gone && r.State != RockState.Breaking && Math.Abs(r.X - x) < 15 && Math.Abs(r.Y - y) < 15);

    private void MoveDigger(double dt, Controls c)
    {
        var d = Digger;
        var want = c.Move;
        d.Moving = false;
        if (want == Dir.None) { MusicHeard(false); return; }
        if (Harpoon is not null) Harpoon = null; // moving lets go of the pump
        double step = DiggerSpeed * dt;
        bool across = want.Horizontal();
        double nx = d.X, ny = d.Y;
        if (across ? Field.OnLine(d.Y) : Field.OnLine(d.X))
        {
            d.Facing = want;
            nx = Math.Clamp(d.X + want.Dx() * step, Field.Tile / 2.0, Field.Width - Field.Tile / 2.0);
            ny = Math.Clamp(d.Y + want.Dy() * step, Field.Surface, Field.Bottom);
        }
        else
        {
            // Not on a line to turn yet: carry on the way it was going until it is.
            if (across)
            {
                double target = d.Facing == Dir.Down ? Math.Ceiling((d.Y - 8) / 16) * 16 + 8 : d.Facing == Dir.Up ? Math.Floor((d.Y - 8) / 16) * 16 + 8 : Field.Snap(d.Y);
                ny = d.Y + Math.Clamp(target - d.Y, -step, step);
            }
            else
            {
                double target = d.Facing == Dir.Right ? Math.Ceiling((d.X - 8) / 16) * 16 + 8 : d.Facing == Dir.Left ? Math.Floor((d.X - 8) / 16) * 16 + 8 : Field.Snap(d.X);
                nx = d.X + Math.Clamp(target - d.X, -step, step);
            }
        }
        if (RockIn(nx, ny)) { MusicHeard(false); return; }
        if (nx == d.X && ny == d.Y) { MusicHeard(false); return; }
        d.Stride += Math.Abs(nx - d.X) + Math.Abs(ny - d.Y);
        d.X = nx;
        d.Y = ny;
        d.Moving = true;
        Ground.Dig(d.X, d.Y);
        MusicHeard(true);
    }

    // ----- the pump -----------------------------------------------------------------------------

    public Vec HarpoonTip => Harpoon is { } h
        ? Digger.Pos + new Vec(h.Dir.Dx(), h.Dir.Dy()) * (8 + h.Length)
        : Digger.Pos;

    private void Pump(double dt, Controls c)
    {
        bool pressed = c.Pump && !_pumpPrev;
        _pumpPrev = c.Pump;
        _pumpRepeat -= dt;
        if (c.Move != Dir.None) return;

        if (Harpoon is { Hooked: { } m })
        {
            if (m.Dead) { Harpoon = null; return; }
            Harpoon.Length = Math.Max(0, Math.Abs(m.X - Digger.X) + Math.Abs(m.Y - Digger.Y) - 14);
            if (pressed || (c.Pump && _pumpRepeat <= 0))
            {
                _pumpRepeat = PumpRepeat;
                Inflate(m);
            }
            return;
        }
        if (Harpoon is null)
        {
            if (!pressed) return;
            Harpoon = new Harpoon { Dir = Digger.Facing };
            _pumpRepeat = PumpRepeat;
            Play(Sfx.Shoot);
            return;
        }
        // Flying out: along the tunnel until it hooks something, hits dirt or runs out of line.
        Harpoon.Length += HarpoonSpeed * dt;
        var tip = HarpoonTip;
        if (Harpoon.Length >= Harpoon.Reach || !Ground.OpenAt(tip.X, tip.Y)) { Harpoon = null; return; }
        foreach (var mon in Monsters)
        {
            if (mon.Dead || mon.Ghost || mon.Crushed is not null || Math.Abs(mon.X - tip.X) > 8 || Math.Abs(mon.Y - tip.Y) > 8) continue;
            Harpoon.Hooked = mon;
            mon.Fire = FireState.None;
            Inflate(mon);
            return;
        }
    }

    private void Inflate(Monster m)
    {
        m.Inflate = Math.Floor(m.Inflate) + 1;
        m.SincePump = 0;
        Play(Sfx.Pump);
        if (m.Inflate < 4) return;
        // Pop: worth more the deeper it was, and double for a scorcher pumped side-on.
        int points = Field.Layer(m.Y) switch { 1 => 200, 2 => 300, 3 => 400, _ => 500 };
        if (m.Kind == MonsterKind.Scorcher && Harpoon?.Dir.Horizontal() == true) points *= 2;
        m.Dead = true;
        Harpoon = null;
        AddScore(points);
        Effects.Add(new Effect { Kind = EffectKind.Pop, Pos = m.Pos, Life = 0.4, Monster = m.Kind });
        Popup(m.Pos with { Y = m.Y - 6 }, points);
        Play(Sfx.Pop);
    }

    // ----- the monsters -------------------------------------------------------------------------

    private void MoveMonsters(double dt)
    {
        var d = Digger;
        foreach (var m in Monsters)
        {
            if (m.Dead) continue;
            if (m.Crushed is { } rock) { m.X = rock.X; m.Y = rock.Y + 8; continue; }
            if (m.Inflate > 0)
            {
                m.SincePump += dt;
                if (m.SincePump >= DeflateEvery)
                {
                    m.SincePump = 0;
                    m.Inflate = Math.Max(0, Math.Floor(m.Inflate) - 1);
                    if (m.Inflate <= 0 && Harpoon?.Hooked == m) Harpoon = null;
                }
                continue;
            }

            if (m.Kind == MonsterKind.Scorcher && Breathe(m, dt)) continue;

            double step = MonsterSpeed * (m.Escaping ? 1.5 : 1) * dt;
            if (m.Escaping)
            {
                // Up to the surface, then off to the left.
                if (m.Y > Field.Surface + 0.5) MoveStraight(m, new Vec(m.X, Field.Surface), step);
                else { m.Y = Field.Surface; m.Ghost = false; m.X -= step; m.Dir = Dir.Left; }
                if (m.X < -10) m.Dead = true;
                continue;
            }
            if (m.Ghost)
            {
                MoveStraight(m, d.Pos, GhostSpeed * (1 + 0.04 * Round) * dt);
                m.GhostClock += dt;
                var at = new Vec(Field.Snap(m.X), Field.Snap(m.Y));
                if (m.GhostClock > 1.2 && (at - m.Pos).Length < 2.5 && Ground.Fits(at.X, at.Y) && !RockIn(at.X, at.Y))
                {
                    m.X = at.X;
                    m.Y = at.Y;
                    m.Ghost = false;
                    m.GhostClock = 0;
                    m.Dir = Dir.None;
                }
                continue;
            }
            m.GhostClock += dt;
            if (m.GhostClock > GhostAfter + 4 || (m.GhostClock > 2 && Options(m).Count == 0))
            {
                m.Ghost = true;
                m.GhostClock = 0;
                continue;
            }
            Walk(m, step);
        }
    }

    private static void MoveStraight(Monster m, Vec to, double step)
    {
        var v = to - m.Pos;
        double len = v.Length;
        if (len < 1e-6) return;
        var move = v * (Math.Min(step, len) / len);
        m.X += move.X;
        m.Y += move.Y;
        m.Dir = Math.Abs(v.X) > Math.Abs(v.Y) ? (v.X < 0 ? Dir.Left : Dir.Right) : (v.Y < 0 ? Dir.Up : Dir.Down);
    }

    private List<Dir> Options(Monster m) =>
        Dirs.All.Where(dir => Ground.Fits(m.X + dir.Dx() * 16, m.Y + dir.Dy() * 16) && !RockIn(m.X + dir.Dx() * 16, m.Y + dir.Dy() * 16)).ToList();

    /// <summary>Along the tunnels from tile to tile, turning at each towards the digger (mostly).</summary>
    private void Walk(Monster m, double step)
    {
        for (int guard = 0; step > 1e-9 && guard < 4; guard++)
        {
            if (Field.OnLine(m.X) && Field.OnLine(m.Y))
            {
                var options = Options(m);
                if (options.Count > 1) options.Remove(m.Dir.Reverse());
                if (options.Count == 0) { m.Dir = Dir.None; return; }
                if (_rng.NextDouble() < 0.75)
                {
                    var d = Digger.Pos;
                    m.Dir = options.OrderBy(o => (new Vec(m.X + o.Dx() * 16, m.Y + o.Dy() * 16) - d).Length).First();
                }
                else if (!options.Contains(m.Dir)) m.Dir = options[_rng.Next(options.Count)];
            }
            if (m.Dir == Dir.None) return;
            double along = m.Dir.Horizontal() ? m.X : m.Y;
            double next = m.Dir.Dx() + m.Dir.Dy() > 0 ? Math.Floor((along - 8) / 16 + 1e-9) * 16 + 24 : Math.Ceiling((along - 8) / 16 - 1e-9) * 16 - 8;
            double gap = Math.Abs(next - along);
            if (gap < 1e-9) gap = 16;
            double s = Math.Min(step, gap);
            m.X += m.Dir.Dx() * s;
            m.Y += m.Dir.Dy() * s;
            if (Math.Abs(s - gap) < 1e-9) { m.X = Field.Snap(m.X); m.Y = Field.Snap(m.Y); }
            step -= s;
        }
    }

    /// <summary>A scorcher in line with the digger stops, glows, and breathes fire. True while it's busy at it.</summary>
    private bool Breathe(Monster m, double dt)
    {
        switch (m.Fire)
        {
            case FireState.Charging:
                m.FireClock += dt;
                if (m.FireClock >= FireCharge) { m.Fire = FireState.Breathing; m.FireClock = 0; Play(Sfx.Fire); }
                return true;
            case FireState.Breathing:
                m.FireClock += dt;
                if (m.FireClock >= FireBreath) { m.Fire = FireState.None; m.FireCooldown = 2.5 + _rng.NextDouble() * 2; }
                return true;
        }
        m.FireCooldown -= dt;
        var d = Digger;
        if (m.Ghost || m.Escaping || m.FireCooldown > 0 || Math.Abs(m.Y - d.Y) > 3 || Math.Abs(m.X - d.X) > 64) return false;
        if (!Field.OnLine(m.X) || _rng.NextDouble() > 0.03) return false;
        m.Dir = d.X < m.X ? Dir.Left : Dir.Right;
        m.Fire = FireState.Charging;
        m.FireClock = 0;
        return true;
    }

    // ----- rocks and the vegetable --------------------------------------------------------------

    private void UpdateRocks(double dt)
    {
        var d = Digger;
        foreach (var r in Rocks)
        {
            if (r.Gone) continue;
            switch (r.State)
            {
                case RockState.Set:
                    if (!Ground.Fits(r.X, r.Y + 16)) break;
                    // Not while the digger is right underneath it: it waits for them to move.
                    r.State = UnderRock(r) ? RockState.Waiting : RockState.Wobbling;
                    r.Clock = 0;
                    break;
                case RockState.Waiting:
                    if (!UnderRock(r)) { r.State = RockState.Wobbling; r.Clock = 0; }
                    break;
                case RockState.Wobbling:
                    r.Clock += dt;
                    if (r.Clock >= RockWobble) { r.State = RockState.Falling; Play(Sfx.RockFall); }
                    break;
                case RockState.Falling:
                    r.Y += RockFallSpeed * dt;
                    foreach (var m in Monsters)
                        if (!m.Dead && m.Crushed is null && Math.Abs(m.X - r.X) < 12 && m.Y > r.Y - 4 && m.Y - r.Y < 14)
                        {
                            m.Crushed = r;
                            r.Caught.Add(m);
                            if (Harpoon?.Hooked == m) Harpoon = null;
                        }
                    if (Math.Abs(d.X - r.X) < 11 && d.Y > r.Y && d.Y - r.Y < 15) { Die("was crushed by a rock"); return; }
                    if (r.Y >= Field.Bottom || !Ground.OpenAt(r.X, r.Y + 9))
                    {
                        r.Y = Math.Min(r.Y, Field.Bottom);
                        r.State = RockState.Breaking;
                        r.Clock = 0;
                        Play(Sfx.RockLand);
                        Landed(r);
                    }
                    break;
                case RockState.Breaking:
                    r.Clock += dt;
                    if (r.Clock >= 0.5) r.Gone = true;
                    break;
            }
        }
    }

    private bool UnderRock(Rock r) => Math.Abs(Digger.X - r.X) < 12 && Digger.Y > r.Y && Digger.Y - r.Y < 24;

    /// <summary>Points for the monsters a rock flattened: more for each extra one.</summary>
    public static int CrushPoints(int n) => n switch
    {
        0 => 0, 1 => 1000, 2 => 2500, 3 => 4000, 4 => 6000, 5 => 8000, 6 => 10000, 7 => 12000, _ => 15000,
    };

    private void Landed(Rock r)
    {
        foreach (var m in r.Caught) m.Dead = true;
        int points = CrushPoints(r.Caught.Count);
        if (points > 0)
        {
            AddScore(points);
            Popup(new Vec(r.X, r.Y - 10), points, 1.6);
        }
        RocksDropped++;
        if (RocksDropped == 2)
        {
            VegetableLeft = VegetableSeconds;
            Play(Sfx.VegetableShows);
        }
    }

    private void Vegetable(double dt)
    {
        if (VegetableLeft <= 0) return;
        VegetableLeft -= dt;
        if ((Digger.Pos - Ground.Start).Length > 8) return;
        var (_, points) = Ground.Vegetable(Round);
        VegetableLeft = 0;
        AddScore(points);
        Popup(Ground.Start, points, 1.6);
        Play(Sfx.Vegetable);
    }

    // ----- what ends the digger -----------------------------------------------------------------

    private void Collide()
    {
        var d = Digger;
        foreach (var m in Monsters)
        {
            if (m.Harmless) continue;
            if (Math.Abs(m.X - d.X) < 11 && Math.Abs(m.Y - d.Y) < 11) { Die($"was caught by a {m.Kind.ToString().ToLowerInvariant()}"); return; }
            if (m.FireLength > 0)
            {
                int dir = m.Dir == Dir.Left ? -1 : 1;
                double near = m.X + dir * 8, far = m.X + dir * (8 + m.FireLength);
                if (Math.Abs(d.Y - m.Y) < 9 && d.X + 6 > Math.Min(near, far) && d.X - 6 < Math.Max(near, far)) { Die("was burned"); return; }
            }
        }
    }

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    /// <summary>Skips the "ready" pause.</summary>
    internal void Go()
    {
        if (Phase != Phase.Ready) return;
        SetPhase(Phase.Playing);
        Music(true);
    }

    internal void JumpTo(int round)
    {
        StartRound(round);
        Go();
    }

    /// <summary>Takes every monster away but the ones given (and keeps a lone one from running off).</summary>
    internal void Keep(params Monster[] keep)
    {
        Monsters.RemoveAll(m => !keep.Contains(m));
        _noEscape = true;
    }

    internal Monster AddMonster(MonsterKind kind, double x, double y)
    {
        Ground.Dig(x, y);
        var m = new Monster { Kind = kind, X = x, Y = y, Home = new Vec(x, y), GhostClock = -1000 };
        Monsters.Add(m);
        return m;
    }

    internal void PutDigger(double x, double y, Dir facing)
    {
        Digger.X = x;
        Digger.Y = y;
        Digger.Facing = facing;
        Ground.Dig(x, y);
    }

    internal void DigLine(double x0, double y0, double x1, double y1)
    {
        int n = (int)Math.Ceiling(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) / 4) + 1;
        for (int i = 0; i <= n; i++) Ground.Dig(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n);
    }

    internal Rock AddRock(double x, double y)
    {
        var r = new Rock { X = x, Y = y };
        Rocks.Add(r);
        return r;
    }
}
