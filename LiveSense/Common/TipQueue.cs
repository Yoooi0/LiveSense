using LiveSense.Service;
using PropertyChanged;
using Stylet;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace LiveSense.Common
{
    public interface ITipQueue : IReadOnlyCollection<ServiceTip>, IDisposable
    {
        void Clear();
        void Enqueue(ServiceTip tip);
        ServiceTip Peek(CancellationToken token);
        ServiceTip Dequeue(CancellationToken token);
    }

    [DoNotNotify]
    public class TipQueue : ITipQueue
    {
        private readonly ConcurrentQueue<ServiceTip> _queue;
        private readonly SemaphoreSlim _occupiedNodes;
        private volatile int _currentAdders;

        public TipQueue()
        {
            _queue = new ConcurrentQueue<ServiceTip>();
            _occupiedNodes = new SemaphoreSlim(0);
        }

        public int Count => _queue.Count;
        public IEnumerator<ServiceTip> GetEnumerator() => _queue.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _queue.GetEnumerator();

        public virtual void Enqueue(ServiceTip item)
        {
            TryEnqueue(item, CancellationToken.None);
        }

        public virtual ServiceTip Peek(CancellationToken token)
        {
            if (TryPeek(out var item, token))
                return item;

            throw new InvalidOperationException();
        }

        public virtual ServiceTip Dequeue(CancellationToken token)
        {
            if (TryDequeue(out var item, token))
                return item;

            throw new InvalidOperationException();
        }

        public virtual void Clear()
        {
            while (Count > 0)
                TryDequeue(out var _, CancellationToken.None);
        }

        protected bool TryEnqueue(ServiceTip item, CancellationToken cancellationToken)
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

        protected bool TryPeek(out ServiceTip item, CancellationToken cancellationToken)
        {
            item = null;

            if (!WaitWhileEmpty(cancellationToken))
                return false;

            cancellationToken.ThrowIfCancellationRequested();
            if (!_queue.TryPeek(out item))
                throw new InvalidOperationException();

            _occupiedNodes.Release();
            return true;
        }

        protected bool TryDequeue(out ServiceTip item, CancellationToken cancellationToken)
        {
            item = null;

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

    [DoNotNotify]
    public class ObservableTipQueue : TipQueue, INotifyPropertyChanged, INotifyCollectionChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public override void Enqueue(ServiceTip item)
        {
            TryEnqueue(item, CancellationToken.None);
            OnCollectionChanged();
        }

        public override ServiceTip Dequeue(CancellationToken token)
        {
            if (TryDequeue(out var item, token))
            {
                OnCollectionChanged();
                return item;
            }

            throw new InvalidOperationException();
        }

        public override void Clear()
        {
            base.Clear();
            OnCollectionChanged();
        }

        private void OnCollectionChanged() => Execute.OnUIThreadSync(() =>
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        });
    }
}
