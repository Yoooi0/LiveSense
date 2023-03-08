using LiveSense.Common;
using LiveSense.Common.Messages;
using LiveSense.Service;
using Stylet;

namespace LiveSense.ViewModels;

public class ServiceViewModel : Conductor<IService>.Collection.OneActive, IHandle<AppSettingsMessage>, IDisposable
{
    public ServiceViewModel(IEventAggregator eventAggregator, IEnumerable<IService> services)
    {
        eventAggregator.Subscribe(this);
        Items.AddRange(services);
    }

    public void Handle(AppSettingsMessage message)
    {
        if (message.Type == AppSettingsMessageType.Saving)
        {
            if (!message.Settings.EnsureContainsObjects("Service")
             || !message.Settings.TryGetObject(out var settings, "Service"))
                return;

            if (ActiveItem != null)
                settings[nameof(ActiveItem)] = ActiveItem.Name;
        }
        else if (message.Type == AppSettingsMessageType.Loading)
        {
            if (!message.Settings.TryGetObject(out var settings, "Service"))
                return;

            if (settings.TryGetValue(nameof(ActiveItem), out var selectedItemToken))
                ChangeActiveItem(Items.FirstOrDefault(x => string.Equals(x.Name, selectedItemToken.ToObject<string>())) ?? Items[0], closePrevious: false);
        }
    }

    protected override void ChangeActiveItem(IService newItem, bool closePrevious)
    {
        if (ActiveItem != null && newItem != null)
        {
            newItem.ContentVisible = ActiveItem.ContentVisible;
            ActiveItem.ContentVisible = false;
        }

        base.ChangeActiveItem(newItem, closePrevious);
    }

    protected virtual void Dispose(bool disposing) { }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
