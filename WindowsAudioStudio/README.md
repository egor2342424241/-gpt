# Windows Audio Studio

Windows desktop audio processor based on WPF, .NET 9 and NAudio 3.3.

## Audio engine

- Enumerates Windows render audio sessions and groups them by process ID.
- Captures each selected process tree using WASAPI process-loopback capture.
- Routes the captured stream into an independent DSP chain and renders it to the default output endpoint.
- Temporarily mutes the original app session while processing is active and restores its user volume/mute values when processing stops.
- Keeps a separate DSP profile for every process.

## DSP currently implemented

- 20-band peaking equalizer: 31 Hz to 20 kHz, ±12 dB.
- Bass boost.
- Stereo width (mid/side).
- Delay/reverb.
- Compressor.
- Soft limiter.
- Per-application output volume and mute.

Process-loopback capture requires Windows 10 version 2004 (build 19041) or newer. It uses Windows `ActivateAudioInterfaceAsync` through NAudio's `WasapiRecorderBuilder.WithProcessLoopback`.

## Build

Install the .NET 9 SDK and build on Windows:

```powershell
dotnet restore WindowsAudioStudio/WindowsAudioStudio.csproj
dotnet build WindowsAudioStudio/WindowsAudioStudio.csproj -c Release
dotnet publish WindowsAudioStudio/WindowsAudioStudio.csproj -c Release -r win-x64 --self-contained true
```

A Windows GitHub Actions workflow builds and uploads a self-contained x64 artifact for each change to this branch.
