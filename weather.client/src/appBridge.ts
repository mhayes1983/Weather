// Only runs in the Android app (vite --mode app). There's no server there, so /api calls go to .NET on the phone.

export const isApp = import.meta.env.MODE === 'app';

export const appName = isApp ? 'Personal Weather' : 'Weather';

export interface SetupStation {
    id: string;
    name: string;
    selected: boolean;
}

export interface SetupState {
    complete: boolean;
    zip: string | null;
    hasWeatherUnderground: boolean;
    hasAmbientWeather: boolean;
    stations: SetupStation[];
}

export interface SetupRequest {
    zip: string;
    weatherUndergroundKey: string | null;
    ambientApiKey: string | null;
    ambientApplicationKey: string | null;
    stationIds: string[];
}

export type KeyCheckResult = 'Valid' | 'Invalid' | 'CouldNotCheck';

interface HybridWebView {
    InvokeDotNet(method: string, args?: unknown[]): Promise<unknown>;
}

declare global {
    interface Window {
        HybridWebView: HybridWebView;
        __appBack?: () => boolean;
    }
}

let setupState: SetupState | undefined;

export function currentSetup() {
    return setupState;
}

export function getSetupState() {
    return window.HybridWebView.InvokeDotNet('GetSetupState').then(result => JSON.parse(result as string) as SetupState);
}

export function checkWeatherUnderground(apiKey: string) {
    return window.HybridWebView.InvokeDotNet('CheckWeatherUnderground', [apiKey]) as Promise<KeyCheckResult>;
}

export function checkAmbientWeather(apiKey: string, applicationKey: string) {
    return window.HybridWebView.InvokeDotNet('CheckAmbientWeather', [apiKey, applicationKey]) as Promise<KeyCheckResult>;
}

export async function saveSetup(request: SetupRequest) {
    const result = await window.HybridWebView.InvokeDotNet('SaveSetup', [JSON.stringify(request)]);
    return JSON.parse(result as string) as { ok: boolean; error: string | null };
}

function loadBridgeScript() {
    return new Promise<void>((resolve, reject) => {
        const script = document.createElement('script');
        script.src = '/_framework/hybridwebview.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('The app bridge could not be loaded.'));
        document.head.append(script);
    });
}

function callApi(url: string, signal?: AbortSignal | null) {
    return new Promise<Response>((resolve, reject) => {
        const abort = () => reject(new DOMException('The request was aborted.', 'AbortError'));
        if (signal?.aborted) {
            abort();
            return;
        }

        // .NET can't be stopped part way, so an aborted caller just stops waiting.
        signal?.addEventListener('abort', abort, { once: true });

        window.HybridWebView.InvokeDotNet('Api', [url])
            .then(result => {
                const text = result as string;
                const separator = text.indexOf('\n');
                resolve(new Response(text.slice(separator + 1), {
                    status: Number(text.slice(0, separator)),
                    headers: { 'Content-Type': 'application/json' },
                }));
            })
            .catch(reject);
    });
}

function routeApiCalls() {
    const browserFetch = window.fetch.bind(window);

    window.fetch = (input, init) => {
        if (typeof input === 'string' && input.startsWith('/api/')) {
            return callApi(input, init?.signal);
        }

        return browserFetch(input, init);
    };
}

// A page from another site would load inside the app, with no way back and access to the bridge.
function openLinksInBrowser() {
    document.addEventListener('click', event => {
        const link = event.target instanceof Element ? event.target.closest('a[href]') : null;
        if (link instanceof HTMLAnchorElement && link.origin !== window.location.origin) {
            event.preventDefault();
            void window.HybridWebView.InvokeDotNet('OpenLink', [link.href]);
        }
    }, true);
}

// The Back button: close the menu, then go back a page, then leave the app.
function handleBack() {
    const dialog = document.querySelector<HTMLDialogElement>('dialog[open]');
    if (dialog) {
        dialog.close();
        return true;
    }

    if (window.location.pathname !== '/' && window.history.length > 1) {
        window.history.back();
        return true;
    }

    return false;
}

export async function startAppBridge() {
    await loadBridgeScript();
    routeApiCalls();
    openLinksInBrowser();
    window.__appBack = handleBack;
    setupState = await getSetupState();
}
