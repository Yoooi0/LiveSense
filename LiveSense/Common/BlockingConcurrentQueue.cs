using System.Collections;
using System.Collections.Concurrent;

namespace LiveSense.Common;

public class BlockingConcurrentQueue<T> : IReadOnlyCollection<T>, IDisposable
{
    private readonly ConcurrentQueue<T> _queue;
    private readonly SemaphoreSlim _occupiedNodes;
    private volatile int _currentAdders;

    public int Count => _queue.Count;
    public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _queue.GetEnumerator();

    public BlockingConcurrentQueue()
    {
        _queue = new ConcurrentQueue<T>();
        _occupiedNodes = new SemaphoreSlim(0);
    }

    public bool TryPeek(out T item, CancellationToken cancellationToken)
    {
        item = default;

        if (!WaitWhileEmpty(cancellationToken))
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        if (!_queue.TryPeek(out item))
            throw new InvalidOperationException();

        _occupiedNodes.Release();
        return true;
    }

    public bool TryDequeue(out T item, CancellationToken cancellationToken)
    {
        item = default;

        if (!WaitWhileEmpty(cancellationToken))
            return false;

        var result = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = _queue.TryDequeue(out item);
            if (!result)
                throw new InvalidOperationException();
        }
        finally
        {
            if (!result)
                _occupiedNodes.Release();
        }

        return true;
    }

    public bool TryEnqueue(T item, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        var spinner = new SpinWait();
        while (true)
        {
            var observedAdders = _currentAdders;
            if (Interlocked.CompareExchange(ref _currentAdders, observedAdders + 1, observedAdders) == observedAdders)
                break;

            spinner.SpinOnce();
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _queue.Enqueue(item);
            _occupiedNodes.Release();
        }
        finally
        {
            Interlocked.Decrement(ref _currentAdders);
        }

        return true;
    }

    protected bool WaitWhileEmpty(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        try
        {
            _occupiedNodes.Wait(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            return false;
        }

        return true;
    }

    public virtual void Clear()
    {
        while (Count > 0)
            TryDequeue(out var _, CancellationToken.None);
    }

    protected virtual void Dispose(bool disposing)
    {
        _occupiedNodes.Dispose();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
