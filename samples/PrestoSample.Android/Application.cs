using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
            .UsePresto()
            .WithInterFont();
        }
    }
}
