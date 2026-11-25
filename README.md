# NexaKord

**Turn a game controller into a MIDI instrument.**  
NexaKord is a Windows application by **Caleb France · Canexa Studios** that lets you use a game controller as a playable MIDI input, with chord-focused musical control and progression-oriented workflows.
https://nexakord.freedev.app/
> Current documented release: **v0.3.1.1**  
> Project status: community testing and contributions welcome.

## The idea

NexaKord lowers the barrier between having musical ideas and playing them. Instead of needing a traditional MIDI keyboard, you can map a familiar game controller and use it as an expressive input for music software.

The aim is simple: make a controller feel like an instrument, and make musical exploration more immediate.

## Current documented workflow

The supplied v3.1.1 setup guide describes this first-run path:

1. Extract the complete NexaKord ZIP to a normal folder.
2. Run `BUILD.bat`. It builds `NexaKord.exe`, installs it in that folder, creates Desktop and Start menu shortcuts, and launches it.
3. On first use, choose **Get MIDI cable** in the app and install the free [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) virtual MIDI cable.
4. In loopMIDI, create a port named `NexaKord`.
5. Connect or Bluetooth-pair a game controller. Follow the in-app instructions, select/scan for the controller if needed, and press **Test**.
6. Use **Teach** to map the controller.
7. In FL Studio, open **F10 → MIDI**, select the NexaKord loopMIDI input and enable it.

Documented keyboard shortcuts:
- `T` — switch between piano and guitar fretboard views.
- `H` — toggle HiChord.

Controller support and behaviour can vary by device. If a controller is not detected, check the device dropdown and use Scan.

## Building from source

The included `BUILD.bat` describes the current build process:

- Windows batch-based build/install script.
- Uses the C# compiler (`csc.exe`) from the .NET Framework v4 compiler location on Windows.
- Compiles the C# files in `src\*.cs` as a Windows GUI executable.
- References `System.dll`, `System.Drawing.dll`, and `System.Windows.Forms.dll`.
- Expects `NAudio.dll` alongside the project.
- Expects helper scripts in `tools\` (`find-old.ps1` and `shortcuts.ps1`).
- Checks for nine expected source files before compiling.

Expected source files checked by the build script:

`AssemblyInfo.cs`, `Core.cs`, `Data.cs`, `Theory.cs`, `Controls.cs`, `GuitarSolver.cs`, `GuitarView.cs`, `LearnForm.cs`, and `UI.cs`.

### Build on Windows

1. Keep the original project folder structure intact.
2. Ensure the expected `src`, `tools`, and dependency files are present.
3. Run `BUILD.bat` from the extracted project folder.
4. If compilation fails, inspect `build-log.txt` in the project folder.

The build script is designed to leave an existing executable untouched when compilation fails. It also includes upgrade handling for older NexaKord/GamepadMIDI installs and carries forward profile/settings data when applicable.

## Troubleshooting

| Issue | First thing to try |
|---|---|
| Controller does not appear | Check the controller dropdown, reconnect/pair it, then use Scan. |
| Controller input is not mapped | Use **Teach** and follow the in-app mapping flow. |
| No MIDI reaches your DAW | Confirm loopMIDI is installed, the `NexaKord` port exists, and the DAW input is enabled. |
| Build fails | Read `build-log.txt`; confirm the full folder structure and required files are present. |
| App crashes | Check `%APPDATA%\NexaKord\crash.txt`. |

For FL Studio, the documented route is **F10 → MIDI → NexaKord input → Enable**.

## Roadmap and long-term direction

The project is intended to grow beyond a single controller-to-MIDI application. The following are **directional goals**, not promises of features already implemented:

- **Fine-tuning NexaKord:** improve reliability, controller detection, mapping clarity, playability, timing/response, onboarding, and the overall user experience through real-world testing.
- **Broader controller testing:** build a practical compatibility picture across different gamepads, connection methods, Windows setups, and DAWs.
- **FM-1 firmware concept:** explore a dedicated firmware/software layer called **FM-1** that could support NexaKord and, over time, other music applications. The intention is to investigate a reusable musical-control platform rather than a one-purpose endpoint.
- **A wider musical ecosystem:** consider how future tools and applications could share a common foundation for controller input and musical interaction.

FM-1 is a future development goal. Its hardware requirements, firmware architecture, supported protocols, compatibility, and release plan still need to be researched and defined with contributors.

## Join the project

NexaKord is looking to form a small, constructive community of:

- **Testers** willing to try different controllers, report reproducible bugs, and share clear feedback.
- **C# / Windows developers** interested in the application, UI, input handling, build system, and reliability.
- **Music-tech / MIDI developers** interested in MIDI workflows, DAW integration, and musical interaction.
- **Firmware / embedded developers** interested in helping research and shape the longer-term FM-1 direction.
- **Musicians and producers** who can evaluate whether the instrument feels intuitive and useful in real sessions.

### How to contribute

1. Explore the application and note what works, what feels awkward, and what breaks.
2. For bugs, include Windows version, controller model, USB/Bluetooth connection, steps to reproduce, expected behaviour, actual behaviour, and relevant logs.
3. For code contributions, keep changes focused and explain the reason for the change.
4. For larger ideas, open a discussion before investing in a substantial implementation.

Please do not include private data in bug reports. Remove personal information from logs before sharing them.

## Current project facts and limitations

This README is based on the supplied v3.1.1 `START HERE.txt`, `BUILD.bat`, and `UNINSTALL.bat`. The complete C# source tree was not included with those files at the time this document was prepared. As a result, detailed claims about internal chord algorithms, progression rules, supported MIDI message types, latency, controller API coverage, licensing, and FM-1 implementation are intentionally not asserted here.

Before publishing this repository, add:
- The complete `src/` and `tools/` folders and required dependencies.
- A licence file stating the terms under which contributions and reuse are allowed.
- A contribution guide and issue templates if desired.
- Screenshots or a short demo showing the actual current application.
- Confirmed contact and community links.

## Credits

**NexaKord**  
Created by **Caleb France** · **Canexa Studios**

If you are interested in testing, contributing, or helping shape FM-1, get in touch through the project's repository discussions/issues or the contact link published by the maintainer.
