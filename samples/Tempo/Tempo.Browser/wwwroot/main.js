import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();
const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);

// A hidden tab may be discarded without another event, so land any pending save now.
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden')
        exports.Tempo.Browser.Program.Flush();
});

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
