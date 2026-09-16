import { createRuntime, transferables } from './runtime.js';

let invoke;
self.onmessage = async event => {
  const { id, request, runtimeBase } = event.data;
  try {
    invoke ??= await createRuntime(runtimeBase ? new URL(runtimeBase) : undefined);
    const result = invoke(request);
    self.postMessage({ id, result }, { transfer: transferables(result) });
  } catch (error) {
    self.postMessage({ id, error: { name: error.name, code: error.code ?? 'internal-error', message: error.message, details: error.details } });
  }
};
