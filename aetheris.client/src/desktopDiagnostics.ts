/** Optional native diagnostic sink; normal browser hosting remains unchanged. */
export function reportDesktopDiagnostic(event: string, detail: unknown): void {
  const host = window as Window & {
    chrome?: { webview?: { postMessage(message: unknown): void } };
  };
  host.chrome?.webview?.postMessage({ event, detail });
}

window.addEventListener("error", (event) =>
  reportDesktopDiagnostic("error", event.message));
window.addEventListener("unhandledrejection", (event) =>
  reportDesktopDiagnostic("unhandledrejection", String(event.reason)));
