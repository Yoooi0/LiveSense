using PropertyChanged;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LiveSense.Common
{
#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type

    // based on: http://drwpf.com/blog/2007/09/16/can-i-bind-my-itemscontrol-to-a-dictionary/
    [SuppressPropertyChangedWarnings]
    public class ObservableDictionary<TKey, TValue> :
            IDictionary<TKey, TValue>,
            IDictionary,
            ICollection,
            INotifyCollectionChanged,
            INotifyPropertyChanged
    {
        protected KeyedDictionaryEntryCollection<TKey> _keyedEntryCollection;

        private int _countCache = 0;
        private Dictionary<TKey, TValue> _dictionaryCache = new Dictionary<TKey, TValue>();
        private int _dictionaryCacheVersion = 0;
        private int _version = 0;

        public ObservableDictionary()
        {
            _keyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>();
        }

        public ObservableDictionary(IDictionary<TKey, TValue> dictionary)
        {
            _keyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>();

            foreach (var entry in dictionary)
                DoAddEntry(entry.Key, entry.Value);
        }

        public ObservableDictionary(IEqualityComparer<TKey> comparer)
        {
            _keyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>(comparer);
        }

        public ObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
        {
            _keyedEntryCollection = new KeyedDictionaryEntryCollection<TKey>(comparer);

            foreach (var entry in dictionary)
                DoAddEntry(entry.Key, entry.Value);
        }

        public IEqualityComparer<TKey> Comparer => _keyedEntryCollection.Comparer;

        public int Count => _keyedEntryCollection.Count;

        public Dictionary<TKey, TValue>.KeyCollection Keys => TrueDictionary.Keys;
        public Dictionary<TKey, TValue>.ValueCollection Values => TrueDictionary.Values;

        public TValue this[TKey key]
        {
            get => (TValue)_keyedEntryCollection[key].Value;
            set => DoSetEntry(key, value);
        }

        private Dictionary<TKey, TValue> TrueDictionary
        {
            get
            {
                if (_dictionaryCacheVersion != _version)
                {
                    _dictionaryCache.Clear();
                    foreach (var entry in _keyedEntryCollection)
                        _dictionaryCache.Add((TKey)entry.Key, (TValue)entry.Value);

                    _dictionaryCacheVersion = _version;
                }

                return _dictionaryCache;
            }
        }


        public void Add(TKey key, TValue value) => DoAddEntry(key, value);
        public void Clear() => DoClearEntries();
        public bool ContainsKey(TKey key) => _keyedEntryCollection.Contains(key);
        public bool ContainsValue(TValue value) => TrueDictionary.ContainsValue(value);
        public IEnumerator GetEnumerator() => new Enumerator<TKey, TValue>(this, false);
        public bool Remove(TKey key) => DoRemoveEntry(key);

        public bool TryGetValue(TKey key, out TValue value)
        {
            var result = _keyedEntryCollection.Contains(key);
            value = result ? (TValue)_keyedEntryCollection[key].Value : default(TValue);
            return result;
        }

        protected virtual bool AddEntry(TKey key, TValue value)
        {
            _keyedEntryCollection.Add(new DictionaryEntry(key, value));
            return true;
        }

        protected virtual bool ClearEntries()
        {
            var result = (Count > 0);
            if (result)
                _keyedEntryCollection.Clear();

            return result;
        }

        protected int GetIndexAndEntryForKey(TKey key, out DictionaryEntry entry)
        {
            entry = new DictionaryEntry();

            var index = -1;
            if (_keyedEntryCollection.Contains(key))
            {
                entry = _keyedEntryCollection[key];
                index = _keyedEntryCollection.IndexOf(entry);
            }

            return index;
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);
        protected virtual void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected virtual bool RemoveEntry(TKey key) => _keyedEntryCollection.Remove(key);

        protected virtual bool SetEntry(TKey key, TValue value)
        {
            var keyExists = _keyedEntryCollection.Contains(key);

            // if identical key/value pair already exists, nothing to do
            if (keyExists && value.Equals((TValue)_keyedEntryCollection[key].Value))
                return false;

            // otherwise, remove the existing entry
            if (keyExists)
                _keyedEntryCollection.Remove(key);

            // add the new entry
            _keyedEntryCollection.Add(new DictionaryEntry(key, value));

            return true;
        }

        private void DoAddEntry(TKey key, TValue value)
        {
            if (AddEntry(key, value))
            {
                _version++;

                var index = GetIndexAndEntryForKey(key, out var entry);
                FireEntryAddedNotifications(entry, index);
            }
        }

        private void DoClearEntries()
        {
            if (ClearEntries())
            {
                _version++;
                FireResetNotifications();
            }
        }

        private bool DoRemoveEntry(TKey key)
        {
            var index = GetIndexAndEntryForKey(key, out var entry);

            var result = RemoveEntry(key);
            if (result)
            {
                _version++;
                if (index > -1)
                    FireEntryRemovedNotifications(entry, index);
            }

            return result;
        }

        private void DoSetEntry(TKey key, TValue value)
        {
            var index = GetIndexAndEntryForKey(key, out var entry);

            if (SetEntry(key, value))
            {
                _version++;

                // if prior entry existed for this key, fire the removed notifications
                if (index > -1)
                {
                    FireEntryRemovedNotifications(entry, index);

                    // force the property change notifications to fire for the modified entry
                    _countCache--;
                }

                // then fire the added notifications
                index = GetIndexAndEntryForKey(key, out entry);
                FireEntryAddedNotifications(entry, index);
            }
        }

        private void FireEntryAddedNotifications(DictionaryEntry entry, int index)
        {
            // fire the relevant PropertyChanged notifications
            FirePropertyChangedNotifications();

            // fire CollectionChanged notification
            if (index > -1)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value), index));
            else
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void FireEntryRemovedNotifications(DictionaryEntry entry, int index)
        {
            // fire the relevant PropertyChanged notifications
            FirePropertyChangedNotifications();

            // fire CollectionChanged notification
            if (index > -1)
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value), index));
            else
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void FirePropertyChangedNotifications()
        {
            if (Count != _countCache)
            {
                _countCache = Count;
                OnPropertyChanged("Count");
                OnPropertyChanged("Item[]");
                OnPropertyChanged("Keys");
                OnPropertyChanged("Values");
            }
        }

        private void FireResetNotifications()
        {
            // fire the relevant PropertyChanged notifications
            FirePropertyChangedNotifications();

            // fire CollectionChanged notification
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => DoAddEntry(key, value);
        bool IDictionary<TKey, TValue>.Remove(TKey key) => DoRemoveEntry(key);
        bool IDictionary<TKey, TValue>.ContainsKey(TKey key) => _keyedEntryCollection.Contains(key);
        bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value) => TryGetValue(key, out value);

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

        TValue IDictionary<TKey, TValue>.this[TKey key]
        {
            get => (TValue)_keyedEntryCollection[key].Value;
            set => DoSetEntry(key, value);
        }

        void IDictionary.Add(object key, object value) => DoAddEntry((TKey)key, (TValue)value);
        void IDictionary.Clear() => DoClearEntries();
        bool IDictionary.Contains(object key) => _keyedEntryCollection.Contains((TKey)key);
        IDictionaryEnumerator IDictionary.GetEnumerator() => new Enumerator<TKey, TValue>(this, true);
        bool IDictionary.IsFixedSize => false;
        bool IDictionary.IsReadOnly => false;

        object IDictionary.this[object key]
        {
            get => _keyedEntryCollection[(TKey)key].Value;
            set => DoSetEntry((TKey)key, (TValue)value);
        }

        ICollection IDictionary.Keys => Keys;
        void IDictionary.Remove(object key) => DoRemoveEntry((TKey)key);
        ICollection IDictionary.Values => Values;

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> kvp) => DoAddEntry(kvp.Key, kvp.Value);
        void ICollection<KeyValuePair<TKey, TValue>>.Clear() => DoClearEntries();
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> kvp) => _keyedEntryCollection.Contains(kvp.Key);

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("CopyTo() failed:  array parameter was null");
            if ((index < 0) || (index > array.Length))
                throw new ArgumentOutOfRangeException("CopyTo() failed:  index parameter was outside the bounds of the supplied array");
            if ((array.Length - index) < _keyedEntryCollection.Count)
                throw new ArgumentException("CopyTo() failed:  supplied array was too small");

            foreach (var entry in _keyedEntryCollection)
                array[index++] = new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
        }

        int ICollection<KeyValuePair<TKey, TValue>>.Count => _keyedEntryCollection.Count;
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> kvp) => DoRemoveEntry(kvp.Key);
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_keyedEntryCollection).CopyTo(array, index);
        int ICollection.Count => _keyedEntryCollection.Count;
        bool ICollection.IsSynchronized => ((ICollection)_keyedEntryCollection).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)_keyedEntryCollection).SyncRoot;
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => new Enumerator<TKey, TValue>(this, false);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add { CollectionChanged += value; }
            remove { CollectionChanged -= value; }
        }

        protected virtual event NotifyCollectionChangedEventHandler CollectionChanged;

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { PropertyChanged += value; }
            remove { PropertyChanged -= value; }
        }

        protected virtual event PropertyChangedEventHandler PropertyChanged;

        protected class KeyedDictionaryEntryCollection<TKey> : KeyedCollection<TKey, DictionaryEntry>
        {
            public KeyedDictionaryEntryCollection() : base() { }

            public KeyedDictionaryEntryCollection(IEqualityComparer<TKey> comparer) : base(comparer) { }
            protected override TKey GetKeyForItem(DictionaryEntry entry) => (TKey)entry.Key;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Enumerator<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IDictionaryEnumerator, IEnumerator
        {
            private ObservableDictionary<TKey, TValue> _dictionary;
            private int _version;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;
            private bool _isDictionaryEntryEnumerator;

            internal Enumerator(ObservableDictionary<TKey, TValue> dictionary, bool isDictionaryEntryEnumerator)
            {
                _dictionary = dictionary;
                _version = dictionary._version;
                _index = -1;
                _isDictionaryEntryEnumerator = isDictionaryEntryEnumerator;
                _current = new KeyValuePair<TKey, TValue>();
            }

            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    ValidateCurrent();
                    return _current;
                }
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                ValidateVersion();
                _index++;
                if (_index < _dictionary._keyedEntryCollection.Count)
                {
                    _current = new KeyValuePair<TKey, TValue>((TKey)_dictionary._keyedEntryCollection[_index].Key, (TValue)_dictionary._keyedEntryCollection[_index].Value);
                    return true;
                }

                _index = -2;
                _current = new KeyValuePair<TKey, TValue>();
                return false;
            }

            private void ValidateCurrent()
            {
                if (_index == -1)
                    throw new InvalidOperationException("The enumerator has not been started.");
                else if (_index == -2)
                    throw new InvalidOperationException("The enumerator has reached the end of the collection.");
            }

            private void ValidateVersion()
            {
                if (_version != _dictionary._version)
                    throw new InvalidOperationException("The enumerator is not valid because the dictionary changed.");
            }

            object IEnumerator.Current
            {
                get
                {
                    ValidateCurrent();
                    if (_isDictionaryEntryEnumerator)
                        return new DictionaryEntry(_current.Key, _current.Value);
                    return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
                }
            }

            void IEnumerator.Reset()
            {
                ValidateVersion();
                _index = -1;
                _current = new KeyValuePair<TKey, TValue>();
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    ValidateCurrent();
                    return new DictionaryEntry(_current.Key, _current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    ValidateCurrent();
                    return _current.Key;
                }
            }
            object IDictionaryEnumerator.Value
            {
                get
                {
                    ValidateCurrent();
                    return _current.Value;
                }
            }
        }
    }
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type
}
