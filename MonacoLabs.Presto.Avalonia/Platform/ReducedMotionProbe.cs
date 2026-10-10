using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace MonacoLabs.Presto.Platform;

/// <summary>Reads the OS reduce-motion setting where the platform exposes it without a platform TFM.</summary>
internal static partial class ReducedMotionProbe
{
    private const string ObjC = "/usr/lib/libobjc.dylib";
    private const string BrowserQuery = "(prefers-reduced-motion: reduce)";
    private static JSObject? s_browserQuery;

    /// <summary>Whether the OS asks for reduced motion, or <c>null</c> when this platform cannot say.</summary>
    public static bool? Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                const uint getClientAreaAnimation = 0x1042;
                return SystemParametersInfo(getClientAreaAnimation, 0, out var animate, 0) ? !animate : null;
            }
            if (OperatingSystem.IsIOS()) return UIAccessibilityIsReduceMotionEnabled();
            if (OperatingSystem.IsMacOS())
            {
                var workspace = Send(GetClass("NSWorkspace"), Selector("sharedWorkspace"));
                return workspace == 0 ? null : SendBool(workspace, Selector("accessibilityDisplayShouldReduceMotion"));
            }
            if (OperatingSystem.IsBrowser()) return BrowserMatches();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or JSException)
        {
        }
        return null;
    }

    /// <summary>Calls <paramref name="changed"/> whenever the platform reports a change itself; only the browser does.</summary>
    public static void Listen(Action changed)
    {
        if (!OperatingSystem.IsBrowser()) return;
        try
        {
            ReflectSet(s_browserQuery ??= MatchMedia(BrowserQuery), "onchange", changed);
        }
        catch (JSException)
        {
        }
    }

    [SupportedOSPlatform("browser")]
    private static bool BrowserMatches() => (s_browserQuery ??= MatchMedia(BrowserQuery)).GetPropertyAsBoolean("matches");

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);

    [LibraryImport("/System/Library/Frameworks/UIKit.framework/UIKit")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool UIAccessibilityIsReduceMotionEnabled();

    [LibraryImport(ObjC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);

    [LibraryImport(ObjC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint Selector(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SendBool(nint receiver, nint selector);

    [JSImport("globalThis.matchMedia")]
    [SupportedOSPlatform("browser")]
    private static partial JSObject MatchMedia(string query);

    // A MediaQueryList's onchange can only be assigned from JS, so Reflect.set does it with the marshalled callback.
    [JSImport("globalThis.Reflect.set")]
    [SupportedOSPlatform("browser")]
    private static partial bool ReflectSet(JSObject target, string key, [JSMarshalAs<JSType.Function>] Action value);
}
