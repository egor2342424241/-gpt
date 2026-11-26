using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NAudio.CoreAudioApi;

namespace WindowsAudioStudio.Models;

public sealed class AudioSessionItem : INotifyPropertyChanged
{
    private readonly AudioSessionControl _session;
    private float _volume;
    private bool _isMuted;
    private float _peak;

    public AudioSessionItem(AudioSessionControl session)
    {
        _session = session;
        try
        {
            ProcessId = session.GetProcessID;
            try { ProcessName = Process.GetProcessById(ProcessId).ProcessName; }
            catch { ProcessName = ProcessId == 0 ? "Системный звук" : "Неизвестный процесс"; }
            DisplayName = string.IsNullOrWhiteSpace(session.DisplayName) ? ProcessName : session.DisplayName;
            _volume = session.SimpleAudioVolume.Volume;
            _isMuted = session.SimpleAudioVolume.Mute;
            _peak = session.AudioMeterInformation.MasterPeakValue;
        }
        catch
        {
            ProcessId = 0;
            ProcessName = "Неизвестный процесс";
            DisplayName = "Аудиосессия";
        }
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public string DisplayName { get; }
    public string Key => $"{ProcessId}:{DisplayName}";

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            try { _session.SimpleAudioVolume.Volume = _volume; } catch { }
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercent));
        }
    }

    public int VolumePercent => (int)Math.Round(Volume * 100);

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            try { _session.SimpleAudioVolume.Mute = value; } catch { }
            OnPropertyChanged();
        }
    }

    public float Peak
    {
        get => _peak;
        private set { _peak = value; OnPropertyChanged(); }
    }

    public void RefreshMeter()
    {
        try { Peak = _session.AudioMeterInformation.MasterPeakValue; } catch { Peak = 0; }
        try
        {
            var current = _session.SimpleAudioVolume.Volume;
            if (Math.Abs(current - _volume) > 0.01f)
            {
                _volume = current;
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumePercent));
            }
            var muted = _session.SimpleAudioVolume.Mute;
            if (muted != _isMuted) { _isMuted = muted; OnPropertyChanged(nameof(IsMuted)); }
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}