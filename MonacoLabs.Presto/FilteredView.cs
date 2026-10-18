using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MonacoLabs.Presto;

/// <summary>
/// A read-only live view of the items of an <see cref="ObservableCollection{T}"/> that its <see cref="Filter"/> accepts, in source order.
/// It raises one change event per row and never resets on a re-evaluation, so list motion sees rows come and go one at a time.
/// </summary>
public sealed class FilteredView<T> : IReadOnlyList<T>, IList, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable, IMovableItems
{
    private static readonly PropertyChangedEventArgs CountChanged = new(nameof(Count));
    private static readonly PropertyChangedEventArgs ItemsChanged = new("Item[]");

    private readonly ObservableCollection<T> _source;
    private readonly string[] _refreshOn;
    private readonly List<Entry> _entries = [];
    private readonly List<T> _view = [];
    private Func<T, bool>? _filter;
    private bool _disposed;

    /// <param name="source">The collection to view.</param>
    /// <param name="filter">Accepts the items to show; null shows every item.</param>
    /// <param name="refreshOn">Item property names whose change re-evaluates that item.</param>
    public FilteredView(ObservableCollection<T> source, Func<T, bool>? filter = null, IEnumerable<string>? refreshOn = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _filter = filter;
        _refreshOn = refreshOn is null ? [] : [.. refreshOn];
        Load();
        _source.CollectionChanged += OnSourceChanged;
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Accepts the items to show; null shows every item. Setting it re-evaluates every item.</summary>
    public Func<T, bool>? Filter
    {
        get => _filter;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _filter = value;
            Refresh();
        }
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _view.Count;
        }
    }

    /// <inheritdoc/>
    public T this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _view[index];
        }
    }

    /// <summary>Returns the row index of <paramref name="item"/>, or -1 when the view does not show it.</summary>
    public int IndexOf(T item)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _view.IndexOf(item);
    }

    /// <summary>Whether the view shows <paramref name="item"/>.</summary>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _view.GetEnumerator();
    }

    /// <summary>Re-evaluates every item: rows no longer accepted leave last to first, then newly accepted items enter in source order.</summary>
    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var entries = _entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            entry.Next = Accepts(entry.Item);
            entries[i] = entry;
        }

        var viewIndex = _view.Count;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (!entry.Accepted)
                continue;
            viewIndex--;
            if (!entry.Next)
                SetAccepted(i, viewIndex, false);
        }

        viewIndex = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!entry.Accepted && entry.Next)
                SetAccepted(i, viewIndex, true);
            if (entries[i].Accepted)
                viewIndex++;
        }
    }

    /// <summary>Re-evaluates <paramref name="item"/> alone.</summary>
    public void Refresh(T item)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (!comparer.Equals(entry.Item, item))
                continue;
            var accepted = Accepts(entry.Item);
            if (accepted != entry.Accepted)
                SetAccepted(i, ViewIndexOf(i), accepted);
        }
    }

    /// <summary>
    /// Moves the row at <paramref name="oldIndex"/> to <paramref name="newIndex"/> by moving its item in the source next to its new
    /// visible neighbor, so hidden items between stay put and the view raises one move.
    /// </summary>
    public void Move(int oldIndex, int newIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(oldIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(oldIndex, _view.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(newIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newIndex, _view.Count);
        if (oldIndex == newIndex)
            return;

        var from = SourceIndexOf(oldIndex);
        int to;
        if (newIndex == _view.Count - 1)
        {
            to = SourceIndexOf(newIndex);
        }
        else
        {
            var successor = SourceIndexOf(newIndex > oldIndex ? newIndex + 1 : newIndex);
            to = from < successor ? successor - 1 : successor;
        }

        _source.Move(from, to);
    }

    /// <summary>Unsubscribes from the source and its items; the view cannot be used afterwards.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _source.CollectionChanged -= OnSourceChanged;
        foreach (var entry in _entries)
            Unsubscribe(entry.Item);
        _entries.Clear();
        _view.Clear();
    }

    private bool Accepts(T item) => _filter is null || _filter(item);

    private void Load()
    {
        foreach (var item in _source)
        {
            var accepted = Accepts(item);
            _entries.Add(new Entry(item, accepted));
            if (accepted)
                _view.Add(item);
            Subscribe(item);
        }
    }

    private int ViewIndexOf(int sourceIndex)
    {
        var viewIndex = 0;
        for (var i = 0; i < sourceIndex; i++)
        {
            if (_entries[i].Accepted)
                viewIndex++;
        }
        return viewIndex;
    }

    private int SourceIndexOf(int viewIndex)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Accepted && viewIndex-- == 0)
                return i;
        }
        throw new ArgumentOutOfRangeException(nameof(viewIndex));
    }

    private void SetAccepted(int sourceIndex, int viewIndex, bool accepted)
    {
        var entry = _entries[sourceIndex];
        entry.Accepted = accepted;
        _entries[sourceIndex] = entry;
        if (accepted)
        {
            _view.Insert(viewIndex, entry.Item);
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, entry.Item, viewIndex), true);
        }
        else
        {
            _view.RemoveAt(viewIndex);
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, entry.Item, viewIndex), true);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (var k = 0; k < e.NewItems!.Count; k++)
                    Insert(e.NewStartingIndex + k, (T)e.NewItems[k]!);
                break;
            case NotifyCollectionChangedAction.Remove:
                for (var k = 0; k < e.OldItems!.Count; k++)
                    RemoveAt(e.OldStartingIndex);
                break;
            case NotifyCollectionChangedAction.Replace:
                for (var k = 0; k < e.NewItems!.Count; k++)
                    Replace(e.NewStartingIndex + k, (T)e.NewItems[k]!);
                break;
            case NotifyCollectionChangedAction.Move:
                MoveEntry(e.OldStartingIndex, e.NewStartingIndex);
                break;
            default:
                Reload();
                break;
        }
    }

    private void Insert(int sourceIndex, T item)
    {
        Subscribe(item);
        _entries.Insert(sourceIndex, new Entry(item, false));
        if (Accepts(item))
            SetAccepted(sourceIndex, ViewIndexOf(sourceIndex), true);
    }

    private void RemoveAt(int sourceIndex)
    {
        var entry = _entries[sourceIndex];
        Unsubscribe(entry.Item);
        if (entry.Accepted)
            SetAccepted(sourceIndex, ViewIndexOf(sourceIndex), false);
        _entries.RemoveAt(sourceIndex);
    }

    private void Replace(int sourceIndex, T item)
    {
        var old = _entries[sourceIndex];
        Unsubscribe(old.Item);
        Subscribe(item);
        var accepted = Accepts(item);
        var viewIndex = ViewIndexOf(sourceIndex);
        if (old.Accepted && accepted)
        {
            _entries[sourceIndex] = new Entry(item, true);
            _view[viewIndex] = item;
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, old.Item, viewIndex), false);
            return;
        }

        if (old.Accepted)
            SetAccepted(sourceIndex, viewIndex, false);
        _entries[sourceIndex] = new Entry(item, false);
        if (accepted)
            SetAccepted(sourceIndex, viewIndex, true);
    }

    private void MoveEntry(int oldSourceIndex, int newSourceIndex)
    {
        var entry = _entries[oldSourceIndex];
        var oldViewIndex = ViewIndexOf(oldSourceIndex);
        _entries.RemoveAt(oldSourceIndex);
        _entries.Insert(newSourceIndex, entry);
        if (!entry.Accepted)
            return;
        var newViewIndex = ViewIndexOf(newSourceIndex);
        if (newViewIndex == oldViewIndex)
            return;
        _view.RemoveAt(oldViewIndex);
        _view.Insert(newViewIndex, entry.Item);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, entry.Item, newViewIndex, oldViewIndex), false);
    }

    private void Reload()
    {
        var count = _view.Count;
        foreach (var entry in _entries)
            Unsubscribe(entry.Item);
        _entries.Clear();
        _view.Clear();
        Load();
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset), count != _view.Count);
    }

    private void Raise(NotifyCollectionChangedEventArgs args, bool countChanged)
    {
        CollectionChanged?.Invoke(this, args);
        if (countChanged)
            PropertyChanged?.Invoke(this, CountChanged);
        PropertyChanged?.Invoke(this, ItemsChanged);
    }

    private void Subscribe(T item)
    {
        if (_refreshOn.Length > 0 && item is INotifyPropertyChanged notifier)
            notifier.PropertyChanged += OnItemPropertyChanged;
    }

    private void Unsubscribe(T item)
    {
        if (_refreshOn.Length > 0 && item is INotifyPropertyChanged notifier)
            notifier.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is T item && (string.IsNullOrEmpty(e.PropertyName) || Array.IndexOf(_refreshOn, e.PropertyName) >= 0))
            Refresh(item);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    bool IList.IsReadOnly => true;

    bool IList.IsFixedSize => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    bool IList.Contains(object? value) => value is T item && Contains(item);

    int IList.IndexOf(object? value) => value is T item ? IndexOf(item) : -1;

    void ICollection.CopyTo(Array array, int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ((ICollection)_view).CopyTo(array, index);
    }

    int IList.Add(object? value) => throw new NotSupportedException();

    void IList.Insert(int index, object? value) => throw new NotSupportedException();

    void IList.Remove(object? value) => throw new NotSupportedException();

    void IList.RemoveAt(int index) => throw new NotSupportedException();

    void IList.Clear() => throw new NotSupportedException();

    private struct Entry(T item, bool accepted)
    {
        public readonly T Item = item;
        public bool Accepted = accepted;
        public bool Next;
    }
}
