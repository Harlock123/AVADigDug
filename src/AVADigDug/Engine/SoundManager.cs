using AVADigDug.Audio;

namespace AVADigDug.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The
/// tunes play on the music channel; the digging music loops on the ambience channel, turned up while the
/// digger moves and down while it stands. In a virtual machine a larger mixing buffer keeps the sound
/// from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.4f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    private string File(Sfx s) => Path.Combine(_folder, s + ".wav");

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune: _engine.PlayMusic(File(sfx), loop: true); return;
            case Sfx.StartTune or Sfx.RoundClear or Sfx.GameOver or Sfx.Death or Sfx.LastOne: _engine.PlayMusic(File(sfx), loop: false); return;
            // The digging music loops all round; it's only heard while the digger moves.
            case Sfx.Music: _engine.PlayAmbience(File(sfx)); return;
            case Sfx.MusicStop: _engine.StopAmbience(); return;
            case Sfx.MusicUp: _engine.SetAmbienceVolume(0.45f); return;
            case Sfx.MusicDown: _engine.SetAmbienceVolume(0f); return;
        }
        _engine.PlayEffect(File(sfx), sfx == Sfx.Pump ? 0.6f : 1f);
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
