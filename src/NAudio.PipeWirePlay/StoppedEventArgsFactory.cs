using System.Reflection;
using NAudio.Wave;

namespace NAudio.PipeWirePlay;

static class StoppedEventArgsFactory
{
    static readonly ConstructorInfo? ExceptionConstructor =
        typeof(StoppedEventArgs).GetConstructor([typeof(Exception)]);

    static readonly ConstructorInfo? ParameterlessConstructor =
        typeof(StoppedEventArgs).GetConstructor(Type.EmptyTypes);

    public static StoppedEventArgs? Create(Exception? exception)
    {
        if (exception is not null && ExceptionConstructor is not null)
        {
            return (StoppedEventArgs?)ExceptionConstructor.Invoke([exception]);
        }

        if (ParameterlessConstructor is not null)
        {
            return (StoppedEventArgs?)ParameterlessConstructor.Invoke(Array.Empty<object>());
        }

        return null;
    }
}
