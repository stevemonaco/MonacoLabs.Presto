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
let shownFraction = 0;

function showProgress(fraction) {
    shownFraction = Math.max(shownFraction, Math.min(fraction, 1));
    rootStyle.setProperty('--wasm-load-percentage', `${Math.round(shownFraction * 100)}%`);
    if (shownFraction === 1)
        document.querySelector('.avalonia-splash').classList.add('loaded');
}

// Written at publish; missing under `dotnet run`, where progress falls back to counting files.
const downloadSizes = await fetch('./download-sizes.json', { cache: 'no-cache' })
    .then(response => response.ok ? response.json() : null, () => null);
const bytesRead = new Map();
let loadedBytes = 0;
let totalBytes = 0;

async function matchesIntegrity(chunks, length, integrity) {
    const separator = integrity.indexOf('-');
    const bytes = new Uint8Array(length);
    let offset = 0;
    for (const chunk of chunks) {
        bytes.set(chunk, offset);
        offset += chunk.byteLength;
    }
    const digest = new Uint8Array(await crypto.subtle.digest(integrity.slice(0, separator).replace('sha', 'SHA-'), bytes));
    return btoa(String.fromCharCode(...digest)) === integrity.slice(separator + 1);
}

// Counts bytes as the runtime reads them, so the large dotnet.native.wasm no longer sits at 99% of a file count.
function loadResource(type, name, defaultUri, integrity) {
    const size = downloadSizes?.[name];
    if (size === undefined || type === 'dotnetjs')
        return undefined;

    const previous = bytesRead.get(name);
    if (previous === undefined) totalBytes += size;
    else loadedBytes -= previous;
    bytesRead.set(name, 0);

    // The browser holds back an SRI-checked body until all of it has arrived, so the hash is checked here instead.
    const verifyHere = integrity && globalThis.crypto?.subtle !== undefined;
    // A returned Response skips the runtime's fetch options, so these repeat them.
    const init = { credentials: 'same-origin', cache: integrity ? 'force-cache' : 'no-cache', integrity: verifyHere ? undefined : integrity || undefined };
    return fetch(defaultUri, init).then(response => {
        if (!response.ok || !response.body)
            return response;

        const reader = response.body.getReader();
        const chunks = [];
        const body = new ReadableStream({
            async pull(controller) {
                const { done, value } = await reader.read();
                if (!done) {
                    if (verifyHere) chunks.push(value);
                    bytesRead.set(name, bytesRead.get(name) + value.byteLength);
                    loadedBytes += value.byteLength;
                    showProgress(loadedBytes / totalBytes);
                    controller.enqueue(value);
                } else if (verifyHere && !await matchesIntegrity(chunks, bytesRead.get(name), integrity)) {
                    controller.error(new Error(`${name} does not match its integrity hash`));
                } else {
                    controller.close();
                }
            },
            cancel: reason => reader.cancel(reason)
        });
        // The body is already decoded, so the server's Content-Encoding and Content-Length no longer apply.
        return new Response(body, {
            status: response.status,
            statusText: response.statusText,
            headers: { 'Content-Type': response.headers.get('Content-Type') ?? 'application/octet-stream' }
        });
    });
}

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    // Unthrottled, every download is requested at once, so the byte total is known from the start.
    .withConfig({ maxParallelDownloads: 1000 })
    .withResourceLoader(loadResource)
    // withModuleConfig is missing from dotnet.d.ts but is where dotnet.js reads the progress hook.
    .withModuleConfig({
        onDownloadResourceProgress: (loaded, total) => {
            if (!downloadSizes)
                showProgress(loaded / total);
        }
    })
    .create();

// The progress hook can skip loaded === total (e.g. a cached load), so finish the ring here too.
showProgress(1);

const config = dotnetRuntime.getConfig();
const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);

// A hidden tab may be discarded without another event, so land any pending save now.
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden')
        exports.Tempo.Browser.Program.Flush();
});

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
