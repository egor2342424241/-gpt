using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WindowsAudioStudio.Audio;

namespace WindowsAudioStudio;

public partial class MainWindow : Window
{
    private readonly AudioSessionService _audio = new();
    private readonly DispatcherTimer _timer = new();
    private readonly List<Slider> _eqSliders = new();
    private static readonly int[] Frequencies = { 31, 44, 63, 88, 125, 175, 250, 350, 500, 700, 1000, 1400, 2000, 2800, 4000, 5600, 8000, 11000, 16000, 20000 };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _audio;
        BuildEqualizer();
        _audio.Refresh();
        DeviceLabel.Text = _audio.OutputDeviceName;
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => _audio.Refresh();
        _timer.Start();
    }

    private void BuildEqualizer()
    {
        EqGrid.Rows = 1;
        EqGrid.Columns = Frequencies.Length;
        foreach (var hz in Frequencies)
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var value = new TextBlock { Text = "0.0", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
            var slider = new Slider { Minimum = -12, Maximum = 12, Value = 0, Orientation = Orientation.Vertical, Height = 190, Width = 24, TickFrequency = 1, ToolTip = $"{hz} Hz" };
            slider.ValueChanged += (_, _) => value.Text = slider.Value.ToString("0.0");
            var label = new TextBlock { Text = hz >= 1000 ? $"{hz / 1000.0:0.#}k" : hz.ToString(), FontSize = 9, Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"), HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(value); panel.Children.Add(slider); panel.Children.Add(label);
            EqGrid.Children.Add(panel); _eqSliders.Add(slider);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _audio.Refresh();
        DeviceLabel.Text = _audio.OutputDeviceName;
    }

    private void ResetEq_Click(object sender, RoutedEventArgs e)
    {
        foreach (var slider in _eqSliders) slider.Value = 0;
        PresetCombo.SelectedIndex = 0;
    }

    private void SessionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionsList.SelectedItem is Models.AudioSessionItem item)
            SelectedSessionLabel.Text = $"{item.DisplayName} · PID {item.ProcessId}";
        else
            SelectedSessionLabel.Text = "Глобальная панель настроек · EQ пока демонстрационный";
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_eqSliders.Count == 0 || PresetCombo.SelectedItem is not ComboBoxItem item) return;
        var name = item.Content?.ToString() ?? "Flat";
        double[] values = name switch
        {
            "Bass Boost" => new double[] { 5,6,6,5,4,3,2,1,0,0,0,0,0,0,0,0,0,0,0,0 },
            "Vocal" => new double[] { -2,-2,-1,0,1,2,3,4,4,3,2,2,1,0,0,0,0,0,0,0 },
            "Rock" => new double[] { 4,3,2,1,0,-1,-1,0,2,3,4,4,3,2,1,1,2,2,2,1 },
            "Electronic" => new double[] { 5,4,3,1,0,-1,-2,-1,1,2,2,1,0,1,3,4,4,3,2,1 },
            _ => new double[20]
        };
        for (int i = 0; i < _eqSliders.Count; i++) _eqSliders[i].Value = values[i];
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _audio.Dispose();
        base.OnClosed(e);
    }
}