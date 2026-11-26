using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NAudio.CoreAudioApi;

namespace WindowsAudioStudio.Models;

public sealed class AudioSessionItem : INotifyPropertyChanged
{
    private List<AudioSessionControl> _controls;
    private float _volume;
    private bool _isMuted;
    private float _peak;
    private bool _isProcessing;
    private bool _sourceMutedForProcessing;

    public AudioSessionItem(int processId, IEnumerable<AudioSessionControl> controls)
    {
        ProcessId = processId;
        _controls = controls.ToList();

        try { ProcessName = Process.GetProcessById(processId).ProcessName; }
        catch { ProcessName = "Завершившийся процесс"; }

        DisplayName = _controls.Select(c => c.DisplayName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? ProcessName;

        _volume = ReadVolume();
        _isMuted = _controls.Count > 0 && _controls.All(c => SafeReadMute(c));
        Dsp = new DspSettings();
    }

    public int ProcessId { get; }
    public string ProcessName { get; }
    public string DisplayName { get; }
    public string Key => ProcessId.ToString();
    public DspSettings Dsp { get; }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (!_isProcessing)
                foreach (var control in _controls) try { control.SimpleAudioVolume.Volume = _volume; } catch { }
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
            if (!_isProcessing)
                foreach (var control in _controls) try { control.SimpleAudioVolume.Mute = value; } catch { }
            OnPropertyChanged();
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { if (_isProcessing == value) return; _isProcessing = value; OnPropertyChanged(); }
    }

    public float Peak
    {
        get => _peak;
        private set { _peak = value; OnPropertyChanged(); }
    }

    public void UpdateControls(IEnumerable<AudioSessionControl> controls)
    {
        _controls = controls.ToList();
        if (_isProcessing)
        {
            foreach (var control in _controls) try { control.SimpleAudioVolume.Mute = true; } catch { }
        }
        else
        {
            _volume = ReadVolume();
            _isMuted = _controls.Count > 0 && _controls.All(SafeReadMute);
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumePercent));
            OnPropertyChanged(nameof(IsMuted));
        }
    }

    public void SetProcessing(bool enabled)
    {
        if (enabled && !_sourceMutedForProcessing)
        {
            foreach (var control in _controls)
                try { control.SimpleAudioVolume.Mute = true; } catch { }
            _sourceMutedForProcessing = true;
        }
        else if (!enabled && _sourceMutedForProcessing)
        {
            foreach (var control in _controls)
            {
                try
                {
                    control.SimpleAudioVolume.Volume = _volume;
                    control.SimpleAudioVolume.Mute = _isMuted;
                }
                catch { }
            }
            _sourceMutedForProcessing = false;
        }

        if (_isProcessing != enabled)
        {
            _isProcessing = enabled;
            OnPropertyChanged(nameof(IsProcessing));
        }
    }

    public void RefreshMeter()
    {
        float peak = 0f;
        foreach (var control in _controls)
        {
            try { peak = Math.Max(peak, control.AudioMeterInformation.MasterPeakValue); }
            catch { }
        }
        Peak = peak;

        if (!_isProcessing && _controls.Count > 0)
        {
            float current = ReadVolume();
            if (Math.Abs(current - _volume) > 0.01f)
            {
                _volume = current;
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumePercent));
            }

            bool muted = _controls.All(SafeReadMute);
            if (muted != _isMuted) { _isMuted = muted; OnPropertyChanged(nameof(IsMuted)); }
        }
    }

    private float ReadVolume()
    {
        if (_controls.Count == 0) return 1f;
        try { return _controls.Average(c => c.SimpleAudioVolume.Volume); }
        catch { return 1f; }
    }

    private static bool SafeReadMute(AudioSessionControl control)
    {
        try { return control.SimpleAudioVolume.Mute; }
        catch { return false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}