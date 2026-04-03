using System.Diagnostics;
using NAudio.Wave;

namespace NAudio.PipeWirePlay;

public sealed class PwPlayWavePlayer : IWavePlayer, IDisposable
{
    readonly object sync = new();
    readonly PwPlayOptions options;

    IWaveProvider? waveProvider;
    Process? process;
    CancellationTokenSource? playbackCancellation;
    Task? playbackTask;
    PlaybackState playbackState;
    long bytesWritten;
    bool disposed;

    public PwPlayWavePlayer()
        : this(new PwPlayOptions())
    {
    }

    public PwPlayWavePlayer(PwPlayOptions options)
        => this.options = options ?? throw new ArgumentNullException(nameof(options));

    public PlaybackState PlaybackState => playbackState;

    public WaveFormat OutputWaveFormat => waveProvider?.WaveFormat
        ?? throw new InvalidOperationException("Init must be called before OutputWaveFormat is available.");

    public float Volume
    {
        get => options.Volume;
        set
        {
            var next = Clamp(value, 0f, 1f);
            if (Math.Abs(options.Volume - next) < 0.0001f)
            {
                return;
            }

            options.Volume = next;
            if (PlaybackState == PlaybackState.Playing)
            {
                throw new NotSupportedException("Changing volume during active pw-play playback is not supported.");
            }
        }
    }

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public void Init(IWaveProvider waveProvider)
        => this.waveProvider = waveProvider ?? throw new ArgumentNullException(nameof(waveProvider));

    public void Play()
    {
        EnsureNotDisposed();
        if (waveProvider is null)
        {
            throw new InvalidOperationException("Init must be called before Play.");
        }

        lock (sync)
        {
            if (playbackState == PlaybackState.Playing)
            {
                return;
            }

            StopInternal(waitForCompletion: false);

            var startInfo = PwPlayCommand.CreateStartInfo(options, waveProvider.WaveFormat);
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start pw-play.");
            playbackCancellation = new CancellationTokenSource();
            playbackState = PlaybackState.Playing;
            bytesWritten = 0;
            playbackTask = Task.Run(() => PumpAudioAsync(process, waveProvider, playbackCancellation.Token), CancellationToken.None);
        }
    }

    public void Pause()
        => Stop();

    public void Stop()
    {
        EnsureNotDisposed();
        lock (sync)
        {
            StopInternal(waitForCompletion: true);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        lock (sync)
        {
            StopInternal(waitForCompletion: true);
            disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    void StopInternal(bool waitForCompletion)
    {
        playbackCancellation?.Cancel();

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (waitForCompletion && playbackTask is not null)
        {
            try
            {
                playbackTask.GetAwaiter().GetResult();
            }
            catch
            {
            }
        }

        playbackCancellation?.Dispose();
        playbackCancellation = null;
        playbackTask = null;
        process?.Dispose();
        process = null;
        playbackState = PlaybackState.Stopped;
    }

    async Task PumpAudioAsync(Process activeProcess, IWaveProvider activeProvider, CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            var stream = activeProcess.StandardInput.BaseStream;
            var buffer = new byte[Math.Max(activeProvider.WaveFormat.AverageBytesPerSecond / 8, activeProvider.WaveFormat.BlockAlign * 256)];

            while (!cancellationToken.IsCancellationRequested)
            {
                var read = activeProvider.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                await stream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                bytesWritten += read;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            try
            {
                activeProcess.StandardInput.Close();
            }
            catch
            {
            }

            try
            {
                await activeProcess.WaitForExitCompatAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception error) when (failure is null)
            {
                failure = error;
            }

            lock (sync)
            {
                if (ReferenceEquals(process, activeProcess))
                {
                    playbackState = PlaybackState.Stopped;
                    process = null;
                    playbackCancellation?.Dispose();
                    playbackCancellation = null;
                    playbackTask = null;
                }
            }

            activeProcess.Dispose();
            PlaybackStopped?.Invoke(this, StoppedEventArgsFactory.Create(failure)!);
        }
    }

    void EnsureNotDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(PwPlayWavePlayer));
        }
    }

    static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
