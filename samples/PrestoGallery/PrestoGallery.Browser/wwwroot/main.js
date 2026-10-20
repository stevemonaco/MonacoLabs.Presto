import { dotnet } from './_framework/dotnet.js'

const reloadKey = 'presto-gallery-reload-at';
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
    alert(`Presto Gallery hit an error and will reload.\n\n${error}`);
    location.reload();
}

// Avalonia's screen probe queries 'window-management' without a catch, and Firefox rejects names it doesn't know.
const queryPermission = navigator.permissions?.query.bind(navigator.permissions);
if (queryPermission)
    navigator.permissions.query = async descriptor => {
        try { return await queryPermission(descriptor); }
        catch (error) { if (error instanceof TypeError) return { state: 'denied' }; throw error; }
    };

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
            rootStyle.setProperty('--wasm-load-percentage-text', `"${percent}"`);
        }
    })
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);