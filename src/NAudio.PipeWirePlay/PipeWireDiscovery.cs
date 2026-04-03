using System.Diagnostics;
using System.Text.Json;

namespace NAudio.PipeWirePlay;

public static class PipeWireDiscovery
{
    public static bool IsAvailable()
        => PwPlayCommand.Resolve("pw-dump") is not null
            && PwPlayCommand.Resolve("pw-play") is not null;

    public static async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAvailable())
        {
            return false;
        }

        try
        {
            await ListPlaybackNodesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public static Task<IReadOnlyList<PipeWireNodeInfo>> ListPlaybackNodesAsync(CancellationToken cancellationToken = default)
        => ListNodesAsync(PipeWireNodeKind.Playback, cancellationToken);

    public static Task<IReadOnlyList<PipeWireNodeInfo>> ListCaptureNodesAsync(CancellationToken cancellationToken = default)
        => ListNodesAsync(PipeWireNodeKind.Capture, cancellationToken);

    public static async Task<IReadOnlyList<PipeWireNodeInfo>> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        var pwDumpPath = PwPlayCommand.Resolve("pw-dump")
            ?? throw new PlatformNotSupportedException("pw-dump was not found in PATH.");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = pwDumpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Failed to start pw-dump.");

        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitCompatAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"pw-dump failed with exit code {process.ExitCode}: {stderr.Trim()}");
        }

        using var document = JsonDocument.Parse(stdout);
        var nodes = new List<PipeWireNodeInfo>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!TryParseNode(element, out var node))
            {
                continue;
            }

            nodes.Add(node);
        }

        return nodes;
    }

    public static async Task<IReadOnlyList<PipeWireNodeInfo>> ListNodesAsync(
        PipeWireNodeKind kind,
        CancellationToken cancellationToken = default)
    {
        var nodes = await ListNodesAsync(cancellationToken).ConfigureAwait(false);
        return nodes.Where(static node => node.Kind != PipeWireNodeKind.Unknown)
            .Where(node => node.Kind == kind)
            .ToArray();
    }

    static bool TryParseNode(JsonElement element, out PipeWireNodeInfo node)
    {
        node = null!;
        if (!element.TryGetProperty("type", out var typeProperty)
            || typeProperty.ValueKind != JsonValueKind.String
            || !string.Equals(typeProperty.GetString(), "PipeWire:Interface:Node", StringComparison.Ordinal))
        {
            return false;
        }

        if (!element.TryGetProperty("id", out var idProperty) || idProperty.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (!element.TryGetProperty("info", out var infoProperty)
            || !infoProperty.TryGetProperty("props", out var propsProperty)
            || propsProperty.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var mediaClass = ReadString(propsProperty, "media.class");
        var kind = mediaClass switch
        {
            "Audio/Sink" => PipeWireNodeKind.Playback,
            "Audio/Source" => PipeWireNodeKind.Capture,
            _ => PipeWireNodeKind.Unknown,
        };

        if (kind == PipeWireNodeKind.Unknown)
        {
            return false;
        }

        var serial = ReadInt(propsProperty, "object.serial");
        var name = ReadString(propsProperty, "node.name");
        var description = ReadString(propsProperty, "node.description");
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        node = new PipeWireNodeInfo
        {
            Id = idProperty.GetInt32(),
            Serial = serial,
            Name = name ?? string.Empty,
            Description = string.IsNullOrWhiteSpace(description) ? (name ?? string.Empty) : (description ?? string.Empty),
            MediaClass = mediaClass ?? string.Empty,
            Kind = kind,
        };
        return true;
    }

    static string? ReadString(JsonElement props, string name)
        => props.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    static int ReadInt(JsonElement props, string name)
        => props.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : 0;
}
