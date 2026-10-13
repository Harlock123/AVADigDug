using AVADigDug.Engine;
using AVADigDug.Models;

namespace AVADigDug.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dig-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>
    /// A round under way with the monsters taken away (add your own), but for one parked in a sealed
    /// pocket in the bottom corner so the round isn't over.
    /// </summary>
    private GameEngine Empty(int round = 1, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(round);
        g.Keep();
        g.Rocks.Clear();
        g.AddMonster(MonsterKind.Puffer, 216, 248).GhostClock = double.MinValue;
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Step(c);
    }

    private static readonly Controls PumpIt = new(Dir.None, true);

    [Fact]
    public void Every_round_has_monsters_in_their_own_tunnels_and_rocks_in_solid_dirt()
    {
        for (int round = 1; round <= 12; round++)
        {
            var ground = Ground.Build(7, round);
            Assert.Equal(Math.Min(8, 4 + (round - 1) / 2), ground.Spawns.Count);
            Assert.Contains(ground.Spawns, s => s.Kind == MonsterKind.Scorcher);
            Assert.All(ground.Spawns, s => Assert.True(ground.Fits(s.X, s.Y)));
            Assert.All(ground.Rocks, r => Assert.False(ground.OpenAt(r.X, r.Y + 16), "dirt under the rock"));
            Assert.True(ground.Fits(Ground.Start.X, Ground.Start.Y));
            Assert.True(ground.Fits(Ground.Start.X, Field.CentreY(Field.FirstDirtRow)), "the shaft reaches the surface");
        }
        Assert.Equal(Ground.Build(3, 2).Rocks, Ground.Build(3, 2).Rocks);
        Assert.Equal(1, Field.Layer(Field.CentreY(2)));
        Assert.Equal(4, Field.Layer(Field.CentreY(15)));
    }

    [Fact]
    public void The_digger_digs_its_own_tunnel_and_turns_only_on_the_lines()
    {
        var g = Empty();
        int before = g.Ground.DugCount();
        var d = g.Digger;
        Steps(g, 60, new Controls(Dir.Right, false));
        Assert.True(d.X > Ground.Start.X + 30);
        Assert.Equal(Ground.Start.Y, d.Y);
        Assert.True(g.Ground.DugCount() > before);
        Assert.True(g.Ground.Fits(d.X - 8, d.Y));
        // Asked to turn between lines, it carries on to the next line first.
        double x = d.X;
        bool between = !Field.OnLine(x);
        g.Step(new Controls(Dir.Down, false));
        if (between) Assert.Equal(Ground.Start.Y, d.Y);
        Steps(g, 40, new Controls(Dir.Down, false));
        Assert.True(Field.OnLine(d.X));
        Assert.True(d.Y > Ground.Start.Y + 8);
        Assert.Equal(Dir.Down, d.Facing);
        // Not above the surface.
        Steps(g, 60 * 6, new Controls(Dir.Up, false));
        Assert.Equal(Field.Surface, d.Y);
    }

    [Fact]
    public void The_harpoon_hooks_a_monster_and_four_pumps_pop_it_for_points_by_depth()
    {
        var g = Empty();
        g.DigLine(40, 168, 120, 168);
        g.PutDigger(40, 168, Dir.Right);
        var m = g.AddMonster(MonsterKind.Puffer, 72, 168);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Step(PumpIt);
        Steps(g, 10);
        Assert.Same(m, g.Harpoon?.Hooked);
        Assert.Equal(1, m.Inflate);
        for (int i = 0; i < 3; i++) { g.Step(PumpIt); g.Step(default); }
        Assert.True(m.Dead);
        Assert.Equal(400, g.Score); // row 10: the third layer down
        Assert.Contains(Sfx.Pop, heard);
        Assert.Contains(Sfx.Shoot, heard);
    }

    [Fact]
    public void Holding_the_pump_keeps_pumping_and_letting_go_lets_it_shrink()
    {
        var g = Empty();
        g.DigLine(40, 168, 120, 168);
        g.PutDigger(40, 168, Dir.Right);
        var m = g.AddMonster(MonsterKind.Puffer, 72, 168);
        g.Step(PumpIt);
        Steps(g, 10, PumpIt);
        Assert.InRange(m.Inflate, 1, 3);
        Steps(g, 60, PumpIt);
        Assert.True(m.Dead);

        g = Empty();
        g.DigLine(40, 168, 120, 168);
        g.PutDigger(40, 168, Dir.Right);
        m = g.AddMonster(MonsterKind.Puffer, 72, 168);
        g.Step(PumpIt);
        Steps(g, 10);
        g.Step(PumpIt);
        Assert.Equal(2, m.Inflate);
        Steps(g, (int)(60 * GameEngine.DeflateEvery * 2) + 4);
        Assert.Equal(0, m.Inflate);
        Assert.Null(g.Harpoon);
    }

    [Fact]
    public void The_harpoon_stops_at_dirt_and_cant_hook_a_ghost()
    {
        var g = Empty();
        g.PutDigger(40, 168, Dir.Right);
        var m = g.AddMonster(MonsterKind.Puffer, 72, 168);
        g.Step(PumpIt);
        Steps(g, 15);
        Assert.Null(g.Harpoon);
        Assert.Equal(0, m.Inflate);

        g = Empty();
        g.DigLine(40, 168, 120, 168);
        g.PutDigger(40, 168, Dir.Right);
        m = g.AddMonster(MonsterKind.Puffer, 72, 168);
        m.Ghost = true;
        m.GhostClock = -100;
        g.Step(PumpIt);
        Steps(g, 3);
        Assert.Null(g.Harpoon?.Hooked);
    }

    [Fact]
    public void A_scorcher_popped_side_on_is_worth_double()
    {
        var g = Empty();
        g.DigLine(40, 120, 120, 120);
        g.PutDigger(40, 120, Dir.Right);
        var m = g.AddMonster(MonsterKind.Scorcher, 72, 120);
        g.Step(PumpIt);
        Steps(g, 10);
        for (int i = 0; i < 3; i++) { g.Step(PumpIt); g.Step(default); }
        Assert.True(m.Dead);
        Assert.Equal(2 * 300, g.Score);
        Assert.Equal(1000, GameEngine.CrushPoints(1));
        Assert.Equal(2500, GameEngine.CrushPoints(2));
        Assert.Equal(15000, GameEngine.CrushPoints(9));
    }

    [Fact]
    public void A_monster_touching_the_digger_or_a_scorchers_fire_ends_it()
    {
        var g = Empty();
        g.DigLine(40, 168, 120, 168);
        g.PutDigger(40, 168, Dir.Right);
        g.AddMonster(MonsterKind.Puffer, 72, 168);
        for (int i = 0; i < 60 * 5 && g.Phase == Phase.Playing; i++) g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("puffer", g.LastDeath);
        Steps(g, (int)(60 * (GameEngine.DyingSeconds + GameEngine.ReadySeconds)) + 4);
        Assert.Equal(2, g.Lives);
        Assert.Equal(Ground.Start.X, g.Digger.X);

        g = Empty();
        g.PutDigger(40, 168, Dir.Right);
        var s = g.AddMonster(MonsterKind.Scorcher, 80, 168);
        s.Dir = Dir.Left;
        s.Fire = FireState.Breathing;
        s.FireClock = 0.4;
        g.Step(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("burned", g.LastDeath);
    }

    [Fact]
    public void Digging_under_a_rock_lets_it_fall_on_the_monsters_below()
    {
        var g = Empty();
        var rock = g.AddRock(152, 72);
        g.DigLine(152, 88, 152, 200);
        var m = g.AddMonster(MonsterKind.Puffer, 152, 184);
        m.GhostClock = -1000;
        var other = g.Monsters[0];
        g.PutDigger(40, 120, Dir.Right);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Steps(g, 2);
        Assert.Equal(RockState.Wobbling, rock.State);
        for (int i = 0; i < 60 * 3 && rock.State != RockState.Breaking; i++) { m.X = 152; m.Y = 184; g.Step(default); }
        Assert.Contains(Sfx.RockFall, heard);
        Assert.True(m.Dead);
        Assert.False(other.Dead);
        Assert.Equal(1000, g.Score);
        Assert.Equal(1, g.RocksDropped);
    }

    [Fact]
    public void A_rock_waits_while_the_digger_is_underneath_then_falls_on_it()
    {
        var g = Empty();
        var rock = g.AddRock(152, 72);
        g.DigLine(152, 88, 152, 104);
        g.PutDigger(152, 88, Dir.Up);
        Steps(g, 30);
        Assert.Equal(RockState.Waiting, rock.State);
        // Go down the shaft below it: the rock follows.
        g.DigLine(152, 88, 152, 200);
        for (int i = 0; i < 60 * 4 && g.Phase == Phase.Playing; i++) g.Step(new Controls(Dir.Down, false));
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("rock", g.LastDeath);
    }

    [Fact]
    public void Two_rocks_bring_out_the_vegetable()
    {
        var g = Empty(round: 2);
        foreach (int x in new[] { 40, 200 })
        {
            g.AddRock(x, 72);
            g.DigLine(x, 88, x, 120);
        }
        g.PutDigger(120, 200, Dir.Left);
        Steps(g, 60 * 3);
        Assert.Equal(2, g.RocksDropped);
        Assert.True(g.VegetableLeft > 5);
        g.PutDigger(Ground.Start.X, Ground.Start.Y, Dir.Left);
        Steps(g, 1);
        Assert.Equal(Ground.Vegetable(2).Points, g.Score);
        Assert.Equal(0, g.VegetableLeft);
    }

    [Fact]
    public void Monsters_follow_the_tunnels_and_turn_to_ghosts_to_cross_the_dirt()
    {
        var g = Empty();
        g.DigLine(40, 200, 184, 200);
        var m = g.AddMonster(MonsterKind.Puffer, 168, 200);
        m.GhostClock = 0;
        g.PutDigger(40, 120, Dir.Right);
        bool ghosted = false, inDirt = false;
        for (int i = 0; i < 60 * 20 && g.Phase == Phase.Playing; i++)
        {
            g.Step(default);
            if (!m.Ghost && !m.Dead) Assert.True(g.Ground.Fits(Field.Snap(m.X), Field.Snap(m.Y)) || !Field.OnLine(m.X) || !Field.OnLine(m.Y));
            if (m.Ghost) { ghosted = true; if (!g.Ground.OpenAt(m.X, m.Y)) inDirt = true; }
        }
        Assert.True(ghosted && inDirt, "it went through the dirt as a ghost");
        Assert.Equal(Phase.Dying, g.Phase);
    }

    [Fact]
    public void The_last_monster_runs_for_it_and_then_the_round_is_over()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.StartGame();
        g.Go();
        g.Rocks.Clear();
        g.PutDigger(Ground.Start.X, Field.Surface, Dir.Right);
        foreach (var m in g.Monsters.Skip(1)) m.Dead = true;
        g.Step(default);
        var last = g.Monsters[0];
        Assert.True(last.Escaping);
        Assert.Contains(Sfx.LastOne, heard);
        for (int i = 0; i < 60 * 30 && g.Phase == Phase.Playing; i++)
        {
            g.Digger.X = 200; g.Digger.Y = 248; // well out of the way
            g.Step(default);
        }
        Assert.True(last.Dead);
        Assert.Equal(Phase.RoundClear, g.Phase);
        Steps(g, (int)(60 * GameEngine.RoundClearSeconds) + 2);
        Assert.Equal(2, g.Round);
        Assert.Equal(Phase.Ready, g.Phase);
    }

    [Fact]
    public void Bonus_diggers_at_ten_thousand_and_every_thirty_thousand_after()
    {
        var g = Empty();
        g.AddScore(9999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
        g.AddScore(29999);
        Assert.Equal(4, g.Lives);
        g.AddScore(1);
        Assert.Equal(5, g.Lives);
    }

    [Fact]
    public void The_music_is_heard_only_while_the_digger_moves()
    {
        var g = Empty();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Steps(g, 10, new Controls(Dir.Right, false));
        Assert.Equal(Sfx.MusicUp, heard.Last(s => s is Sfx.MusicUp or Sfx.MusicDown));
        Steps(g, 2);
        Assert.Equal(Sfx.MusicDown, heard.Last(s => s is Sfx.MusicUp or Sfx.MusicDown));
        g.Pause();
        Assert.Equal(Sfx.MusicStop, heard[^1]);
        g.Resume();
        Assert.Equal(Sfx.MusicDown, heard[^1]);
    }

    [Fact]
    public void The_demonstration_pilot_clears_rounds_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing && g.Round < 3; i++) g.Step(pilot.Decide(g));
        Assert.True(g.Round >= 3, $"round {g.Round}; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Round} {g.Score} {g.Lives} {g.Digger.Pos} {g.Ground.DugCount()} {g.Monsters.Count(m => !m.Dead)}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 10)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Music, heard);
        Assert.Contains(Sfx.MusicUp, heard);
        Assert.Contains(Sfx.Shoot, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 8)));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.MusicStop, heard.Last(s => s is Sfx.Music or Sfx.MusicStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s is not (Sfx.MusicStop or Sfx.MusicUp or Sfx.MusicDown)).Select(s => s.ToString());
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        Assert.Equal((16, 16), (Sprites.DiggerWalk[0].Width, Sprites.DiggerWalk[0].Height));
        foreach (var s in new[] { Sprites.DiggerWalk[0], Sprites.PufferWalk[0], Sprites.ScorcherWalk[0], Sprites.Ghost, Sprites.Rock, Sprites.Pop })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
        for (int round = 1; round <= 16; round++)
            Assert.Equal(16, Sprites.VegetableArt(Ground.Vegetable(round).Name).Width);
    }
}
