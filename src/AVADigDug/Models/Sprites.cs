namespace AVADigDug.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The hero is drawn row by row; the bigger and
/// rounder things are painted from ellipses and boxes into a pixel grid, which keeps them easy to read
/// and to change. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F4, // white
        ['k'] = 0xFF14141C, // black
        ['u'] = 0xFF2A6AFF, // blue
        ['U'] = 0xFF8AB8FF, // light blue
        ['r'] = 0xFFE83A3A, // red
        ['R'] = 0xFF9A1E1E, // dark red
        ['y'] = 0xFFFFE14A, // yellow
        ['o'] = 0xFFFF8A1E, // orange
        ['g'] = 0xFF3CD84A, // green
        ['G'] = 0xFF1E8A2A, // dark green
        ['m'] = 0xFF9A9AAA, // grey
        ['M'] = 0xFF5A5A6A, // dark grey
        ['b'] = 0xFF8A5A2A, // brown
        ['p'] = 0xFFB070FF, // purple
        ['P'] = 0xFFFF7AC8, // pink
        ['c'] = 0xFF4FD7FF, // cyan
    };

    // ----- the digger (facing right; turned or mirrored for the other ways) ----------------------

    private static Canvas Digger(bool step) => new Canvas(16, 16)
        .Ellipse(7, 5, 5, 4.5, 'w').Box(9, 3, 12, 6, 'u').Box(10, 4, 11, 5, 'U')   // helmet and visor
        .Box(3, 9, 11, 12, 'w').Box(5, 9, 9, 10, 'r')                            // suit, red chest
        .Box(11, 9, 14, 10, 'w').Box(13, 8, 15, 11, 'M')                         // arm and pump nozzle
        .Box(step ? 3 : 4, 13, step ? 5 : 6, 15, 'u').Box(step ? 9 : 8, 13, step ? 11 : 10, 15, 'u');

    public static readonly Sprite[] DiggerWalk = [Digger(false).Done(), Digger(true).Done()];

    /// <summary>Caught: the digger squashed flat.</summary>
    public static readonly Sprite DiggerFlat = new Canvas(16, 16).Ellipse(8, 12, 7, 3, 'w').Ellipse(8, 11, 3, 2, 'u').Box(2, 13, 4, 15, 'u').Box(11, 13, 13, 15, 'u').Done();

    // ----- the monsters ---------------------------------------------------------------------------

    private static Canvas Puffer(bool step) => new Canvas(16, 16)
        .Ellipse(8, 8, 7, 7, 'r')
        .Ellipse(5, 6.5, 2.6, 2.6, 'y').Ellipse(11, 6.5, 2.6, 2.6, 'y').Dot(5, 6, 'k').Dot(11, 6, 'k').Box(7, 6, 8, 6, 'y')
        .Box(step ? 3 : 4, 14, step ? 5 : 6, 15, 'R').Box(step ? 11 : 10, 14, step ? 13 : 12, 15, 'R');

    public static readonly Sprite[] PufferWalk = [Puffer(false).Done(), Puffer(true).Done()];

    private static Canvas Scorcher(bool step, char body) => new Canvas(16, 16)
        .Ellipse(7, 9, 6, 5, body).Ellipse(12, 5, 3.5, 3.5, body).Box(13, 4, 15, 7, body)
        .Dot(12, 4, 'w').Dot(13, 4, 'k').Box(1, 4, 2, 6, 'w').Box(3, 3, 3, 5, 'w')     // eye, wings
        .Box(step ? 3 : 4, 13, step ? 5 : 6, 15, 'G').Box(step ? 8 : 9, 13, step ? 10 : 11, 15, 'G');

    public static readonly Sprite[] ScorcherWalk = [Scorcher(false, 'g').Done(), Scorcher(true, 'g').Done()];
    /// <summary>Glowing before it breathes.</summary>
    public static readonly Sprite ScorcherGlow = Scorcher(false, 'y').Done();

    /// <summary>Just the eyes, drifting through the dirt.</summary>
    public static readonly Sprite Ghost = new Canvas(16, 16).Ellipse(5, 8, 2.8, 3.2, 'w').Ellipse(11, 8, 2.8, 3.2, 'w').Dot(5, 8, 'k').Dot(11, 8, 'k').Done();

    /// <summary>A flame, one length of the fire.</summary>
    public static readonly Sprite[] Flame =
    [
        new Canvas(8, 12).Ellipse(4, 7, 4, 4.5, 'r').Ellipse(4, 7.5, 2.5, 3, 'o').Ellipse(4, 8, 1.2, 1.6, 'y').Done(),
        new Canvas(8, 12).Ellipse(4, 6, 4, 5, 'r').Ellipse(4, 6.5, 2.5, 3.5, 'o').Ellipse(4, 7, 1.2, 2, 'y').Done(),
    ];

    /// <summary>A monster popping.</summary>
    public static readonly Sprite Pop = new Canvas(16, 16).Ellipse(8, 8, 7.5, 7.5, 'w').Ellipse(8, 8, 5.5, 5.5, '.')
        .Dot(1, 1, 'w').Dot(14, 1, 'w').Dot(1, 14, 'w').Dot(14, 14, 'w').Dot(8, 0, 'w').Dot(8, 15, 'w').Done();

    // ----- rocks and vegetables -------------------------------------------------------------------

    public static readonly Sprite Rock = new Canvas(16, 16).Ellipse(8, 9, 7.5, 6.5, 'b').Ellipse(7, 7, 5, 4, 'o')
        .Ellipse(6, 6, 2.5, 1.5, 'y').Box(9, 11, 13, 11, 'M').Box(3, 12, 6, 12, 'M').Done();
    public static readonly Sprite RockBroken = new Canvas(16, 16).Ellipse(4, 12, 3.5, 3, 'b').Ellipse(12, 12, 3.5, 3, 'b')
        .Ellipse(8, 9, 2.5, 2, 'o').Dot(3, 11, 'y').Dot(11, 11, 'y').Done();

    public static Sprite VegetableArt(string name) => name switch
    {
        "CARROT" => new Canvas(16, 16).Box(4, 0, 5, 4, 'g').Box(7, 0, 8, 4, 'g').Box(10, 1, 11, 4, 'g').Ellipse(8, 9, 5, 6, 'o').Box(6, 14, 9, 15, 'o').Box(4, 8, 6, 8, 'R').Box(9, 11, 11, 11, 'R').Done(),
        "TURNIP" => new Canvas(16, 16).Box(6, 0, 9, 4, 'g').Ellipse(8, 10, 6, 5.5, 'w').Ellipse(8, 8, 5.5, 3, 'p').Box(7, 15, 8, 15, 'w').Done(),
        "MUSHROOM" => new Canvas(16, 16).Ellipse(8, 6, 7.5, 5.5, 'r').Box(0, 6, 15, 7, '.').Box(5, 7, 10, 14, 'w').Dot(4, 3, 'w').Dot(10, 2, 'w').Dot(12, 5, 'w').Done(),
        "CUCUMBER" => new Canvas(16, 16).Ellipse(8, 8, 3.5, 7.5, 'g').Dot(7, 4, 'G').Dot(9, 8, 'G').Dot(7, 11, 'G').Box(7, 0, 8, 1, 'G').Done(),
        "EGGPLANT" => new Canvas(16, 16).Ellipse(8, 10, 5.5, 6, 'p').Box(5, 1, 10, 4, 'g').Dot(6, 8, 'w').Done(),
        "PEPPER" => new Canvas(16, 16).Ellipse(8, 9, 6, 6, 'g').Box(7, 0, 8, 3, 'G').Box(7, 5, 8, 14, 'G').Dot(5, 7, 'w').Done(),
        "TOMATO" => new Canvas(16, 16).Ellipse(8, 9, 7, 6, 'r').Box(5, 2, 10, 3, 'g').Box(7, 1, 8, 4, 'g').Dot(5, 7, 'w').Done(),
        "ONION" => new Canvas(16, 16).Ellipse(8, 10, 6.5, 5.5, 'y').Box(7, 0, 8, 5, 'g').Box(7, 7, 8, 14, 'o').Dot(5, 9, 'w').Done(),
        "WATERMELON" => new Canvas(16, 16).Ellipse(8, 8, 7.5, 6, 'G').Ellipse(8, 8, 6, 4.5, 'g').Box(2, 8, 13, 8, 'G').Dot(5, 6, 'w').Done(),
        _ => new Canvas(16, 16).Box(6, 0, 9, 4, 'g').Box(4, 1, 5, 3, 'g').Box(10, 1, 11, 3, 'g').Ellipse(8, 10, 5, 5.5, 'y')
            .Dot(6, 8, 'o').Dot(9, 8, 'o').Dot(7, 11, 'o').Dot(10, 12, 'o').Dot(5, 12, 'o').Done(),
    };
}
