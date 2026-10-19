using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using MonacoLabs.Presto;

namespace Tempo.Android;

[Application]
public class Application : AvaloniaAndroidApplication<Tempo.App>
{
    protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .UsePresto()
            .WithInterFont();
}
