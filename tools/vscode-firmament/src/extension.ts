import { spawn } from "node:child_process";
import path from "node:path";
import * as vscode from "vscode";
import {
  commandInvocation,
  commandSucceeded,
  developmentInvocation,
  diagnosticRange,
  isMissingExecutableError,
  parseCliJson,
  shouldValidateOnSave,
  sortDiagnostics,
  type CliCommand,
  type CliDiagnostic,
} from "./core";

const supported: Record<CliCommand, Set<string>> = {
  validate: new Set([".firmament"]),
  build: new Set([".firmament"]),
  view: new Set([".firmament", ".step", ".stp"]),
  verify: new Set([".firmament", ".step", ".stp"]),
};

interface ProcessResult {
  exitCode: number;
  stdout: string;
  stderr: string;
  elapsedMs: number;
}

function run(executable: string, args: string[]): Promise<ProcessResult> {
  const started = performance.now();
  return new Promise((resolve, reject) => {
    const child = spawn(executable, args, { windowsHide: true, shell: false });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8").on("data", (chunk) => {
      stdout += chunk;
    });
    child.stderr.setEncoding("utf8").on("data", (chunk) => {
      stderr += chunk;
    });
    child.on("error", reject);
    child.on("close", (code) =>
      resolve({ exitCode: code ?? -1, stdout, stderr, elapsedMs: performance.now() - started }),
    );
  });
}

function executableSetting(): string {
  return vscode.workspace.getConfiguration("aetheris").get<string>("executablePath", "");
}

function activeDocument(command: CliCommand): vscode.TextDocument | undefined {
  const document = vscode.window.activeTextEditor?.document;
  if (
    !document ||
    document.uri.scheme !== "file" ||
    !supported[command].has(path.extname(document.fileName).toLowerCase())
  ) {
    void vscode.window.showWarningMessage(
      `Aetheris ${command} requires an active ${[...supported[command]].join(" or ")} file.`,
    );
    return undefined;
  }
  return document;
}

function asDiagnostic(item: CliDiagnostic, text: string): vscode.Diagnostic {
  const mapped = diagnosticRange(text, item.sourceSpan);
  const value = new vscode.Diagnostic(
    new vscode.Range(mapped.start.line, mapped.start.character, mapped.end.line, mapped.end.character),
    item.hint ? `${item.message}\nHint: ${item.hint}` : item.message,
    item.severity === "warning"
      ? vscode.DiagnosticSeverity.Warning
      : item.severity === "info"
        ? vscode.DiagnosticSeverity.Information
        : vscode.DiagnosticSeverity.Error,
  );
  value.code = item.code;
  value.source = item.source || "Aetheris / Firmament";
  return value;
}

export function activate(context: vscode.ExtensionContext): void {
  const output = vscode.window.createOutputChannel("Aetheris");
  const collection = vscode.languages.createDiagnosticCollection("Aetheris / Firmament");
  context.subscriptions.push(output, collection);

  const execute = async (command: CliCommand, document?: vscode.TextDocument, quiet = false): Promise<void> => {
    if (!vscode.workspace.isTrusted) {
      if (!quiet) void vscode.window.showWarningMessage("Trust this workspace before running the Aetheris CLI.");
      return;
    }
    document ??= activeDocument(command);
    if (!document) return;
    if (document.isDirty && !(await document.save())) return;
    collection.delete(document.uri);
    const invocation = commandInvocation(command, document.fileName, executableSetting());
    if (!quiet) {
      output.appendLine(`> ${invocation.executable} ${invocation.args.map((arg) => JSON.stringify(arg)).join(" ")}`);
    }
    try {
      let result: ProcessResult;
      try {
        result = await run(invocation.executable, invocation.args);
      } catch (error) {
        const mayUseDevelopmentFallback =
          context.extensionMode === vscode.ExtensionMode.Development &&
          !executableSetting().trim() &&
          isMissingExecutableError(error);
        if (!mayUseDevelopmentFallback) throw error;
        const fallback = developmentInvocation(
          path.resolve(context.extensionPath, "..", "..", "Aetheris.CLI", "Aetheris.CLI.csproj"),
          invocation,
        );
        result = await run(fallback.executable, fallback.args);
      }
      let parsed;
      try {
        parsed = parseCliJson(result.stdout, command);
      } catch (error) {
        output.appendLine(result.stderr || result.stdout);
        throw error;
      }
      const grouped = new Map<string, CliDiagnostic[]>();
      for (const item of sortDiagnostics(parsed.diagnostics)) {
        const target = item.sourceSpan?.path
          ? path.resolve(path.dirname(document.fileName), item.sourceSpan.path)
          : document.fileName;
        grouped.set(target, [...(grouped.get(target) ?? []), item]);
      }
      for (const [target, items] of grouped) {
        const uri = vscode.Uri.file(target);
        const text = target === document.fileName ? document.getText() : "";
        collection.set(
          uri,
          items.map((item) => asDiagnostic(item, text)),
        );
      }
      if (result.stderr.trim()) output.appendLine(result.stderr.trim());
      if (!quiet) {
        output.appendLine(
          `${command} completed in ${result.elapsedMs.toFixed(0)} ms${parsed.artifactPath ? `; artifact: ${parsed.artifactPath}` : ""}`,
        );
      }
      const errors = parsed.diagnostics.filter((item) => item.severity === "error").length;
      const succeeded = commandSucceeded(result.exitCode, parsed);
      const message =
        command === "build" && parsed.artifactPath && succeeded
          ? `Built ${path.basename(parsed.artifactPath)}`
          : command === "view" && parsed.launched
            ? "Aetheris: opened in Cadmata"
            : command === "verify" && succeeded
              ? "Aetheris: verification complete"
              : errors > 0 || result.exitCode !== 0
                ? `Aetheris: ${errors || parsed.diagnostics.length} problem(s)`
                : "Aetheris: Valid";
      if (!quiet || result.exitCode !== 0) vscode.window.setStatusBarMessage(message, 5000);
    } catch (error) {
      const message = isMissingExecutableError(error)
        ? "Aetheris CLI was not found. Install Aetheris or set 'aetheris.executablePath'."
        : `Aetheris ${command} failed: ${error instanceof Error ? error.message : String(error)}`;
      output.appendLine(message);
      if (!quiet) void vscode.window.showErrorMessage(message);
    }
  };

  for (const command of ["validate", "build", "view", "verify"] as const) {
    context.subscriptions.push(vscode.commands.registerCommand(`aetheris.${command}`, () => execute(command)));
  }
  context.subscriptions.push(
    vscode.workspace.onDidSaveTextDocument((document) => {
      if (
        shouldValidateOnSave(
          document.languageId,
          vscode.workspace.getConfiguration("aetheris", document.uri).get("validateOnSave", true),
          vscode.workspace.isTrusted,
        )
      ) {
        void execute("validate", document, true);
      }
    }),
  );
}

export function deactivate(): void {}
