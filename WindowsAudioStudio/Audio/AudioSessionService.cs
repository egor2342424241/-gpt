using System.Collections.ObjectModel;
using NAudio.CoreAudioApi;
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
        catch
        {
            _device = null;
            _manager = null;
        }
    }

    public void Refresh()
    {
        if (_manager is null) ConnectDefaultDevice();
        if (_manager is null) return;

        try
        {
            var groups = new Dictionary<int, List<AudioSessionControl>>();
            var current = _manager.Sessions;

            for (int i = 0; i < current.Count; i++)
            {
                try
                {
                    AudioSessionControl control = current[i];
                    int processId = checked((int)control.GetProcessID);
                    if (processId <= 0 || processId == Environment.ProcessId) continue;

                    if (!groups.TryGetValue(processId, out var controls))
                        groups[processId] = controls = new List<AudioSessionControl>();

                    controls.Add(control);
                }
                catch { }
            }

            var existing = Sessions.ToDictionary(item => item.ProcessId);
            var seen = new HashSet<int>();

            foreach (var pair in groups)
            {
                seen.Add(pair.Key);
                if (existing.TryGetValue(pair.Key, out var item))
                {
                    item.UpdateControls(pair.Value);
                    item.RefreshMeter();
                }
                else
                {
                    Sessions.Add(new AudioSessionItem(pair.Key, pair.Value));
                }
            }

            for (int i = Sessions.Count - 1; i >= 0; i--)
                if (!seen.Contains(Sessions[i].ProcessId))
                    Sessions.RemoveAt(i);
        }
        catch { }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _enumerator.Dispose();
    }
}