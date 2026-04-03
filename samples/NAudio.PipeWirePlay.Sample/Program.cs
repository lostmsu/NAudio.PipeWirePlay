using NAudio.PipeWirePlay;
using NAudio.Wave;

if (!await PipeWireDiscovery.IsAvailableAsync())
{
    Console.Error.WriteLine("PipeWire tools were not found. Expected pw-dump and pw-play in PATH.");
    return 1;
}

var sinks = await PipeWireDiscovery.ListPlaybackNodesAsync();
Console.WriteLine("Playback nodes:");
foreach (var sink in sinks)
{
    Console.WriteLine($"  {sink.Id,4} serial={sink.Serial,-6} name={sink.Name} desc={sink.Description}");
}

var target = args.Length > 0 ? args[0] : null;
if (!string.IsNullOrWhiteSpace(target))
{
    Console.WriteLine($"Targeting PipeWire node: {target}");
}

using var playback = new PwPlayWavePlayer(new PwPlayOptions
{
    Target = target,
    Latency = "30ms",
    MediaRole = "Notification",
});

playback.PlaybackStopped += (_, e) =>
{
    if (e?.Exception is not null)
    {
        Console.Error.WriteLine($"Playback stopped with error: {e.Exception}");
        return;
    }

    Console.WriteLine("Playback stopped.");
};

playback.Init(new SineWaveProvider(440, TimeSpan.FromSeconds(2)));
Console.WriteLine("Playing a 440Hz sine wave for 2 seconds...");
playback.Play();

while (playback.PlaybackState == PlaybackState.Playing)
{
    await Task.Delay(50);
}

return 0;

sealed class SineWaveProvider : IWaveProvider
{
    readonly WaveFormat waveFormat;
    readonly int totalSamples;
    readonly double frequency;
    readonly short amplitude;
    int emittedSamples;

    public SineWaveProvider(double frequency, TimeSpan duration, int sampleRate = 24000, short amplitude = 8192)
    {
        this.frequency = frequency;
        this.amplitude = amplitude;
        waveFormat = new WaveFormat(sampleRate, 16, 1);
        totalSamples = (int)Math.Round(duration.TotalSeconds * sampleRate);
    }

    public WaveFormat WaveFormat => waveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        var bytesPerSample = waveFormat.BlockAlign;
        var maxSamples = count / bytesPerSample;
        var remainingSamples = totalSamples - emittedSamples;
        var samplesToWrite = Math.Min(maxSamples, remainingSamples);
        if (samplesToWrite <= 0)
        {
            return 0;
        }

        for (var i = 0; i < samplesToWrite; i++)
        {
            var sampleIndex = emittedSamples + i;
            var angle = 2 * Math.PI * frequency * sampleIndex / waveFormat.SampleRate;
            var sample = (short)Math.Round(Math.Sin(angle) * amplitude);
            var sampleOffset = offset + i * bytesPerSample;
            buffer[sampleOffset] = (byte)(sample & 0xff);
            buffer[sampleOffset + 1] = (byte)((sample >> 8) & 0xff);
        }

        emittedSamples += samplesToWrite;
        return samplesToWrite * bytesPerSample;
    }
}
