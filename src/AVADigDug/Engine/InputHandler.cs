using Avalonia.Input;
using AVADigDug.Models;

namespace AVADigDug.Engine;

/// <summary>Keyboard and gamepad boiled down to the controls: a way to dig (the latest key held wins), and the pump.</summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    public void Update() => _pad.Update();

    private static Dir Way(Key k) => k switch
    {
        Key.Up or Key.W => Dir.Up,
        Key.Down or Key.S => Dir.Down,
        Key.Left or Key.A => Dir.Left,
        Key.Right or Key.D => Dir.Right,
        _ => Dir.None,
    };

    public Controls Controls
    {
        get
        {
            var move = Dir.None;
            for (int i = _held.Count - 1; i >= 0 && move == Dir.None; i--) move = Way(_held[i]);
            if (move == Dir.None)
                move = _pad.DPadUp || _pad.LeftStickY < -0.5f ? Dir.Up
                    : _pad.DPadDown || _pad.LeftStickY > 0.5f ? Dir.Down
                    : _pad.DPadLeft || _pad.LeftStickX < -0.5f ? Dir.Left
                    : _pad.DPadRight || _pad.LeftStickX > 0.5f ? Dir.Right : Dir.None;
            bool pump = Has(Key.Space) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || Has(Key.Z) || Has(Key.X)
                        || _pad.ButtonA || _pad.ButtonB || _pad.ButtonX;
            return new Controls(move, pump);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
