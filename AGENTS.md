# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A digging game in the style of Namco's 1982 arcade game Dig Dug, in C# on .NET 10 and Avalonia 12, drawn through
Avalonia's `DrawingContext`. Tunnel through the earth, pump the monsters until they pop, and drop rocks on them. One
self-contained executable per platform (Windows, macOS and Linux, x64 and ARM). The pixel art is drawn in code
(`Models/Sprites.cs`) and the sounds are synthesised by `tools/make_sounds.py`.

## Commands

```bash
dotnet build AVADigDug.slnx                       # the SDK version is in global.json (10.0.400 or later)
dotnet test AVADigDug.slnx                        # every test runs headless: no window opens
dotnet run --project src/AVADigDug                # play it; --no-audio for silence
DIGDUG_SHOTS=$PWD/Images dotnet test tests/AVADigDug.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                      # rebuild src/AVADigDug/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVADigDug/Models/`: the playfield, with no Avalonia in it.
  - `Field.cs` has `Vec`, the `Dir` directions and the measures: a 224 x 256 portrait screen of 16-pixel tiles,
    and the fixed step.
  - `Ground.cs` is the earth as a grid of 4-pixel cells, dug or not, and lays out each round from a seed.
  - `Sprites.cs` holds the pixel art.
- `src/AVADigDug/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, attract, play, pause, initials, high scores) and the fixed-step loop;
    `GameEngine.Play.cs` plays a round: the digger, the harpoon, monsters, ghosts, rocks and the vegetable.
  - `Entities.cs` has the digger, harpoon, monsters, rocks, effects and the `Controls` record.
  - `AttractPilot.cs` is the computer player, used by the attract mode and the tests.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVADigDug/Views/`: `GameCanvas` (split over partial files) draws a fixed 1024 x 768 picture scaled to the
  window, with the score panels either side, and runs the 60 Hz timer. Ctrl+S saves a screenshot to the
  Pictures folder.
- `src/AVADigDug/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVADigDug.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: `GameplayTests` and `ScreenshotTests`.

## Rules that keep it working

- **Fixed steps.** `GameEngine` advances in fixed 1/60 s steps (`Field.Step`) from an accumulator, independent of
  the frame rate, so tests can play deterministically from a seed. Draw random numbers from the engine's seeded
  `_rng` (`new Random(seed)`), never from `Random.Shared`. `Ground` lays out a round from its own
  `Random` built from the seed and the round number.
- **Engine first, drawing second.** Gameplay belongs in `Engine/` and `Models/`, where tests can reach it without
  a window. `Views/` only reads the game and draws it.
- **Input goes through `InputHandler`.** Movement is a `Dir` (the latest key held wins), not a signed `dy`; the
  stick maps negative Y to `Dir.Up`. Test new controls through `InputHandler`, not by setting `Controls` directly.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVADigDug`); tests load the
  table from a fresh folder under the system temp path and delete it afterwards. The screenshot tests use a
  silent `SoundManager`.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release; either then asks the portfolio site to rebuild.
