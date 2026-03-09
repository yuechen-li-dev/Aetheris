export const STEP_UPLOAD_LIMIT_MB = 250;
export const STEP_UPLOAD_LIMIT_BYTES = STEP_UPLOAD_LIMIT_MB * 1024 * 1024;

export function formatMegabytes(bytes: number): string {
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}
