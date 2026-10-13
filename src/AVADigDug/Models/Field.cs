namespace AVADigDug.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

public enum Dir { None, Up, Down, Left, Right }

public static class Dirs
{
    public static int Dx(this Dir d) => d switch { Dir.Left => -1, Dir.Right => 1, _ => 0 };
    public static int Dy(this Dir d) => d switch { Dir.Up => -1, Dir.Down => 1, _ => 0 };
    public static bool Horizontal(this Dir d) => d is Dir.Left or Dir.Right;
    public static Dir Reverse(this Dir d) => d switch
    {
        Dir.Up => Dir.Down,
        Dir.Down => Dir.Up,
        Dir.Left => Dir.Right,
        Dir.Right => Dir.Left,
        _ => Dir.None,
    };
    public static readonly Dir[] All = [Dir.Up, Dir.Left, Dir.Down, Dir.Right];
}

/// <summary>
/// The screen in game pixels: 224 by 256, portrait like the arcade's: a grid of 16-pixel tiles, 14
/// across and 16 down. The top two rows are sky, with the surface the digger walks along on row 1;
/// below is the earth, in four layers, deeper ones worth more.
/// </summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const int Tile = 16;
    public const int Cols = Width / Tile;
    public const int Rows = Height / Tile;
    public const int SurfaceRow = 1;
    public const int FirstDirtRow = 2;
    public const double Step = 1.0 / 60;

    public static double CentreX(int col) => col * Tile + Tile / 2.0;
    public static double CentreY(int row) => row * Tile + Tile / 2.0;
    public static int ColOf(double x) => (int)Math.Floor(x / Tile);
    public static int RowOf(double y) => (int)Math.Floor(y / Tile);
    public static double Surface => CentreY(SurfaceRow);
    public static double Bottom => CentreY(Rows - 1);

    /// <summary>On the tile lattice along this axis (the other axis is free).</summary>
    public static bool OnLine(double v) => Math.Abs((v - Tile / 2.0) % Tile) < 0.01 || Math.Abs((v - Tile / 2.0) % Tile - Tile) < 0.01;
    public static double Snap(double v) => Math.Round((v - Tile / 2.0) / Tile) * Tile + Tile / 2.0;

    /// <summary>The layer of earth at a depth, 1 (top) to 4 (deepest).</summary>
    public static int Layer(double y) => RowOf(y) switch { <= 4 => 1, <= 8 => 2, <= 12 => 3, _ => 4 };
}
