using NAudio.Wave;

namespace AudioPlayer;

internal sealed class SpeedSampleProvider(ISampleProvider source) : ISampleProvider
{
    private readonly object _gate = new();
    private readonly float[] _sourceBuffer = new float[Math.Max(4096, source.WaveFormat.Channels * 2048)];
    private float[] _frameA = new float[source.WaveFormat.Channels];
    private float[] _frameB = new float[source.WaveFormat.Channels];
    private double _position;
    private bool _hasA;
    private bool _hasB;
    private float _speed = 1f;

    public WaveFormat WaveFormat => source.WaveFormat;

    public float Speed
    {
        get
        {
            lock (_gate)
            {
                return _speed;
            }
        }
        set
        {
            lock (_gate)
            {
                _speed = Math.Clamp(value, 0.5f, 2f);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            var channels = WaveFormat.Channels;
            var requestedFrames = count / channels;
            var writtenFrames = 0;

            EnsureFrames();
            while (writtenFrames < requestedFrames && _hasA && _hasB)
            {
                var blend = (float)_position;
                for (var channel = 0; channel < channels; channel++)
                {
                    buffer[offset + writtenFrames * channels + channel] = _frameA[channel] + (_frameB[channel] - _frameA[channel]) * blend;
                }

                writtenFrames++;
                _position += _speed;

                while (_position >= 1d && _hasB)
                {
                    (_frameA, _frameB) = (_frameB, _frameA);
                    _hasA = true;
                    _hasB = ReadFrame(_frameB);
                    _position -= 1d;
                }
            }

            return writtenFrames * channels;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _position = 0;
            _hasA = false;
            _hasB = false;
        }
    }

    private void EnsureFrames()
    {
        if (!_hasA)
        {
            _hasA = ReadFrame(_frameA);
        }

        if (!_hasB)
        {
            _hasB = ReadFrame(_frameB);
        }
    }

    private bool ReadFrame(float[] frame)
    {
        var channels = WaveFormat.Channels;
        var read = 0;
        while (read < channels)
        {
            var count = source.Read(_sourceBuffer, read, channels - read);
            if (count <= 0)
            {
                return false;
            }

            Array.Copy(_sourceBuffer, read, frame, read, count);
            read += count;
        }

        return true;
    }
}
