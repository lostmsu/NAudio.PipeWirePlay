namespace NAudio.PipeWirePlay;

public sealed class PipeWireNodeInfo
{
    public required int Id { get; init; }

    public required int Serial { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string MediaClass { get; init; }

    public required PipeWireNodeKind Kind { get; init; }
}
