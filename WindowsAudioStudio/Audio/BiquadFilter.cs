namespace WindowsAudioStudio.Audio;

internal sealed class BiquadFilter
{
    private float _b0 = 1f, _b1, _b2, _a1, _a2;
    private float _z1, _z2;

    public void SetPeaking(float sampleRate, float frequency, float q, float gainDb)
    {
        double a = Math.Pow(10, gainDb / 40.0);
        double omega = 2.0 * Math.PI * Math.Clamp(frequency, 10, sampleRate * 0.45f) / sampleRate;
        double sn = Math.Sin(omega), cs = Math.Cos(omega);
        double alpha = sn / (2.0 * q);
        double a0 = 1.0 + alpha / a;

        _b0 = (float)((1.0 + alpha * a) / a0);
        _b1 = (float)((-2.0 * cs) / a0);
        _b2 = (float)((1.0 - alpha * a) / a0);
        _a1 = (float)((-2.0 * cs) / a0);
        _a2 = (float)((1.0 - alpha / a) / a0);
    }

    public float Process(float input)
    {
        float output = _b0 * input + _z1;
        _z1 = _b1 * input - _a1 * output + _z2;
        _z2 = _b2 * input - _a2 * output;
        return float.IsFinite(output) ? output : 0f;
    }

    public void Reset() => (_z1, _z2) = (0f, 0f);
}