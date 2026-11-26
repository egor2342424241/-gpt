using System.Runtime.InteropServices;
using NAudio.Wave;
using WindowsAudioStudio.Models;

namespace WindowsAudioStudio.Audio;

internal sealed class AudioDspWaveProvider : IWaveProvider
{
    private readonly BufferedWaveProvider _input;
    private readonly DspSettings _settings;
    private readonly Func<float> _volume;
    private readonly BiquadFilter[] _leftEq = Enumerable.Range(0, DspSettings.Frequencies.Length).Select(_ => new BiquadFilter()).ToArray();
    private readonly BiquadFilter[] _rightEq = Enumerable.Range(0, DspSettings.Frequencies.Length).Select(_ => new BiquadFilter()).ToArray();
    private readonly BiquadFilter _bassLeft = new();
    private readonly BiquadFilter _bassRight = new();
    private readonly float[] _delayLeft = new float[8192];
    private readonly float[] _delayRight = new float[8192];
    private readonly int _sampleRate;
    private int _snapshotVersion = -1;
    private int _delayPosition;
    private float _envelope;

    public WaveFormat WaveFormat => _input.WaveFormat;

    public AudioDspWaveProvider(BufferedWaveProvider input, DspSettings settings, Func<float> volume)
    {
        _input = input;
        _settings = settings;
        _volume = volume;
        _sampleRate = input.WaveFormat.SampleRate;
    }

    public int Read(Span<byte> buffer)
    {
        int read = _input.Read(buffer);
        int aligned = read - read % sizeof(float);
        if (aligned <= 0) return read;

        DspSnapshot snapshot = _settings.Snapshot();
        if (snapshot.Version != _snapshotVersion)
        {
            for (int i = 0; i < snapshot.BandGains.Length; i++)
            {
                float frequency = DspSettings.Frequencies[i];
                _leftEq[i].SetPeaking(_sampleRate, frequency, 1.05f, snapshot.BandGains[i]);
                _rightEq[i].SetPeaking(_sampleRate, frequency, 1.05f, snapshot.BandGains[i]);
            }

            _bassLeft.SetPeaking(_sampleRate, 95f, 0.75f, snapshot.BassBoost ? 5.5f : 0f);
            _bassRight.SetPeaking(_sampleRate, 95f, 0.75f, snapshot.BassBoost ? 5.5f : 0f);
            _snapshotVersion = snapshot.Version;
        }

        Span<float> samples = MemoryMarshal.Cast<byte, float>(buffer[..aligned]);
        float outputGain = Math.Clamp(_volume(), 0f, 1f);
        float attack = MathF.Exp(-1f / (_sampleRate * 0.012f));
        float release = MathF.Exp(-1f / (_sampleRate * 0.160f));
        float wet = snapshot.Reverb ? 0.13f : 0f;
        float width = snapshot.StereoWidth ? 1.35f : 1f;

        for (int i = 0; i + 1 < samples.Length; i += 2)
        {
            float left = samples[i];
            float right = samples[i + 1];

            if (snapshot.BassBoost)
            {
                left = _bassLeft.Process(left);
                right = _bassRight.Process(right);
            }

            for (int band = 0; band < _leftEq.Length; band++)
            {
                left = _leftEq[band].Process(left);
                right = _rightEq[band].Process(right);
            }

            if (snapshot.StereoWidth)
            {
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f * width;
                left = mid + side;
                right = mid - side;
            }

            if (wet > 0f)
            {
                float delayedLeft = _delayLeft[_delayPosition];
                float delayedRight = _delayRight[_delayPosition];
                _delayLeft[_delayPosition] = left + delayedRight * 0.24f;
                _delayRight[_delayPosition] = right + delayedLeft * 0.24f;
                left += delayedLeft * wet;
                right += delayedRight * wet;
                _delayPosition++;
                if (_delayPosition >= _delayLeft.Length) _delayPosition = 0;
            }

            if (snapshot.Compressor)
            {
                float level = MathF.Max(MathF.Abs(left), MathF.Abs(right));
                float coefficient = level > _envelope ? attack : release;
                _envelope = coefficient * _envelope + (1f - coefficient) * level;
                const float threshold = 0.22f;
                const float ratio = 3.0f;
                if (_envelope > threshold)
                {
                    float compressed = threshold + (_envelope - threshold) / ratio;
                    float gain = compressed / MathF.Max(_envelope, 0.0001f);
                    left *= gain;
                    right *= gain;
                }
            }

            if (snapshot.Limiter)
            {
                left = MathF.Tanh(left * 1.35f);
                right = MathF.Tanh(right * 1.35f);
            }

            samples[i] = Math.Clamp(left * outputGain, -1f, 1f);
            samples[i + 1] = Math.Clamp(right * outputGain, -1f, 1f);
        }

        return read;
    }
}