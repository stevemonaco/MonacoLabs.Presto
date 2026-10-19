import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const reloadKey = 'tempo-reload-at';
let failed = false;

function onUnhandledError(error) {
    if (failed) return;
    failed = true;

    let reloadedAt = 0;
    try { reloadedAt = Number(sessionStorage.getItem(reloadKey)) || 0; } catch { }
    if (Date.now() - reloadedAt < 10000) {
        const splash = document.querySelector('.avalonia-splash');
        const message = splash.querySelector('.splash-error');
        splash.classList.remove('splash-close');
        message.textContent = String(error);
        message.hidden = false;
        return;
    }

    try { sessionStorage.setItem(reloadKey, String(Date.now())); } catch { }
    alert(`Tempo hit an error and will reload.\n\n${error}`);
    location.reload();
}

window.addEventListener('error', e => onUnhandledError(e.error ?? e.message));
window.addEventListener('unhandledrejection', e => onUnhandledError(e.reason));

const rootStyle = document.documentElement.style;

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    // withModuleConfig is missing from dotnet.d.ts but is where dotnet.js reads the progress hook.
    .withModuleConfig({
        onDownloadResourceProgress: (loaded, total) => {
            const percent = `${Math.round(loaded / total * 100)}%`;
            rootStyle.setProperty('--wasm-load-percentage', percent);
            if (loaded === total)
                document.querySelector('.avalonia-splash').classList.add('loaded');
        }
    })
    .create();

// The progress hook can skip loaded === total (e.g. a cached load), so finish the ring here too.
rootStyle.setProperty('--wasm-load-percentage', '100%');
document.querySelector('.avalonia-splash').classList.add('loaded');

const config = dotnetRuntime.getConfig();
const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);

// A hidden tab may be discarded without another event, so land any pending save now.
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden')
        exports.Tempo.Browser.Program.Flush();
});

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
