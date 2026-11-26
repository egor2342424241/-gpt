using System.Collections.ObjectModel;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using WindowsAudioStudio.Models;

namespace WindowsAudioStudio.Audio;

public sealed class AudioSessionService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _device;
    private AudioSessionManager? _manager;
    public ObservableCollection<AudioSessionItem> Sessions { get; } = new();
    public string OutputDeviceName => _device?.FriendlyName ?? "Устройство вывода не найдено";

    public AudioSessionService() => ConnectDefaultDevice();

    private void ConnectDefaultDevice()
    {
        try
        {
            _device?.Dispose();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _manager = _device.AudioSessionManager;
        }
        catch { _device = null; _manager = null; }
    }

    public void Refresh()
    {
        if (_manager is null) ConnectDefaultDevice();
        if (_manager is null) return;
        try
        {
            var current = _manager.Sessions;
            var found = new List<AudioSessionItem>();
            for (int i = 0; i < current.Count; i++)
            {
                try
                {
                    var control = current[i];
                    if (control.State == AudioSessionState.AudioSessionStateExpired) continue;
                    found.Add(new AudioSessionItem(control));
                }
                catch { }
            }
            var existing = Sessions.ToDictionary(s => s.Key, s => s);
            var refreshed = new List<AudioSessionItem>();
            foreach (var item in found)
            {
                if (existing.TryGetValue(item.Key, out var old)) { old.RefreshMeter(); refreshed.Add(old); }
                else refreshed.Add(item);
            }
            for (int i = Sessions.Count - 1; i >= 0; i--)
                if (!refreshed.Any(x => x.Key == Sessions[i].Key)) Sessions.RemoveAt(i);
            foreach (var item in refreshed)
                if (!Sessions.Any(x => x.Key == item.Key)) Sessions.Add(item);
        }
        catch { }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator.Dispose();
    }
}