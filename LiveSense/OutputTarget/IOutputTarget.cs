namespace LiveSense.OutputTarget;

public enum OutputTargetStatus
{
    Disconnected,
    Disconnecting,
    Connecting,
    Connected
}

public interface IOutputTarget
{
    string Name { get; }
    OutputTargetStatus Status { get; }
    bool ContentVisible { get; set; }

    Task ToggleConnectAsync();
}
