import path from "node:path";

export type CliCommand = "validate" | "build" | "view" | "verify";
export type Severity = "error" | "warning" | "info";

export interface SourceSpan {
  path?: string;
  line?: number;
  column?: number;
  endLine?: number;
  endColumn?: number;
  start?: number;
  length?: number;
}

export interface CliDiagnostic {
  code?: string;
  severity: Severity;
  message: string;
  hint?: string;
  source?: string;
  sourceSpan?: SourceSpan;
}

export interface ParsedCliResult {
  success?: boolean;
  diagnostics: CliDiagnostic[];
  artifactPath?: string;
  launched?: boolean;
}

type UnknownRecord = Record<string, unknown>;

function record(value: unknown): UnknownRecord | undefined {
  return value !== null && typeof value === "object" && !Array.isArray(value) ? (value as UnknownRecord) : undefined;
}

function string(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function number(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function severity(value: unknown): Severity {
  const normalized = String(value ?? "error").toLowerCase();
  if (normalized === "warning" || normalized === "warn") return "warning";
  if (normalized === "info" || normalized === "information") return "info";
  return "error";
}

function span(value: unknown): SourceSpan | undefined {
  const item = record(value);
  if (!item) return undefined;
  const result: SourceSpan = {
    path: string(item.path) ?? string(item.file) ?? string(item.sourcePath),
    line: number(item.line),
    column: number(item.column),
    endLine: number(item.endLine),
    endColumn: number(item.endColumn),
    start: number(item.start),
    length: number(item.length),
  };
  return Object.values(result).some((value) => value !== undefined) ? result : undefined;
}

const nonActionableCodes = new Set([
  "firmament-v2-parse-succeeded",
  "firmament-v2-parser-invoked",
  "firmament-v2-unified-canonical-parsed",
  "firmament-v2-unified-canonical-symbols-bound",
]);

function diagnostics(value: unknown): CliDiagnostic[] {
  if (!Array.isArray(value)) return [];
  return value.flatMap((entry): CliDiagnostic[] => {
    if (typeof entry === "string") return [{ severity: "error", message: entry }];
    const item = record(entry);
    if (!item) return [];
    const code = string(item.code);
    if (code && nonActionableCodes.has(code)) return [];
    const message = string(item.message) ?? code;
    if (!message) return [];
    return [
      {
        code,
        severity: severity(item.severity),
        message,
        hint: string(item.hint) ?? string(item.actionableHint),
        source: string(item.source),
        sourceSpan: span(item.sourceSpan ?? item.span ?? item.location ?? item),
      },
    ];
  });
}

export function parseCliJson(stdout: string, command: CliCommand): ParsedCliResult {
  let parsed: unknown;
  try {
    parsed = JSON.parse(stdout);
  } catch (error) {
    throw new Error(`Aetheris CLI returned malformed JSON: ${error instanceof Error ? error.message : String(error)}`);
  }
  const root = record(parsed);
  if (!root) throw new Error("Aetheris CLI returned a JSON value instead of an object.");
  if (command === "validate") {
    const report = record(root.firmamentV2Validation);
    if (!report) throw new Error("Aetheris validate JSON did not contain firmamentV2Validation.");
    return { success: report.status !== "invalid", diagnostics: diagnostics(report.diagnostics) };
  }
  const artifact = record(root.artifact);
  return {
    success:
      typeof root.success === "boolean"
        ? root.success
        : command === "verify"
          ? root.overallAdmission !== "Rejected"
          : undefined,
    diagnostics: diagnostics(root.diagnostics),
    artifactPath: string(root.outputPath) ?? string(root.output) ?? string(root.stepPath) ?? string(artifact?.path),
    launched: typeof root.launched === "boolean" ? root.launched : undefined,
  };
}

export interface OffsetPosition {
  line: number;
  character: number;
}
export interface DiagnosticRange {
  start: OffsetPosition;
  end: OffsetPosition;
}

function offsetPosition(text: string, offset: number): OffsetPosition {
  const bounded = Math.max(0, Math.min(offset, text.length));
  const prefix = text.slice(0, bounded);
  const lines = prefix.split(/\r?\n/);
  return { line: lines.length - 1, character: lines.at(-1)?.length ?? 0 };
}

export function diagnosticRange(text: string, sourceSpan?: SourceSpan): DiagnosticRange {
  if (sourceSpan?.start !== undefined) {
    const start = offsetPosition(text, sourceSpan.start);
    const end = offsetPosition(text, sourceSpan.start + Math.max(sourceSpan.length ?? 1, 1));
    return { start, end };
  }
  if (sourceSpan?.line !== undefined || sourceSpan?.column !== undefined) {
    const start = {
      line: Math.max((sourceSpan.line ?? 1) - 1, 0),
      character: Math.max((sourceSpan.column ?? 1) - 1, 0),
    };
    const end = {
      line: Math.max((sourceSpan.endLine ?? sourceSpan.line ?? 1) - 1, start.line),
      character: Math.max((sourceSpan.endColumn ?? (sourceSpan.column ?? 1) + 1) - 1, start.character + 1),
    };
    return { start, end };
  }
  return { start: { line: 0, character: 0 }, end: { line: 0, character: 1 } };
}

export interface Invocation {
  executable: string;
  args: string[];
}

export function commandInvocation(command: CliCommand, filePath: string, configuredExecutable = ""): Invocation {
  return { executable: configuredExecutable.trim() || "aetheris", args: [command, path.resolve(filePath), "--json"] };
}

export function developmentInvocation(projectPath: string, invocation: Invocation): Invocation {
  return {
    executable: "dotnet",
    args: ["run", "--project", path.resolve(projectPath), "--", ...invocation.args],
  };
}

export function sortDiagnostics(items: CliDiagnostic[]): CliDiagnostic[] {
  return [...items].sort((left, right) => {
    const a = left.sourceSpan;
    const b = right.sourceSpan;
    return (
      (a?.line ?? Number.MAX_SAFE_INTEGER) - (b?.line ?? Number.MAX_SAFE_INTEGER) ||
      (a?.column ?? Number.MAX_SAFE_INTEGER) - (b?.column ?? Number.MAX_SAFE_INTEGER) ||
      (a?.start ?? Number.MAX_SAFE_INTEGER) - (b?.start ?? Number.MAX_SAFE_INTEGER) ||
      (left.code ?? "").localeCompare(right.code ?? "") ||
      left.message.localeCompare(right.message)
    );
  });
}

export function shouldValidateOnSave(languageId: string, enabled: boolean, trusted: boolean): boolean {
  return languageId === "firmament" && enabled && trusted;
}

export function commandSucceeded(exitCode: number, result: ParsedCliResult): boolean {
  return exitCode === 0 && result.success !== false;
}

export function isMissingExecutableError(error: unknown): boolean {
  return error instanceof Error && "code" in error && error.code === "ENOENT";
}
