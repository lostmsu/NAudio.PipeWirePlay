using System.Diagnostics;

namespace NAudio.PipeWirePlay;

static class ProcessCompat
{
    public static Task WaitForExitCompatAsync(this Process process, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        void HandleExited(object? sender, EventArgs e) => completion.TrySetResult(null);

        process.EnableRaisingEvents = true;
        process.Exited += HandleExited;

        if (process.HasExited)
        {
            process.Exited -= HandleExited;
            return Task.CompletedTask;
        }

        if (!cancellationToken.CanBeCanceled)
        {
            return AwaitAndDetachAsync();
        }

        var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return AwaitAndDetachAsync(registration);

        async Task AwaitAndDetachAsync(CancellationTokenRegistration registration = default)
        {
            try
            {
                await completion.Task.ConfigureAwait(false);
            }
            finally
            {
                registration.Dispose();
                process.Exited -= HandleExited;
            }
        }
    }
}
