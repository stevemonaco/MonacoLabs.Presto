using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Draggables;

namespace PrestoSample.Pages;

public partial class DraggablePage : UserControl
{
    private const int LogLines = 6;

    private readonly List<string> _log = [];
    private int _drags;
    private int _updates;

    public DraggablePage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var spring = PrestoAvalonia.CreateScope(this).Execute(_ =>
        {
            PrestoAvalonia.CreateDraggable(SnapCard, new() { Container = Stage, Snap = Utils.Snap(40) });
            PrestoAvalonia.CreateDraggable(Rail, new()
            {
                Container = Stage,
                Trigger = Handle,
                Y = new DraggableAxisParams { Enabled = false },
            });
            return PrestoAvalonia.CreateDraggable(SpringCard, new()
            {
                Container = Stage,
                ReleaseStiffness = 40,
                ReleaseDamping = 8,
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
            });
        });

        ShowState(spring);
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
