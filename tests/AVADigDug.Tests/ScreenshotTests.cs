using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVADigDug.Engine;
using AVADigDug.Models;
using AVADigDug.Views;
using Controls = AVADigDug.Engine.Controls;

namespace AVADigDug.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set DIGDUG_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dig-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("DIGDUG_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVADigDug-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_a_round_under_way()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 40 && g.State == GameState.Playing; i++)
        {
            g.Step(pilot.Decide(g));
            if (i > 60 * 6 && g.Harpoon?.Hooked is { Inflate: >= 2 } && g.Monsters.Count(m => !m.Dead) >= 3) break;
        }
        Assert.NotNull(g.Harpoon?.Hooked);
        Shot(w, c, "digging");
        w.Close();
    }

    [AvaloniaFact]
    public void Pumping_fire_and_a_falling_rock()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.Go();
        var keep = g.Monsters.Take(0).ToArray();
        g.Keep(keep);
        // A puffer at the end of a tunnel, being pumped up.
        g.DigLine(40, 168, 120, 168);
        g.PutDigger(40, 168, Dir.Right);
        var puffer = g.AddMonster(MonsterKind.Puffer, 72, 168);
        g.Step(new Controls(Dir.None, true));
        for (int i = 0; i < 10; i++) g.Step(default);
        for (int i = 0; i < 2; i++) { g.Step(new Controls(Dir.None, true)); g.Step(default); }
        Assert.True(puffer.Inflate >= 2);
        // A scorcher breathing fire along another tunnel.
        g.DigLine(130, 216, 210, 216);
        var scorcher = g.AddMonster(MonsterKind.Scorcher, 200, 216);
        scorcher.Dir = Dir.Left;
        scorcher.Fire = FireState.Breathing;
        scorcher.FireClock = 0.3;
        // And a rock on its way down.
        var rock = g.AddRock(152, 72);
        g.DigLine(152, 88, 152, 136);
        rock.State = RockState.Falling;
        rock.Y = 100;
        Shot(w, c, "pumping");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 6 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 6 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials)
        {
            g.Initials!.Type('A');
            g.Initials.Type('V');
            Shot(w, c, "initials");
            g.Initials.Type('A');
            g.Idle(5);
        }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
