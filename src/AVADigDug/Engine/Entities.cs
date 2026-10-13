using AVADigDug.Models;

namespace AVADigDug.Engine;

/// <summary>The digger: moves along the tile lattice, digging as it goes.</summary>
public sealed class Digger
{
    public double X { get; set; }
    public double Y { get; set; }
    public Dir Facing { get; set; } = Dir.Right;
    public bool Moving { get; set; }
    public double Stride { get; set; }
    public Vec Pos => new(X, Y);
}

/// <summary>The air pump's harpoon: shoots out along a tunnel and hooks a monster.</summary>
public sealed class Harpoon
{
    public required Dir Dir { get; init; }
    public double Length { get; set; }
    public Monster? Hooked { get; set; }
    public const double Reach = 44;
}

public enum FireState { None, Charging, Breathing }

public sealed class Monster
{
    public required MonsterKind Kind { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public required Vec Home { get; init; }
    public Dir Dir { get; set; } = Dir.Left;
    /// <summary>Drifting through the dirt as a pair of eyes.</summary>
    public bool Ghost { get; set; }
    public double GhostClock { get; set; }
    /// <summary>Pumped up: 0 (normal) to 4 (pop). A pumped monster can't move or hurt.</summary>
    public double Inflate { get; set; }
    public double SincePump { get; set; }
    public FireState Fire { get; set; }
    public double FireClock { get; set; }
    public double FireCooldown { get; set; } = 2;
    /// <summary>The last one left, running for the exit.</summary>
    public bool Escaping { get; set; }
    /// <summary>Caught under a falling rock.</summary>
    public Rock? Crushed { get; set; }
    public bool Dead { get; set; }
    public Vec Pos => new(X, Y);
    public bool Harmless => Inflate > 0 || Crushed is not null || Dead;

    /// <summary>The fire's reach in front of a breathing scorcher (0 when there's none).</summary>
    public double FireLength => Fire == FireState.Breathing ? Math.Min(40, 8 + FireClock * 160) : 0;
}

public enum RockState { Set, Waiting, Wobbling, Falling, Breaking }

public sealed class Rock
{
    public double X { get; set; }
    public double Y { get; set; }
    public RockState State { get; set; }
    public double Clock { get; set; }
    public List<Monster> Caught { get; } = [];
    public bool Gone { get; set; }
}

public enum EffectKind { Score, Pop }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public MonsterKind Monster { get; init; }
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: a direction to move, and the pump.</summary>
public record struct Controls(Dir Move, bool Pump);
