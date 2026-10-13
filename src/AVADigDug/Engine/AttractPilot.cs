using AVADigDug.Models;

namespace AVADigDug.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It digs towards the nearest monster to get in
/// line with it, faces it and pumps it up once the tunnel between them is clear, keeps pumping a hooked
/// one until it pops, backs away from anything too close or a scorcher about to breathe, keeps out
/// from under loose rocks, and picks up the vegetable when it's safe.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private Dir _wander = Dir.Left;
    private double _wanderClock;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing) return default;
        var d = g.Digger;

        if (g.Harpoon is { Hooked: not null }) return new Controls(Dir.None, true);
        if (g.Harpoon is not null) return default; // let it fly

        // Danger first: a fire on this row, a loose rock overhead, a monster too close.
        foreach (var m in g.Monsters)
        {
            if (m.Dead || m.Kind != MonsterKind.Scorcher || m.Fire == FireState.None) continue;
            if (Math.Abs(m.Y - d.Y) < 10 && Math.Abs(m.X - d.X) < 70) return new Controls(Escape(g, vertical: true), false);
        }
        foreach (var r in g.Rocks)
            if (!r.Gone && r.State is RockState.Waiting or RockState.Wobbling or RockState.Falling && Math.Abs(r.X - d.X) < 16 && d.Y > r.Y && d.Y - r.Y < 70)
                return new Controls(Escape(g, vertical: false), false);

        var target = g.Monsters.Where(m => !m.Dead && m.Crushed is null && m.Inflate <= 0)
            .OrderBy(m => (m.Pos - d.Pos).Length).FirstOrDefault();

        foreach (var m in g.Monsters)
        {
            if (m.Harmless || (m.Pos - d.Pos).Length > 22) continue;
            // Close: shoot if it's in line and the way is clear, otherwise get away.
            if (!m.Ghost && InLine(g, m, out var face)) return Shoot(d, face);
            return new Controls(Escape(g, vertical: Math.Abs(m.X - d.X) > Math.Abs(m.Y - d.Y)), false);
        }

        if (g.VegetableLeft > 1 && target is not null && (target.Pos - Ground.Start).Length > 50)
            return new Controls(Toward(d, Ground.Start), false);

        if (target is null) return default;
        if (!target.Ghost && InLine(g, target, out var dir) && (target.Pos - d.Pos).Length < Harpoon.Reach + 10)
            return Shoot(d, dir);

        // Get in line: along the row or column, whichever is nearer.
        _wanderClock -= Field.Step;
        var aim = target.Pos;
        if (Math.Abs(aim.X - d.X) < Math.Abs(aim.Y - d.Y) && Math.Abs(aim.X - d.X) > 2)
            return new Controls(aim.X < d.X ? Dir.Left : Dir.Right, false);
        if (Math.Abs(aim.Y - d.Y) > 2) return new Controls(aim.Y < d.Y ? Dir.Up : Dir.Down, false);
        if (Math.Abs(aim.X - d.X) > 24) return new Controls(aim.X < d.X ? Dir.Left : Dir.Right, false);
        if (_wanderClock <= 0) { _wanderClock = 0.6; _wander = Dirs.All[_rng.Next(4)]; }
        return new Controls(_wander, false);
    }

    /// <summary>Face the way to shoot first (a tap turns the digger), then pump.</summary>
    private static Controls Shoot(Digger d, Dir face) =>
        d.Facing == face ? new Controls(Dir.None, true) : new Controls(face, false);

    private static Dir Toward(Digger d, Vec to) =>
        Math.Abs(to.X - d.X) > 2 ? (to.X < d.X ? Dir.Left : Dir.Right) : to.Y < d.Y ? Dir.Up : Dir.Down;

    /// <summary>In the same row or column, with open tunnel all the way between.</summary>
    private static bool InLine(GameEngine g, Monster m, out Dir dir)
    {
        var d = g.Digger;
        dir = Dir.None;
        if (Math.Abs(m.Y - d.Y) < 3) dir = m.X < d.X ? Dir.Left : Dir.Right;
        else if (Math.Abs(m.X - d.X) < 3) dir = m.Y < d.Y ? Dir.Up : Dir.Down;
        else return false;
        double dist = Math.Abs(m.X - d.X) + Math.Abs(m.Y - d.Y);
        for (double s = 8; s < dist - 6; s += 3)
            if (!g.Ground.OpenAt(d.X + dir.Dx() * s, d.Y + dir.Dy() * s)) return false;
        return true;
    }

    /// <summary>A way out: off the current row (vertical) or column, where there's room or dirt to dig.</summary>
    private static Dir Escape(GameEngine g, bool vertical)
    {
        var d = g.Digger;
        Dir[] ways = vertical ? [Dir.Up, Dir.Down] : [Dir.Left, Dir.Right];
        return ways.OrderByDescending(w =>
        {
            var to = new Vec(d.X + w.Dx() * 16, d.Y + w.Dy() * 16);
            if (to.X < 8 || to.X > Field.Width - 8 || to.Y < Field.Surface || to.Y > Field.Bottom) return -1000.0;
            return g.Monsters.Where(m => !m.Harmless).Select(m => (m.Pos - to).Length).DefaultIfEmpty(200).Min();
        }).First();
    }
}
