using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVADigDug.Engine;
using AVADigDug.Models;

namespace AVADigDug.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVADigDug/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    private static readonly uint[] Layers = [0xFFF0B428, 0xFFE07A1E, 0xFFC0461E, 0xFF8A2A22];
    private static readonly uint[] Specks = [0xFFC08A14, 0xFFA8500E, 0xFF8A2A10, 0xFF5A1610];
    private const uint Sky = 0xFF0A0E3A, Tunnel = 0xFF000000;

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            DrawEarth(dc);
            switch (g.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    DrawPlay(dc);
                    break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the earth ----------------------------------------------------------------------------

    private void DrawEarth(DrawingContext dc)
    {
        var g = Engine;
        Fill(dc, Sky, 0, 0, Field.Width, Field.FirstDirtRow * Field.Tile);
        // Four layers, deeper ones darker, with a scatter of stones.
        int[] tops = [2, 5, 9, 13, 16];
        for (int i = 0; i < 4; i++)
        {
            double y0 = tops[i] * Field.Tile, y1 = tops[i + 1] * Field.Tile;
            Fill(dc, Layers[i], 0, y0, Field.Width, y1 - y0);
            for (int k = 0; k < 40; k++)
            {
                uint h = (uint)(k * 2654435761u + (uint)i * 40503u);
                Fill(dc, Specks[i], h % Field.Width, y0 + (h >> 10) % (uint)(y1 - y0 - 2), 2, 2);
            }
        }
        // The tunnels, a row of cells at a time, runs merged.
        var ground = g.Ground;
        for (int cy = Field.FirstDirtRow * Field.Tile / Ground.Cell; cy < Ground.CellsY; cy++)
        {
            int run = -1;
            for (int cx = 0; cx <= Ground.CellsX; cx++)
            {
                bool dug = cx < Ground.CellsX && ground.IsDug(cx, cy);
                if (dug && run < 0) run = cx;
                if (!dug && run >= 0)
                {
                    Fill(dc, Tunnel, run * Ground.Cell, cy * Ground.Cell, (cx - run) * Ground.Cell, Ground.Cell);
                    run = -1;
                }
            }
        }
        // Flowers along the surface: one for each round (a big one for every ten).
        int round = g.Round;
        double fx = Field.Width - 10;
        for (int i = 0; i < round / 10 && fx > 120; i++, fx -= 14) Flower(dc, fx, true);
        for (int i = 0; i < round % 10 && fx > 120; i++, fx -= 10) Flower(dc, fx, false);
    }

    private static void Flower(DrawingContext dc, double x, bool big)
    {
        double r = big ? 4 : 2.6;
        Fill(dc, 0xFF3CD84A, x - 0.5, 24, 1, 8);
        foreach (var (dx, dy) in new[] { (-1.0, 0.0), (1.0, 0.0), (0.0, -1.0), (0.0, 1.0) })
            dc.DrawEllipse(Brush(big ? 0xFFFF7AC8 : 0xFFF4F4F4), null, P(x + dx * r * 0.8, 21 + dy * r * 0.8), r * 0.7 * Scale, r * 0.7 * Scale);
        dc.DrawEllipse(Brush(0xFFFFE14A), null, P(x, 21), r * 0.45 * Scale, r * 0.45 * Scale);
    }

    // ----- the play -----------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var r in g.Rocks)
        {
            if (r.Gone) continue;
            double wobble = r.State == RockState.Wobbling && (int)(_clock * 16) % 2 == 0 ? 1 : 0;
            DrawSprite(dc, r.State == RockState.Breaking ? Sprites.RockBroken : Sprites.Rock, r.X - 8 + wobble, r.Y - 8);
        }
        if (g.VegetableLeft > 0 && !(g.VegetableLeft < 2 && (int)(_clock * 8) % 2 == 0))
            DrawSprite(dc, Sprites.VegetableArt(Ground.Vegetable(g.Round).Name), Ground.Start.X - 8, Ground.Start.Y - 8);

        foreach (var m in g.Monsters)
        {
            if (m.Dead) continue;
            DrawMonster(dc, m);
        }

        DrawDigger(dc);

        foreach (var fx in g.Effects)
        {
            if (fx.Kind == EffectKind.Pop) DrawSprite(dc, Sprites.Pop, fx.Pos.X - 8, fx.Pos.Y - 8, px: 1.6, centred: true);
            else Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - fx.Age * 8), 28, White, center: true);
        }

        switch (g.State)
        {
            case GameState.Paused:
                Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME");
                break;
            case GameState.GameOver:
                Banner(dc, "GAME  OVER", null);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.Ready) Banner(dc, $"ROUND {g.Round}", g.State == GameState.Attract ? "DEMONSTRATION" : "PLAYER 1");
                else if (g.Phase == Phase.RoundClear) Banner(dc, $"ROUND {g.Round} CLEAR!", null);
                else if (g.State == GameState.Attract) Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 8), 30, Red, center: true);
                break;
        }
    }

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xE0000000, 30, 104, 164, sub is null ? 24 : 36);
        Text(dc, title, P(Field.Width / 2.0, 116), 46, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 132), 26, White, center: true);
    }

    private void DrawMonster(DrawingContext dc, Monster m)
    {
        int frame = (int)(_clock * 6 + m.X / 16) % 2;
        if (frame < 0) frame = -frame;
        bool left = m.Dir == Dir.Left;
        if (m.Ghost)
        {
            if ((int)(_clock * 10) % 3 != 0) DrawSprite(dc, Sprites.Ghost, m.X - 8, m.Y - 8);
            return;
        }
        // Pumped up: bigger with each pump.
        double px = 1 + 0.18 * m.Inflate;
        Sprites.Sprite art = m.Kind == MonsterKind.Puffer ? Sprites.PufferWalk[frame]
            : m.Fire == FireState.Charging && (int)(_clock * 12) % 2 == 0 ? Sprites.ScorcherGlow : Sprites.ScorcherWalk[frame];
        DrawSprite(dc, art, m.X - 8, m.Y - 8, flip: left, px: px, centred: true);

        if (m.FireLength > 0)
        {
            int dir = left ? -1 : 1;
            for (double d = 0; d < m.FireLength; d += 8)
            {
                double x = m.X + dir * (8 + d) + (left ? -8 : 0);
                DrawSprite(dc, Sprites.Flame[(int)(_clock * 12 + d / 8) % 2], x, m.Y - 6);
            }
        }
    }

    private void DrawDigger(DrawingContext dc)
    {
        var g = Engine;
        var d = g.Digger;
        if (g.Phase == Phase.Dying)
        {
            if (g.PhaseTime < 0.8) DrawSprite(dc, Sprites.DiggerWalk[0], d.X - 8, d.Y - 8, angle: g.PhaseTime * 900);
            else if (g.PhaseTime < 2) DrawSprite(dc, Sprites.DiggerFlat, d.X - 8, d.Y - 8);
            return;
        }
        if (g.Harpoon is { } h)
        {
            var from = d.Pos + new Vec(h.Dir.Dx(), h.Dir.Dy()) * 7;
            var tip = g.HarpoonTip;
            dc.DrawLine(new Pen(White, 2), P(from), P(tip));
            dc.DrawEllipse(Brush(0xFFFFE14A), null, P(tip), 1.6 * Scale, 1.6 * Scale);
        }

        var art = Sprites.DiggerWalk[d.Moving ? (int)(d.Stride / 4) % 2 : 0];
        switch (d.Facing)
        {
            case Dir.Left: DrawSprite(dc, art, d.X - 8, d.Y - 8, flip: true); break;
            case Dir.Up: DrawSprite(dc, art, d.X - 8, d.Y - 8, angle: -90); break;
            case Dir.Down: DrawSprite(dc, art, d.X - 8, d.Y - 8, angle: 90, flip: true, flipAfter: true); break;
            default: DrawSprite(dc, art, d.X - 8, d.Y - 8); break;
        }
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored, turned or scaled about its middle.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, bool flip = false, double angle = 0,
        double px = 1, bool centred = false, bool flipAfter = false)
    {
        if (centred) { x -= s.Width * (px - 1) / 2; y -= s.Height * (px - 1) / 2; }
        var centre = P(x + s.Width * px / 2.0, y + s.Height * px / 2.0);
        var turn = Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(angle * Math.PI / 180)
            * (flipAfter ? Matrix.CreateScale(-1, 1) : Matrix.Identity) * Matrix.CreateTranslation(centre.X, centre.Y);
        using var pushed = angle != 0 || flipAfter ? (IDisposable?)dc.PushTransform(turn) : null;
        bool mirror = flip && !flipAfter;
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[mirror ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
