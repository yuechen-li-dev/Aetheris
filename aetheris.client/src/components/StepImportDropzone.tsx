import { useCallback, useMemo, useRef, useState, type DragEvent } from 'react';
import { z } from 'zod';

type StepImportDropzoneProps = {
    onFileAccepted: (file: File) => void;
    onValidationError: (message: string) => void;
};

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

export function StepImportDropzone({ onFileAccepted, onValidationError }: StepImportDropzoneProps) {
    const [isDragActive, setIsDragActive] = useState(false);
    const inputRef = useRef<HTMLInputElement | null>(null);

    const handleFiles = useCallback((files: File[]) => {
        const parsed = stepFileSelectionSchema.safeParse(files);
        if (!parsed.success) {
            onValidationError(parsed.error.issues[0]?.message ?? 'Invalid STEP file selection.');
            return;
        }

        onFileAccepted(parsed.data[0]);
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
        handleFiles(fileListToArray(event.dataTransfer.files));
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
                    handleFiles(fileListToArray(event.target.files));
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
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}>
                <p>Drop STEP file here or click to browse</p>
            </div>
        </>
    );
}
