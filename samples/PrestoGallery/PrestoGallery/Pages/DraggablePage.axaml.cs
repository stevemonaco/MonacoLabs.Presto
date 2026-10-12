using Avalonia.Controls;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Draggables;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class DraggablePage : ShowcasePage
{
    private const int LogLines = 6;

    private readonly List<string> _log = [];
    private int _drags;
    private int _updates;
    private Draggable? _spring;

    public DraggablePage() => InitializeComponent();

    protected override void OnAttached()
    {
        SnapCode.Code = Snippet.Of(PrestoAvalonia.CreateDraggable(SnapCard, new() { Container = Stage, Snap = Utils.Snap(40) })).Code;
        RailCode.Code = Snippet.Of(PrestoAvalonia.CreateDraggable(Rail, new()
        {
            Container = Stage,
            Trigger = Handle,
            Y = new DraggableAxisParams { Enabled = false },
        })).Code;
        CreateSpring();
    }

    private void ReleaseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_spring is null) return;
        Scope.Execute(_ =>
        {
            _spring.Revert();
            CreateSpring();
        });
    }

    private void CreateSpring()
    {
        var (release, releaseCode) = ReleaseBox.SelectedIndex == 1
            ? Snippet.Of(new DraggableParams { ReleaseInertia = new Inertia() { ModifyTarget = Utils.Snap(40) } })
            : Snippet.Of(new DraggableParams { ReleaseStiffness = 40, ReleaseDamping = 8 });
        (_spring, var code) = Snippet.Of(PrestoAvalonia.CreateDraggable(SpringCard, release with
        {
            Container = Stage,
            OnGrab = Record("OnGrab"),
            OnSnap = Record("OnSnap"),
            OnRelease = Record("OnRelease"),
            OnSettle = Record("OnSettle"),
            OnDrag = _ => _drags++,
            OnUpdate = draggable =>
            {
                _updates++;
                ShowState(draggable);
            },
            // The progress readouts are a ratio of the container bounds, which are only real once it has been measured.
            OnAfterResize = ShowState,
        }));
        SpringCode.Code = $"var release = {releaseCode};\n\n{code}";
        ShowState(_spring);
    }

    private Action<Draggable> Record(string callback) => _ =>
    {
        _log.Insert(0, callback);
        if (_log.Count > LogLines) _log.RemoveAt(LogLines);
        LogText.Text = string.Join(Environment.NewLine, _log);
    };

    private void ShowState(Draggable draggable)
    {
        XText.Text = Utils.RoundPad(draggable.X, 0);
        YText.Text = Utils.RoundPad(draggable.Y, 0);
        ProgressXText.Text = Utils.RoundPad(draggable.ProgressX, 2);
        ProgressYText.Text = Utils.RoundPad(draggable.ProgressY, 2);
        CountsText.Text = $"OnDrag {_drags} · OnUpdate {_updates}";
    }
}
