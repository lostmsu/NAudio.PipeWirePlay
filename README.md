# NAudio.PipeWirePlay

Small `pw-play`-backed playback library for NAudio on Linux/PipeWire.

This repo intentionally stays narrow:

- device discovery via `pw-dump`
- playback via `pw-play --raw`
- `NAudio.Core`-compatible output surface

It is not trying to be a full PipeWire binding.

## Why

NAudio has useful core abstractions such as `IWaveProvider`, `WaveFormat`, and `IWavePlayer`, but it does not currently ship a first-party PipeWire output backend.

This project keeps the NAudio-facing API small and uses PipeWire's own `pw-play` tool as the actual sink.

## Requirements

- Linux
- PipeWire tools available in `PATH`
  - `pw-play`
  - `pw-dump`

## Projects

- `src/NAudio.PipeWirePlay`
  - library targeting `netstandard2.0`
- `samples/NAudio.PipeWirePlay.Sample`
  - simple console sample

## Basic usage

```csharp
using NAudio.Wave;
using NAudio.PipeWirePlay;

var player = new PwPlayWavePlayer();
player.Init(new SineWaveProvider(440, TimeSpan.FromSeconds(2)));
player.Play();
```

## Discovery

```csharp
var sinks = await PipeWireDiscovery.ListPlaybackNodesAsync();
foreach (var sink in sinks)
{
    Console.WriteLine($"{sink.Id} {sink.Name} {sink.Description}");
}
```

## Notes

- `Pause()` currently behaves like `Stop()`. `pw-play` is a simple process sink, so true pause/resume is out of scope for this first version.
- Playback writes raw PCM to `pw-play` over `stdin`. If the producer gets ahead of realtime, PipeWire/backpressure will block the writer instead of accelerating playback.
- Supported raw formats in this implementation:
  - PCM 8-bit unsigned
  - PCM 16-bit signed
  - PCM 32-bit signed
  - IEEE float 32-bit
  - IEEE float 64-bit
