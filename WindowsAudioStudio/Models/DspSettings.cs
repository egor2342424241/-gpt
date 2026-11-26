namespace WindowsAudioStudio.Models;

public enum DspEffect
{
    BassBoost,
    StereoWidth,
    Reverb,
    Compressor,
    Limiter
}

public sealed record DspSnapshot(
    int Version,
    float[] BandGains,
    bool BassBoost,
    bool StereoWidth,
    bool Reverb,
    bool Compressor,
    bool Limiter);

public sealed class DspSettings
{
    public static readonly int[] Frequencies =
        { 31, 44, 63, 88, 125, 175, 250, 350, 500, 700, 1000, 1400, 2000, 2800, 4000, 5600, 8000, 11000, 16000, 20000 };

    private readonly object _sync = new();
    private readonly float[] _bands = new float[Frequencies.Length];
    private int _version;
    private bool _bassBoost;
    private bool _stereoWidth;
    private bool _reverb;
    private bool _compressor;
    private bool _limiter;

    public void SetBandGain(int index, float gainDb)
    {
        if ((uint)index >= (uint)_bands.Length) throw new ArgumentOutOfRangeException(nameof(index));
        lock (_sync)
        {
            _bands[index] = Math.Clamp(gainDb, -12f, 12f);
            _version++;
        }
    }

    public float GetBandGain(int index)
    {
        lock (_sync) return _bands[index];
    }

    public void SetEffect(DspEffect effect, bool enabled)
    {
        lock (_sync)
        {
            switch (effect)
            {
                case DspEffect.BassBoost: _bassBoost = enabled; break;
                case DspEffect.StereoWidth: _stereoWidth = enabled; break;
                case DspEffect.Reverb: _reverb = enabled; break;
                case DspEffect.Compressor: _compressor = enabled; break;
                case DspEffect.Limiter: _limiter = enabled; break;
                default: throw new ArgumentOutOfRangeException(nameof(effect));
            }
            _version++;
        }
    }

    public DspSnapshot Snapshot()
    {
        lock (_sync)
            return new DspSnapshot(_version, (float[])_bands.Clone(), _bassBoost, _stereoWidth, _reverb, _compressor, _limiter);
    }
}