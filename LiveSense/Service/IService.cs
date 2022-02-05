namespace LiveSense.Service;

public enum ServiceStatus
{
    Disconnected,
    Disconnecting,
    Connecting,
    Connected
}

public interface IService
{
    string Name { get; }
    ServiceStatus Status { get; }
    bool ContentVisible { get; set; }

    Task ToggleConnectAsync();
}
