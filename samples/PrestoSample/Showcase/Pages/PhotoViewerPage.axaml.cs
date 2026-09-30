using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Gestures;

namespace PrestoSample.Showcase.Pages;

public partial class PhotoViewerPage : ShowcasePage
{
    private static readonly string[] Files = ["mongolia.jpg", "margarita-sunset.jpg", "port-blanc.jpg"];

    private readonly Image[] _photos;
    private Transformable[] _viewers = [];
    private int _current;

    public PhotoViewerPage()
    {
        InitializeComponent();
        var bitmaps = Files.Select(file => new Bitmap(AssetLoader.Open(new Uri($"avares://PrestoSample/Assets/Photos/{file}")))).ToArray();
        _photos = [.. bitmaps.Select(bitmap => new Image
        {
            Source = bitmap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        })];
        Photos.ItemsSource = _photos.Select(photo => new Panel { ClipToBounds = true, Background = Palette.Background, Children = { photo } }).ToList();
        Filmstrip.ItemsSource = bitmaps.Select(bitmap => new Image { Source = bitmap, Height = 48 }).ToList();
    }

    protected override void OnAttached()
    {
        _viewers = [.. _photos.Select(photo => photo.CreateTransformable(new TransformableParams
        {
            Rotate = true,
            RotationSnap = 90,
            OnUpdate = viewer =>
            {
                ScaleText.Text = Utils.RoundPad(viewer.Scale, 2);
                RotationText.Text = Utils.RoundPad(viewer.Rotation, 1);
            },
            OnRelease = viewer => VelocityText.Text = Utils.RoundPad(viewer.Velocity, 2),
        }))];
    }

    private void PhotoChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewers.Length == 0) return;
        _viewers[_current].Reset(animate: false);
        _current = Photos.SelectedIndex;
    }

    private void ResetClicked(object? sender, RoutedEventArgs e) => _viewers[_current].Reset();
}
