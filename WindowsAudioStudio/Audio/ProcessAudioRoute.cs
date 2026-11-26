using NAudio.CoreAudioApi;
using NAudio.Wave;
using WindowsAudioStudio.Models;

namespace WindowsAudioStudio.Audio;

/// <summary>
/// Captures one process tree with WASAPI process loopback, applies DSP, and renders the result
/// through the default output endpoint. The source session is muted only while this route is active.
/// </summary>
internal sealed class ProcessAudioRoute : IAsyncDisposable
{
    private readonly AudioSessionItem _session;
    private readonly CancellationTokenSource _stop = new();
    private readonly WaveFormat _format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    private readonly BufferedWaveProvider _buffer;
    private readonly AudioDspWaveProvider _processor;
    private WasapiRecorder? _capture;
    private WasapiPlayer? _player;
    private Task? _captureTask;
    private bool _started;
    private int _disposed;

    public ProcessAudioRoute(AudioSessionItem session)
    {
        _session = session;
        _buffer = new BufferedWaveProvider(_format)
        {
            BufferDuration = TimeSpan.FromMilliseconds(500),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };
        _processor = new AudioDspWaveProvider(_buffer, session.Dsp,
            () => session.IsMuted ? 0f : session.Volume);
    }

    public async Task StartAsync()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            throw new PlatformNotSupportedException("Захват отдельного приложения требует Windows 10 версии 2004 или новее.");

        try
        {
            _capture = await new WasapiRecorderBuilder()
                .WithProcessLoopback((uint)_session.ProcessId, ProcessLoopbackMode.IncludeTargetProcessTree)
                .WithFormat(_format)
                .WithBufferLength(50)
                .WithMmcssThreadPriority("Pro Audio")
                .BuildAsync();

            _player = new WasapiPlayerBuilder()
                .WithLatency(60)
                .WithMmcssThreadPriority("Pro Audio")
                .Build();

            _player.Init(_processor);

            WasapiRecorder recorder = _capture;
            _captureTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var packet in recorder.CaptureAsync(_stop.Token).ConfigureAwait(false))
                    {
                        byte[] data = packet.Data.ToArray();
                        if (data.Length > 0) _buffer.AddSamples(data, 0, data.Length);
                    }
                }
                catch (OperationCanceledException) { }
            });

            // Give the capture stream time to start and fill its first packets before switching
            // from the application's original session to the processed return stream.
            await Task.Delay(90);
            _session.SetProcessing(true);
            _player.Play();
            _started = true;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();

        try
        {
            if (_captureTask is not null)
                await _captureTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch { }

        try { _player?.Stop(); } catch { }
        _player?.Dispose();
        _player = null;

        _capture?.Dispose();
        _capture = null;

        if (_started || _session.IsProcessing)
            _session.SetProcessing(false);

        _stop.Dispose();
    }
}