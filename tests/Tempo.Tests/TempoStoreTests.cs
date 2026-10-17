using MonacoLabs.Presto;
using Tempo.Model;

namespace Tempo.Tests;

public sealed class TempoStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TempoStoreTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SavedDataLoadsBackUnchanged()
    {
        var store = new TempoStore(_root);
        var task = new TaskItem { Title = "Write proposal", Notes = "n", EstimateSessions = 3, CompletedSessions = 1, Tag = TaskTag.Design, Created = DateTimeOffset.UnixEpoch };
        var session = new SessionRecord(task.Id, FocusMode.Focus, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(25), true);
        await store.SaveAsync(new TempoData
        {
            Tasks = [task, new TaskItem { Title = "Second", IsDone = true }],
            Settings = new Settings { WorkMinutes = 50, Motion = MotionPreference.Reduced, WindowWidth = 900, WindowX = -10, IsMaximized = true },
            Sessions = [session],
        });

        var loaded = await new TempoStore(_root).LoadAsync();

        Assert.Equal(["Write proposal", "Second"], loaded.Tasks.Select(t => t.Title));
        var first = loaded.Tasks[0];
        Assert.Equal((task.Id, "n", 3, 1, false, TaskTag.Design, DateTimeOffset.UnixEpoch),
            (first.Id, first.Notes, first.EstimateSessions, first.CompletedSessions, first.IsDone, first.Tag, first.Created));
        Assert.True(loaded.Tasks[1].IsDone);
        Assert.Equal((50, MotionPreference.Reduced, 900d, -10, true),
            (loaded.Settings.WorkMinutes, loaded.Settings.Motion, loaded.Settings.WindowWidth, loaded.Settings.WindowX, loaded.Settings.IsMaximized));
        Assert.Equal([session], loaded.Sessions);
        Assert.True(store.Exists);
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public async Task MissingFileGivesDefaults()
    {
        var store = new TempoStore(_root);
        var loaded = await store.LoadAsync();

        Assert.False(store.Exists);
        Assert.Empty(loaded.Tasks);
        Assert.Empty(loaded.Sessions);
        Assert.Equal(25, loaded.Settings.WorkMinutes);
        Assert.True(loaded.Settings.PlayIntro);
    }

    [Fact]
    public async Task AFileMissingAKeyLoadsThatKeyAsDefault()
    {
        var store = new TempoStore(_root);
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(store.FilePath, """{ "Tasks": [ { "Title": "Kept" } ] }""", TestContext.Current.CancellationToken);

        var loaded = await store.LoadAsync();

        Assert.Equal(["Kept"], loaded.Tasks.Select(t => t.Title));
        Assert.Null(loaded.Tasks[0].Tag);
        Assert.Empty(loaded.Sessions);
        Assert.Equal(25, loaded.Settings.WorkMinutes);
    }

    [Fact]
    public async Task CorruptFileGivesDefaultsAndIsKeptAsBak()
    {
        var store = new TempoStore(_root);
        Directory.CreateDirectory(_root);
        byte[] garbage = [0x7B, 0x22, 0x54, 0x61, 0x73, 0x6B, 0xFF, 0x00];
        await File.WriteAllBytesAsync(store.FilePath, garbage, TestContext.Current.CancellationToken);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded.Tasks);
        Assert.Equal(25, loaded.Settings.WorkMinutes);
        Assert.Equal(garbage, await File.ReadAllBytesAsync(store.FilePath + ".bak", TestContext.Current.CancellationToken));
        Assert.False(File.Exists(store.FilePath));
    }
}
