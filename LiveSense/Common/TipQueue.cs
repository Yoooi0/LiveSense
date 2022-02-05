using LiveSense.Service;
using PropertyChanged;
using Stylet;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LiveSense.Common;

public interface ITipQueue : IReadOnlyCollection<ServiceTip>, IDisposable
{
    public int Count { get; }

    void Clear();
    void Enqueue(ServiceTip tip);
    ServiceTip Peek(CancellationToken token);
    ServiceTip Dequeue(CancellationToken token);
}

[DoNotNotify]
public class TipQueue : ITipQueue, INotifyPropertyChanged, INotifyCollectionChanged
{
    private readonly BlockingConcurrentQueue<ServiceTip> _queue;

    public event PropertyChangedEventHandler PropertyChanged;
    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public int Count => _queue.Count;
    public IEnumerator<ServiceTip> GetEnumerator() => _queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _queue.GetEnumerator();

    public TipQueue()
    {
        _queue = new BlockingConcurrentQueue<ServiceTip>();
    }

    public virtual void Enqueue(ServiceTip item)
    {
        _queue.TryEnqueue(item, CancellationToken.None);
        OnCollectionChanged();
    }

    public virtual ServiceTip Peek(CancellationToken token)
    {
        if (_queue.TryPeek(out var item, token))
            return item;

        throw new InvalidOperationException();
    }

    public virtual ServiceTip Dequeue(CancellationToken token)
    {
        if (_queue.TryDequeue(out var item, token))
        {
            OnCollectionChanged();
            return item;
        }

        throw new InvalidOperationException();
    }

    public virtual void Clear()
    {
        _queue.Clear();
        OnCollectionChanged();
    }

    protected virtual void Dispose(bool disposing)
    {
        _queue.Dispose();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void OnCollectionChanged() => Execute.OnUIThreadSync(() =>
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
    });
}
