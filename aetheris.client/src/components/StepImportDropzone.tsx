import { useCallback, useEffect, useMemo, useRef, useState, type DragEvent } from 'react';
import { z } from 'zod';

type StepSchemaFamily = 'AP203' | 'AP214' | 'AP242' | 'Unknown';
type StepCompatibility = 'Supported' | 'Experimental' | 'Not verified';

type StepFileMetadata = {
    fileName: string;
    size: number;
    schemaFamily: StepSchemaFamily;
    compatibility: StepCompatibility;
};

type StepImportDropzoneProps = {
    resetToken: number;
    onFileAccepted: (file: File) => void;
    onValidationError: (message: string) => void;
};

const stepFileMetadataSchema = z.object({
    fileName: z.string().min(1),
    size: z.number().positive(),
    schemaFamily: z.enum(['AP203', 'AP214', 'AP242', 'Unknown']),
    compatibility: z.enum(['Supported', 'Experimental', 'Not verified']),
});

const stepFileSelectionSchema = z
    .array(z.instanceof(File))
    .length(1, 'Please select a single STEP file.')
    .superRefine((files, ctx) => {
        const [file] = files;

        if (!file) {
            return;
        }

        const lowerName = file.name.toLowerCase();
        if (!lowerName.endsWith('.step') && !lowerName.endsWith('.stp')) {
            ctx.addIssue({
                code: z.ZodIssueCode.custom,
                message: 'Unsupported file type. Please select a .step or .stp file.',
            });
        }

        if (file.size <= 0) {
            ctx.addIssue({
                code: z.ZodIssueCode.custom,
                message: 'File is empty.',
            });
        }
    });

function fileListToArray(fileList: FileList | null): File[] {
    return fileList ? Array.from(fileList) : [];
}

function formatFileSize(bytes: number): string {
    if (bytes < 1024) {
        return `${bytes} bytes`;
    }

    const kib = bytes / 1024;
    if (kib < 1024) {
        return `${kib.toFixed(1)} KB`;
    }

    const mib = kib / 1024;
    return `${mib.toFixed(2)} MB`;
}

function schemaToCompatibility(schemaFamily: StepSchemaFamily): StepCompatibility {
    if (schemaFamily === 'AP242') {
        return 'Supported';
    }

    if (schemaFamily === 'AP203' || schemaFamily === 'AP214') {
        return 'Experimental';
    }

    return 'Not verified';
}

function normalizeStepSchema(token: string): StepSchemaFamily {
    const normalized = token.trim().toUpperCase().replace(/[-\s]+/g, '_');
    const compact = normalized.replace(/_/g, '');

    if (normalized.includes('CONFIG_CONTROL_DESIGN') || compact.includes('AP203')) {
        return 'AP203';
    }

    if (normalized.includes('AUTOMOTIVE_DESIGN') || compact.includes('AP214')) {
        return 'AP214';
    }

    if (
        compact.includes('AP242')
        || normalized.includes('MANAGED_MODEL_BASED_3D_ENGINEERING')
        || normalized.includes('MODEL_BASED_3D_ENGINEERING')
        || normalized.includes('APPLIED_MODEL_BASED_3D_ENGINEERING')
    ) {
        return 'AP242';
    }

    return 'Unknown';
}

function detectStepSchemaFamily(stepHeaderText: string): StepSchemaFamily {
    const schemaMatch = stepHeaderText.match(/FILE_SCHEMA\s*\(\s*\(([^)]*)\)\s*\)\s*;/i);
    if (!schemaMatch?.[1]) {
        return 'Unknown';
    }

    const schemaTokens = Array.from(schemaMatch[1].matchAll(/'([^']+)'/g), (match) => match[1]);
    if (schemaTokens.length === 0) {
        return 'Unknown';
    }

    for (const token of schemaTokens) {
        const mapped = normalizeStepSchema(token);
        if (mapped !== 'Unknown') {
            return mapped;
        }
    }

    return 'Unknown';
}

async function readStepHeader(file: File): Promise<string> {
    const headerSliceSize = Math.min(file.size, 64 * 1024);
    const headerChunk = file.slice(0, headerSliceSize);
    return headerChunk.text();
}

export function StepImportDropzone({ resetToken, onFileAccepted, onValidationError }: StepImportDropzoneProps) {
    const [isDragActive, setIsDragActive] = useState(false);
    const [selectionMetadata, setSelectionMetadata] = useState<StepFileMetadata | null>(null);
    const [selectionError, setSelectionError] = useState<string | null>(null);
    const inputRef = useRef<HTMLInputElement | null>(null);

    useEffect(() => {
        setSelectionMetadata(null);
        setSelectionError(null);
    }, [resetToken]);

    const handleFiles = useCallback(async (files: File[]) => {
        const parsed = stepFileSelectionSchema.safeParse(files);
        if (!parsed.success) {
            const message = parsed.error.issues[0]?.message ?? 'Invalid STEP file selection.';
            setSelectionMetadata(null);
            setSelectionError(message);
            onValidationError(message);
            return;
        }

        const file = parsed.data[0];

        let schemaFamily: StepSchemaFamily = 'Unknown';
        try {
            const headerText = await readStepHeader(file);
            schemaFamily = detectStepSchemaFamily(headerText);
        } catch {
            schemaFamily = 'Unknown';
        }

        const metadataResult = stepFileMetadataSchema.safeParse({
            fileName: file.name,
            size: file.size,
            schemaFamily,
            compatibility: schemaToCompatibility(schemaFamily),
        });

        if (!metadataResult.success) {
            const message = metadataResult.error.issues[0]?.message ?? 'Unable to read STEP file metadata.';
            setSelectionMetadata(null);
            setSelectionError(message);
            onValidationError(message);
            return;
        }

        setSelectionError(null);
        setSelectionMetadata(metadataResult.data);
        onFileAccepted(file);
    }, [onFileAccepted, onValidationError]);

    const handleBrowseClick = useCallback(() => {
        inputRef.current?.click();
    }, []);

    const handleDragOver = useCallback((event: DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        event.dataTransfer.dropEffect = 'copy';
        setIsDragActive(true);
    }, []);

    const handleDragLeave = useCallback((event: DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragActive(false);
    }, []);

    const handleDrop = useCallback((event: DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setIsDragActive(false);
        void handleFiles(fileListToArray(event.dataTransfer.files));
    }, [handleFiles]);

    const className = useMemo(
        () => `step-dropzone${isDragActive ? ' step-dropzone--active' : ''}`,
        [isDragActive],
    );

    return (
        <>
            <input
                ref={inputRef}
                className="step-dropzone__input"
                type="file"
                accept=".step,.stp,text/plain"
                aria-label="STEP 242 File"
                data-testid="step-import-file-input"
                onChange={(event) => {
                    void handleFiles(fileListToArray(event.target.files));
                    event.currentTarget.value = '';
                }}
            />
            <div
                className={className}
                role="button"
                tabIndex={0}
                onClick={handleBrowseClick}
                onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        handleBrowseClick();
                    }
                }}
                onDragOver={handleDragOver}
                data-testid="step-import-dropzone"
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}>
                {selectionError ? (
                    <div className="step-dropzone__content">
                        <p className="step-dropzone__primary">File selection needs attention</p>
                        <p className="step-dropzone__meta">{selectionError}</p>
                    </div>
                ) : selectionMetadata ? (
                    <div className="step-dropzone__content">
                        <p className="step-dropzone__primary" title={selectionMetadata.fileName}>{selectionMetadata.fileName}</p>
                        <p className="step-dropzone__meta">Size: {formatFileSize(selectionMetadata.size)}</p>
                        <p className="step-dropzone__meta">Detected schema: {selectionMetadata.schemaFamily}</p>
                        <p className="step-dropzone__meta">Compatibility: {selectionMetadata.compatibility}</p>
                    </div>
                ) : (
                    <p>Drop STEP file here or click to browse</p>
                )}
            </div>
        </>
    );
}
