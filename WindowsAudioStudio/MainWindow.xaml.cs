using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsAudioStudio.Audio;
using WindowsAudioStudio.Models;

namespace WindowsAudioStudio;

public partial class MainWindow : Window
{
    private readonly AudioSessionService _audio = new();
    private readonly AudioRouteManager _routes = new();
    private readonly DispatcherTimer _timer = new();
    private readonly List<Slider> _eqSliders = new();
    private AudioSessionItem? _selected;
    private bool _updatingControls;
    private bool _suppressProcessingEvents;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _audio;
        BuildEqualizer();
        _audio.Sessions.CollectionChanged += Sessions_CollectionChanged;
        _audio.Refresh();
        DeviceLabel.Text = _audio.OutputDeviceName;

        _timer.Interval = TimeSpan.FromMilliseconds(700);
        _timer.Tick += (_, _) =>
        {
            _audio.Refresh();
            DeviceLabel.Text = _audio.OutputDeviceName;
        };
        _timer.Start();

        if (SessionsList.Items.Count > 0)
            SessionsList.SelectedIndex = 0;
    }

    private void BuildEqualizer()
    {
        EqGrid.Columns = DspSettings.Frequencies.Length;
        for (int index = 0; index < DspSettings.Frequencies.Length; index++)
        {
            int frequency = DspSettings.Frequencies[index];
            var panel = new StackPanel { Width = 39, HorizontalAlignment = HorizontalAlignment.Center };
            var value = new TextBlock
            {
                Text = "0.0", FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            var slider = new Slider
            {
                Minimum = -12, Maximum = 12, Value = 0, Orientation = Orientation.Vertical,
                Height = 175, Width = 24, TickFrequency = 1, ToolTip = $"{frequency} Hz", Tag = index
            };
            slider.ValueChanged += (_, _) =>
            {
                value.Text = slider.Value.ToString("0.0");
                if (!_updatingControls && _selected is not null)
                    _selected.Dsp.SetBandGain(index, (float)slider.Value);
            };

            string label = frequency >= 1000 ? $"{frequency / 1000.0:0.#}k" : frequency.ToString();
            var frequencyLabel = new TextBlock
            {
                Text = label, FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DimGray
            };
            panel.Children.Add(value);
            panel.Children.Add(slider);
            panel.Children.Add(frequencyLabel);
            EqGrid.Children.Add(panel);
            _eqSliders.Add(slider);
        }
    }

    private void Sessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (AudioSessionItem item in e.OldItems)
            {
                item.PropertyChanged -= Session_PropertyChanged;
                _ = _routes.DisableAsync(item.ProcessId);
            }
        }

        if (e.NewItems is not null)
            foreach (AudioSessionItem item in e.NewItems)
                item.PropertyChanged += Session_PropertyChanged;

        if (_selected is not null && !_audio.Sessions.Contains(_selected))
        {
            _selected = null;
            SelectedSessionLabel.Text = "Выбери приложение слева";
            UpdateSelectedControls();
        }
    }

    private void Session_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AudioSessionItem.IsProcessing) || _suppressProcessingEvents ||
            sender is not AudioSessionItem item)
            return;
        _ = ApplyProcessingAsync(item);
    }

    private async Task ApplyProcessingAsync(AudioSessionItem item)
    {
        try
        {
            if (item.IsProcessing)
            {
                await _routes.EnableAsync(item);
                StatusLabel.Text = $"DSP включён для {item.DisplayName}.";
            }
            else
            {
                await _routes.DisableAsync(item.ProcessId);
                StatusLabel.Text = $"DSP выключен для {item.DisplayName}. Исходная громкость восстановлена.";
            }
        }
        catch (Exception ex)
        {
            _suppressProcessingEvents = true;
            item.IsProcessing = false;
            _suppressProcessingEvents = false;
            await _routes.DisableAsync(item.ProcessId);
            StatusLabel.Text = $"Не удалось запустить DSP: {ex.Message}";
        }
    }

    private void SessionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = SessionsList.SelectedItem as AudioSessionItem;
        SelectedSessionLabel.Text = _selected is null
            ? "Выбери приложение слева"
            : $"{_selected.DisplayName} · PID {_selected.ProcessId}";
        UpdateSelectedControls();
    }

    private void UpdateSelectedControls()
    {
        _updatingControls = true;
        try
        {
            var snapshot = _selected?.Dsp.Snapshot();
            for (int i = 0; i < _eqSliders.Count; i++)
                _eqSliders[i].Value = snapshot?.BandGains[i] ?? 0f;

            BassBoostBox.IsChecked = snapshot?.BassBoost ?? false;
            StereoWidthBox.IsChecked = snapshot?.StereoWidth ?? false;
            ReverbBox.IsChecked = snapshot?.Reverb ?? false;
            CompressorBox.IsChecked = snapshot?.Compressor ?? false;
            LimiterBox.IsChecked = snapshot?.Limiter ?? false;
        }
        finally { _updatingControls = false; }
    }

    private void EffectChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingControls || _selected is null || sender is not CheckBox box ||
            box.Tag is not string tag || !Enum.TryParse<DspEffect>(tag, out var effect))
            return;
        _selected.Dsp.SetEffect(effect, box.IsChecked == true);
    }

    private void ResetEq_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        for (int i = 0; i < _eqSliders.Count; i++)
        {
            _selected.Dsp.SetBandGain(i, 0f);
            _eqSliders[i].Value = 0;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _audio.Refresh();
        DeviceLabel.Text = _audio.OutputDeviceName;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        try { _routes.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        _audio.Dispose();
        base.OnClosed(e);
    }
}