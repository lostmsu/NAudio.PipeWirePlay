using System.Diagnostics;
using NAudio.Wave;

namespace NAudio.PipeWirePlay;

static class PwPlayCommand
{
    public static string? Resolve(string commandName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, commandName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static ProcessStartInfo CreateStartInfo(PwPlayOptions options, WaveFormat format)
    {
        var binaryPath = options.BinaryPath ?? Resolve("pw-play")
            ?? throw new PlatformNotSupportedException("pw-play was not found in PATH.");

        var startInfo = new ProcessStartInfo
        {
            FileName = binaryPath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.Arguments = BuildArguments(options, format);
        return startInfo;
    }

    public static string BuildArguments(PwPlayOptions options, WaveFormat format)
    {
        var arguments = new List<string>
        {
            "--raw",
            "-",
            "--rate", format.SampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--channels", format.Channels.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--format", MapFormat(format),
            "--volume", options.Volume.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "--media-type", Quote(options.MediaType),
            "--media-category", Quote(options.MediaCategory),
            "--media-role", Quote(options.MediaRole),
        };

        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            arguments.Add("--target");
            arguments.Add(Quote(options.Target!));
        }

        if (!string.IsNullOrWhiteSpace(options.Latency))
        {
            arguments.Add("--latency");
            arguments.Add(Quote(options.Latency!));
        }

        return string.Join(" ", arguments);
    }

    public static string MapFormat(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return format.BitsPerSample switch
            {
                32 => "f32",
                64 => "f64",
                _ => throw new NotSupportedException($"Unsupported IEEE float width: {format.BitsPerSample}"),
            };
        }

        if (format.Encoding == WaveFormatEncoding.Pcm)
        {
            return format.BitsPerSample switch
            {
                8 => "u8",
                16 => "s16",
                32 => "s32",
                _ => throw new NotSupportedException($"Unsupported PCM bit depth: {format.BitsPerSample}"),
            };
        }

        throw new NotSupportedException(
            $"Unsupported wave format. Encoding={format.Encoding}, BitsPerSample={format.BitsPerSample}");
    }

    static string Quote(string value)
        => value.IndexOfAny([' ', '\t', '"']) >= 0
            ? "\"" + value.Replace("\"", "\\\"") + "\""
            : value;
}
