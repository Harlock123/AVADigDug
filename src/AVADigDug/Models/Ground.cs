namespace AVADigDug.Models;

public enum MonsterKind
{
    /// <summary>Round, goggled, and inflatable.</summary>
    Puffer,
    /// <summary>A little dragon that breathes fire along its tunnel.</summary>
    Scorcher,
}

/// <summary>Where a monster starts a round.</summary>
public readonly record struct Spawn(MonsterKind Kind, double X, double Y);

/// <summary>
/// The earth, as a grid of 4-pixel cells, each dug or not. The digger clears a 16-pixel-wide tunnel as
/// it goes; monsters (unless they turn to ghosts) and the harpoon need a tunnel. A round's layout is made
/// from the seed: monsters in short tunnels of their own, rocks set in the dirt, and a shaft down the
/// middle for the digger to start in.
/// </summary>
public sealed class Ground
{
    public const int Cell = 4;
    public const int CellsX = Field.Width / Cell;
    public const int CellsY = Field.Height / Cell;

    private readonly bool[,] _dug = new bool[CellsX, CellsY];

    public List<Spawn> Spawns { get; } = [];
    public List<Vec> Rocks { get; } = [];
    /// <summary>Where the digger starts (and the bonus vegetable appears).</summary>
    public static readonly Vec Start = new(Field.CentreX(7), Field.CentreY(8));

    public Ground()
    {
        // The sky is open.
        for (int x = 0; x < CellsX; x++)
            for (int y = 0; y < Field.FirstDirtRow * Field.Tile / Cell; y++) _dug[x, y] = true;
    }

    public bool IsDug(int cx, int cy) => cx is >= 0 and < CellsX && cy is >= 0 and < CellsY && _dug[cx, cy];

    /// <summary>Is the point in open tunnel (or sky)?</summary>
    public bool OpenAt(double x, double y) => IsDug((int)Math.Floor(x / Cell), (int)Math.Floor(y / Cell));

    /// <summary>A 16-pixel body centred here would fit wholly in tunnel.</summary>
    public bool Fits(double x, double y)
    {
        if (x < Field.Tile / 2.0 || x > Field.Width - Field.Tile / 2.0 || y < Field.Surface || y > Field.Bottom) return false;
        int x0 = (int)Math.Floor((x - 7.9) / Cell), x1 = (int)Math.Floor((x + 7.9) / Cell);
        int y0 = (int)Math.Floor((y - 7.9) / Cell), y1 = (int)Math.Floor((y + 7.9) / Cell);
        for (int cx = x0; cx <= x1; cx++)
            for (int cy = y0; cy <= y1; cy++)
                if (!IsDug(cx, cy)) return false;
        return true;
    }

    /// <summary>Digs out a 16-pixel square centred here. Returns how many cells were dirt.</summary>
    public int Dig(double x, double y)
    {
        int n = 0;
        int x0 = (int)Math.Floor((x - 7.9) / Cell), x1 = (int)Math.Floor((x + 7.9) / Cell);
        int y0 = (int)Math.Floor((y - 7.9) / Cell), y1 = (int)Math.Floor((y + 7.9) / Cell);
        for (int cx = Math.Max(0, x0); cx <= Math.Min(CellsX - 1, x1); cx++)
            for (int cy = Math.Max(0, y0); cy <= Math.Min(CellsY - 1, y1); cy++)
                if (!_dug[cx, cy]) { _dug[cx, cy] = true; n++; }
        return n;
    }

    public int DugCount()
    {
        int n = 0;
        foreach (var d in _dug) if (d) n++;
        return n;
    }

    /// <summary>A round's layout.</summary>
    public static Ground Build(int seed, int round)
    {
        var rng = new Random(unchecked(seed * 7919 + round * 104729));
        var g = new Ground();
        // The digger's shaft, from the surface down to the middle.
        for (int r = Field.FirstDirtRow; r <= Field.RowOf(Start.Y); r++) g.Dig(Start.X, Field.CentreY(r));

        var used = new HashSet<(int, int)>();
        for (int r = Field.FirstDirtRow; r <= Field.RowOf(Start.Y) + 1; r++)
            for (int c = 6; c <= 8; c++) used.Add((c, r));

        int monsters = Math.Min(8, 4 + (round - 1) / 2);
        int scorchers = Math.Max(1, monsters * 2 / 5);
        for (int i = 0, tries = 0; i < monsters && tries < 500; tries++)
        {
            bool across = rng.Next(2) == 0;
            int len = 3 + rng.Next(2);
            int c = rng.Next(across ? Field.Cols - len : Field.Cols), r = rng.Next(Field.FirstDirtRow + 1, (across ? Field.Rows : Field.Rows - len) - 1);
            var tiles = Enumerable.Range(0, len).Select(k => across ? (c + k, r) : (c, r + k)).ToList();
            // Keep tunnels apart from each other and from the middle.
            if (tiles.Any(t => Around(t).Any(used.Contains))) continue;
            foreach (var t in tiles) { used.Add(t); g.Dig(Field.CentreX(t.Item1), Field.CentreY(t.Item2)); }
            var (mc, mr) = tiles[len / 2];
            g.Spawns.Add(new Spawn(i < scorchers ? MonsterKind.Scorcher : MonsterKind.Puffer, Field.CentreX(mc), Field.CentreY(mr)));
            i++;
        }

        int rocks = Math.Min(6, 3 + round / 4);
        for (int i = 0, tries = 0; i < rocks && tries < 500; tries++)
        {
            int c = rng.Next(1, Field.Cols - 1), r = rng.Next(Field.FirstDirtRow + 1, Field.Rows - 4);
            // In solid dirt, with dirt under it, and not straight above a tunnel or another rock.
            if (Around((c, r)).Any(used.Contains) || used.Contains((c, r + 2)) || used.Contains((c, r + 3))) continue;
            used.Add((c, r));
            g.Rocks.Add(new Vec(Field.CentreX(c), Field.CentreY(r)));
            i++;
        }
        return g;
    }

    private static IEnumerable<(int, int)> Around((int C, int R) t)
    {
        for (int dc = -1; dc <= 1; dc++)
            for (int dr = -1; dr <= 1; dr++) yield return (t.C + dc, t.R + dr);
    }

    /// <summary>The bonus vegetable for a round, and its points.</summary>
    public static (string Name, int Points) Vegetable(int round) => round switch
    {
        1 => ("CARROT", 400),
        2 => ("TURNIP", 600),
        3 => ("MUSHROOM", 800),
        <= 5 => ("CUCUMBER", 1000),
        <= 7 => ("EGGPLANT", 2000),
        <= 9 => ("PEPPER", 3000),
        <= 11 => ("TOMATO", 4000),
        <= 13 => ("ONION", 5000),
        <= 15 => ("WATERMELON", 6000),
        _ => ("PINEAPPLE", 8000),
    };
}
