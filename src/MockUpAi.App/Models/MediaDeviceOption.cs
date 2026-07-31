namespace MockUpAi.App.Models;

public sealed class MediaDeviceOption
{
    public int DeviceIndex { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public override string ToString()
    {
        return DisplayName;
    }
}
