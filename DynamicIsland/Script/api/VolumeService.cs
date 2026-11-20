using NAudio.CoreAudioApi;

namespace DynamicIsland;

/// <summary>Общая громкость Windows (устройство вывода по умолчанию) через Core Audio (пакет NAudio).</summary>
public sealed class VolumeService : IDisposable
{
    private MMDeviceEnumerator? _enumerator;

    public bool Available { get; private set; } = true;

    private MMDevice Device()
    {
        _enumerator ??= new MMDeviceEnumerator();
        // устройство по умолчанию могло смениться (наушники, Bluetooth) — берём актуальное каждый раз
        return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    /// <summary>Уровень 0..1 и признак «без звука».</summary>
    public (float Level, bool Muted) Read()
    {
        try
        {
            using var d = Device();
            var v = d.AudioEndpointVolume;
            Available = true;
            return (v.MasterVolumeLevelScalar, v.Mute);
        }
        catch (Exception ex)
        {
            Available = false;
            AppLog.Error("VolumeService.Read", ex);
            return (0, false);
        }
    }

    public void SetLevel(float level)
    {
        try
        {
            using var d = Device();
            var v = d.AudioEndpointVolume;
            v.MasterVolumeLevelScalar = Math.Clamp(level, 0f, 1f);
            if (level > 0 && v.Mute) v.Mute = false; // сдвинули ползунок — звук включается
        }
        catch (Exception ex) { AppLog.Error("VolumeService.SetLevel", ex); }
    }

    public void ToggleMute()
    {
        try
        {
            using var d = Device();
            d.AudioEndpointVolume.Mute = !d.AudioEndpointVolume.Mute;
        }
        catch (Exception ex) { AppLog.Error("VolumeService.ToggleMute", ex); }
    }

    public void Dispose() => _enumerator?.Dispose();
}
