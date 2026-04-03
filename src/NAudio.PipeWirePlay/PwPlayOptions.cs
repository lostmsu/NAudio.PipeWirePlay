namespace NAudio.PipeWirePlay;

public sealed class PwPlayOptions
{
    public string? BinaryPath { get; set; }

    public string? Target { get; set; }

    public string? Latency { get; set; } = "40ms";

    public float Volume { get; set; } = 1f;

    public string MediaType { get; set; } = "Audio";

    public string MediaCategory { get; set; } = "Playback";

    public string MediaRole { get; set; } = "Music";
}
