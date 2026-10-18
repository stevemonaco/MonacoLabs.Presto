using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MonacoLabs.Presto.Tests;

public sealed class FilteredViewTests
{
    private static readonly Func<int, bool> Even = n => n % 2 == 0;

    [Fact]
    public void ShowsTheAcceptedItemsInSourceOrder()
    {
        var source = new ObservableCollection<int>([1, 2, 3, 4, 6]);

        Assert.Equal([2, 4, 6], new FilteredView<int>(source, Even));
        Assert.Equal([1, 2, 3, 4, 6], new FilteredView<int>(source));
    }

    [Fact]
    public void SourceAddRaisesOneAddAtTheViewIndex()
    {
        var source = new ObservableCollection<int>([1, 2, 3, 4]);
        var (view, log) = Watch(source, Even);

        source.Insert(3, 8);
        source.Insert(0, 5);

        Assert.Equal(["Add 8 @1"], log);
        Assert.Equal([2, 8, 4], view);
    }

    [Fact]
    public void SourceRemoveRaisesOneRemoveAtTheViewIndex()
    {
        var source = new ObservableCollection<int>([1, 2, 3, 4]);
        var (view, log) = Watch(source, Even);

        source.RemoveAt(2);
        source.RemoveAt(2);

        Assert.Equal(["Remove 4 @1"], log);
        Assert.Equal([2], view);
    }

    [Theory]
    [InlineData(2, 6, "Replace 6 for 2 @0")]
    [InlineData(2, 5, "Remove 2 @0")]
    [InlineData(1, 6, "Add 6 @0")]
    [InlineData(1, 5)]
    public void SourceReplaceRaisesOnlyWhatTheViewSees(int old, int replacement, params string[] expected)
    {
        var source = new ObservableCollection<int>([old, 4]);
        var (_, log) = Watch(source, Even);

        source[0] = replacement;

        Assert.Equal(expected, log);
    }

    [Fact]
    public void SourceMovePastHiddenItemsOrOfAHiddenItemRaisesNothing()
    {
        var source = new ObservableCollection<int>([2, 1, 3, 4]);
        var (view, log) = Watch(source, Even);

        source.Move(0, 2);
        source.Move(0, 1);

        Assert.Empty(log);
        Assert.Equal([2, 4], view);
    }

    [Fact]
    public void SourceMovePastAVisibleItemRaisesOneMove()
    {
        var source = new ObservableCollection<int>([2, 1, 4, 3, 6]);
        var (view, log) = Watch(source, Even);

        source.Move(0, 3);

        Assert.Equal(["Move 2 @0->1"], log);
        Assert.Equal([4, 2, 6], view);
    }

    [Fact]
    public void SourceResetRaisesOneReset()
    {
        var source = new ObservableCollection<int>([1, 2]);
        var (view, log) = Watch(source, Even);

        source.Clear();

        Assert.Equal(["Reset"], log);
        Assert.Empty(view);
    }

    [Fact]
    public void FilterChangeRemovesLastToFirstThenInsertsInSourceOrder()
    {
        var source = new ObservableCollection<int>([1, 2, 3, 4, 5, 6]);
        var (view, log) = Watch(source, Even);

        view.Filter = n => n is 1 or 4 or 5;

        Assert.Equal(["Remove 6 @2", "Remove 2 @0", "Add 1 @0", "Add 5 @2"], log);
        Assert.Equal([1, 4, 5], view);
    }

    [Fact]
    public void RefreshReEvaluatesStateOutsideTheItems()
    {
        var source = new ObservableCollection<int>([1, 2, 3]);
        var limit = 2;
        var (view, log) = Watch(source, n => n <= limit);

        limit = 3;
        view.Refresh();

        Assert.Equal(["Add 3 @2"], log);
    }

    [Fact]
    public void RefreshItemReEvaluatesThatItemAlone()
    {
        var a = new Item("a", "work");
        var b = new Item("b", "work");
        var source = new ObservableCollection<Item>([a, b]);
        var (view, log) = Watch(source, i => i.Tag == "work");

        a.Tag = "home";
        b.Tag = "home";
        view.Refresh(b);

        Assert.Equal(["Remove b @1"], log);
        Assert.Equal([a], view);
    }

    [Fact]
    public void RefreshOnReactsOnlyToTheListedNames()
    {
        var a = new Item("a", "work");
        var b = new Item("b", "work");
        var source = new ObservableCollection<Item>([a, b]);
        var (_, log) = Watch(source, i => i.Tag == "work" && i.Title != "x", [nameof(Item.Tag)]);

        a.Title = "x";
        Assert.Empty(log);

        b.Tag = "home";
        Assert.Equal(["Remove b @1"], log);

        a.Raise(null);
        Assert.Equal(["Remove b @1", "Remove a @0"], log);

        a.Title = "a";
        a.Raise("");
        Assert.Equal(["Remove b @1", "Remove a @0", "Add a @0"], log);
    }

    [Fact]
    public void ItemsAreWatchedOnlyWhileInTheSourceAndOnlyWithRefreshOn()
    {
        var a = new Item("a", "work");
        var source = new ObservableCollection<Item>([a]);
        using var plain = new FilteredView<Item>(source);
        Assert.Equal(0, a.ListenerCount);

        using var view = new FilteredView<Item>(source, refreshOn: [nameof(Item.Tag)]);
        Assert.Equal(1, a.ListenerCount);

        var b = new Item("b", "work");
        source[0] = b;
        Assert.Equal(0, a.ListenerCount);
        Assert.Equal(1, b.ListenerCount);

        source.Clear();
        Assert.Equal(0, b.ListenerCount);

        source.Add(a);
        source.Remove(a);
        Assert.Equal(0, a.ListenerCount);
    }

    [Theory]
    [InlineData(0, 1, new[] { 1, 3, 4, 5, 2, 6, 7 })]
    [InlineData(0, 2, new[] { 1, 3, 4, 5, 6, 2, 7 })]
    [InlineData(2, 0, new[] { 1, 6, 2, 3, 4, 5, 7 })]
    [InlineData(2, 1, new[] { 1, 2, 3, 6, 4, 5, 7 })]
    [InlineData(1, 0, new[] { 1, 4, 2, 3, 5, 6, 7 })]
    public void MoveWritesTheSourceNextToTheVisibleNeighbor(int oldIndex, int newIndex, int[] expectedSource)
    {
        var source = new ObservableCollection<int>([1, 2, 3, 4, 5, 6, 7]);
        var (view, log) = Watch(source, Even);
        var moved = view[oldIndex];

        view.Move(oldIndex, newIndex);

        Assert.Equal(expectedSource, source);
        Assert.Equal([$"Move {moved} @{oldIndex}->{newIndex}"], log);
        Assert.Equal(moved, view[newIndex]);
    }

    [Fact]
    public void MoveOutOfRangeThrows()
    {
        var view = new FilteredView<int>(new ObservableCollection<int>([1, 2, 4]), Even);

        Assert.Throws<ArgumentOutOfRangeException>(() => view.Move(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.Move(-1, 0));
    }

    [Fact]
    public void CountAndIndexerChangesAreRaisedAfterTheCollectionEvent()
    {
        var source = new ObservableCollection<int>([2, 4]);
        var view = new FilteredView<int>(source, Even);
        var log = new List<string>();
        view.CollectionChanged += (_, e) => log.Add(e.Action.ToString());
        view.PropertyChanged += (_, e) => log.Add(e.PropertyName!);

        source.Add(6);
        source.Move(0, 1);
        source[0] = 8;

        Assert.Equal(["Add", "Count", "Item[]", "Move", "Item[]", "Replace", "Item[]"], log);
    }

    [Fact]
    public void WritingListMembersThrow()
    {
        IList view = new FilteredView<int>(new ObservableCollection<int>([2]));

        Assert.True(view.IsReadOnly);
        Assert.True(view.IsFixedSize);
        Assert.Equal(2, view[0]);
        Assert.Throws<NotSupportedException>(() => view.Add(4));
        Assert.Throws<NotSupportedException>(() => view.Insert(0, 4));
        Assert.Throws<NotSupportedException>(() => view.Remove(2));
        Assert.Throws<NotSupportedException>(() => view.RemoveAt(0));
        Assert.Throws<NotSupportedException>(view.Clear);
        Assert.Throws<NotSupportedException>(() => view[0] = 4);
    }

    [Fact]
    public void DisposeDetachesFromTheSourceAndItemsAndLaterCallsThrow()
    {
        var a = new Item("a", "work");
        var source = new ObservableCollection<Item>([a]);
        var (view, log) = Watch(source, null, [nameof(Item.Tag)]);

        view.Dispose();
        source.Add(new Item("b", "work"));

        Assert.Empty(log);
        Assert.Equal(0, a.ListenerCount);
        Assert.Throws<ObjectDisposedException>(() => view.Count);
        Assert.Throws<ObjectDisposedException>(view.Refresh);
        Assert.Throws<ObjectDisposedException>(() => view.Move(0, 0));
        Assert.Throws<ObjectDisposedException>(() => view.Filter = null);
    }

    private static (FilteredView<T> View, List<string> Log) Watch<T>(ObservableCollection<T> source, Func<T, bool>? filter,
        IEnumerable<string>? refreshOn = null)
    {
        var view = new FilteredView<T>(source, filter, refreshOn);
        var log = new List<string>();
        view.CollectionChanged += (_, e) => log.Add(e.Action switch
        {
            NotifyCollectionChangedAction.Add => $"Add {e.NewItems![0]} @{e.NewStartingIndex}",
            NotifyCollectionChangedAction.Remove => $"Remove {e.OldItems![0]} @{e.OldStartingIndex}",
            NotifyCollectionChangedAction.Replace => $"Replace {e.NewItems![0]} for {e.OldItems![0]} @{e.NewStartingIndex}",
            NotifyCollectionChangedAction.Move => $"Move {e.NewItems![0]} @{e.OldStartingIndex}->{e.NewStartingIndex}",
            _ => "Reset",
        });
        return (view, log);
    }

    private sealed class Item(string name, string tag) : INotifyPropertyChanged
    {
        private readonly string _name = name;

        public string Title
        {
            get;
            set { field = value; Raise(nameof(Title)); }
        } = name;

        public string Tag
        {
            get => tag;
            set { tag = value; Raise(nameof(Tag)); }
        }

        public int ListenerCount => PropertyChanged?.GetInvocationList().Length ?? 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => _name;
    }
}
