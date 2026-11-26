using WindowsAudioStudio.Models;

namespace WindowsAudioStudio.Audio;

internal sealed class AudioRouteManager : IAsyncDisposable
{
    private readonly Dictionary<int, ProcessAudioRoute> _routes = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task EnableAsync(AudioSessionItem session)
    {
        await _gate.WaitAsync();
        try
        {
            if (_routes.ContainsKey(session.ProcessId)) return;
            var route = new ProcessAudioRoute(session);
            try
            {
                await route.StartAsync();
                _routes.Add(session.ProcessId, route);
            }
            catch
            {
                await route.DisposeAsync();
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task DisableAsync(int processId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_routes.Remove(processId, out var route))
                await route.DisposeAsync();
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var route in _routes.Values)
                await route.DisposeAsync();
            _routes.Clear();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}