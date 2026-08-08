"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// src/extension.ts
var extension_exports = {};
__export(extension_exports, {
  activate: () => activate,
  deactivate: () => deactivate
});
module.exports = __toCommonJS(extension_exports);
var import_node_child_process = require("node:child_process");
var import_node_path2 = __toESM(require("node:path"), 1);
var vscode = __toESM(require("vscode"), 1);

// src/core.ts
var import_node_path = __toESM(require("node:path"), 1);
function record(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value) ? value : void 0;
}
function string(value) {
  return typeof value === "string" && value.length > 0 ? value : void 0;
}
function number(value) {
  return typeof value === "number" && Number.isFinite(value) ? value : void 0;
}
function severity(value) {
  const normalized = String(value ?? "error").toLowerCase();
  if (normalized === "warning" || normalized === "warn") return "warning";
  if (normalized === "info" || normalized === "information") return "info";
  return "error";
}
function span(value) {
  const item = record(value);
  if (!item) return void 0;
  const result = {
    path: string(item.path) ?? string(item.file) ?? string(item.sourcePath),
    line: number(item.line),
    column: number(item.column),
    endLine: number(item.endLine),
    endColumn: number(item.endColumn),
    start: number(item.start),
    length: number(item.length)
  };
  return Object.values(result).some((value2) => value2 !== void 0) ? result : void 0;
}
var nonActionableCodes = /* @__PURE__ */ new Set([
  "firmament-v2-parse-succeeded",
  "firmament-v2-parser-invoked",
  "firmament-v2-unified-canonical-parsed",
  "firmament-v2-unified-canonical-symbols-bound"
]);
function diagnostics(value) {
  if (!Array.isArray(value)) return [];
  return value.flatMap((entry) => {
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
        sourceSpan: span(item.sourceSpan ?? item.span ?? item.location ?? item)
      }
    ];
  });
}
function parseCliJson(stdout, command) {
  let parsed;
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
    success: typeof root.success === "boolean" ? root.success : command === "verify" ? root.overallAdmission !== "Rejected" : void 0,
    diagnostics: diagnostics(root.diagnostics),
    artifactPath: string(root.outputPath) ?? string(root.output) ?? string(root.stepPath) ?? string(artifact?.path),
    launched: typeof root.launched === "boolean" ? root.launched : void 0
  };
}
function offsetPosition(text, offset) {
  const bounded = Math.max(0, Math.min(offset, text.length));
  const prefix = text.slice(0, bounded);
  const lines = prefix.split(/\r?\n/);
  return { line: lines.length - 1, character: lines.at(-1)?.length ?? 0 };
}
function diagnosticRange(text, sourceSpan) {
  if (sourceSpan?.start !== void 0) {
    const start = offsetPosition(text, sourceSpan.start);
    const end = offsetPosition(text, sourceSpan.start + Math.max(sourceSpan.length ?? 1, 1));
    return { start, end };
  }
  if (sourceSpan?.line !== void 0 || sourceSpan?.column !== void 0) {
    const start = {
      line: Math.max((sourceSpan.line ?? 1) - 1, 0),
      character: Math.max((sourceSpan.column ?? 1) - 1, 0)
    };
    const end = {
      line: Math.max((sourceSpan.endLine ?? sourceSpan.line ?? 1) - 1, start.line),
      character: Math.max((sourceSpan.endColumn ?? (sourceSpan.column ?? 1) + 1) - 1, start.character + 1)
    };
    return { start, end };
  }
  return { start: { line: 0, character: 0 }, end: { line: 0, character: 1 } };
}
function commandInvocation(command, filePath, configuredExecutable = "") {
  return { executable: configuredExecutable.trim() || "aetheris", args: [command, import_node_path.default.resolve(filePath), "--json"] };
}
function developmentInvocation(projectPath, invocation) {
  return {
    executable: "dotnet",
    args: ["run", "--project", import_node_path.default.resolve(projectPath), "--", ...invocation.args]
  };
}
function sortDiagnostics(items) {
  return [...items].sort((left, right) => {
    const a = left.sourceSpan;
    const b = right.sourceSpan;
    return (a?.line ?? Number.MAX_SAFE_INTEGER) - (b?.line ?? Number.MAX_SAFE_INTEGER) || (a?.column ?? Number.MAX_SAFE_INTEGER) - (b?.column ?? Number.MAX_SAFE_INTEGER) || (a?.start ?? Number.MAX_SAFE_INTEGER) - (b?.start ?? Number.MAX_SAFE_INTEGER) || (left.code ?? "").localeCompare(right.code ?? "") || left.message.localeCompare(right.message);
  });
}
function shouldValidateOnSave(languageId, enabled, trusted) {
  return languageId === "firmament" && enabled && trusted;
}
function commandSucceeded(exitCode, result) {
  return exitCode === 0 && result.success !== false;
}
function isMissingExecutableError(error) {
  return error instanceof Error && "code" in error && error.code === "ENOENT";
}

// src/extension.ts
var supported = {
  validate: /* @__PURE__ */ new Set([".firmament"]),
  build: /* @__PURE__ */ new Set([".firmament"]),
  view: /* @__PURE__ */ new Set([".firmament", ".step", ".stp"]),
  verify: /* @__PURE__ */ new Set([".firmament", ".step", ".stp"])
};
function run(executable, args) {
  const started = performance.now();
  return new Promise((resolve, reject) => {
    const child = (0, import_node_child_process.spawn)(executable, args, { windowsHide: true, shell: false });
    let stdout = "";
    let stderr = "";
    child.stdout.setEncoding("utf8").on("data", (chunk) => {
      stdout += chunk;
    });
    child.stderr.setEncoding("utf8").on("data", (chunk) => {
      stderr += chunk;
    });
    child.on("error", reject);
    child.on(
      "close",
      (code) => resolve({ exitCode: code ?? -1, stdout, stderr, elapsedMs: performance.now() - started })
    );
  });
}
function executableSetting() {
  return vscode.workspace.getConfiguration("aetheris").get("executablePath", "");
}
function activeDocument(command) {
  const document = vscode.window.activeTextEditor?.document;
  if (!document || document.uri.scheme !== "file" || !supported[command].has(import_node_path2.default.extname(document.fileName).toLowerCase())) {
    void vscode.window.showWarningMessage(
      `Aetheris ${command} requires an active ${[...supported[command]].join(" or ")} file.`
    );
    return void 0;
  }
  return document;
}
function asDiagnostic(item, text) {
  const mapped = diagnosticRange(text, item.sourceSpan);
  const value = new vscode.Diagnostic(
    new vscode.Range(mapped.start.line, mapped.start.character, mapped.end.line, mapped.end.character),
    item.hint ? `${item.message}
Hint: ${item.hint}` : item.message,
    item.severity === "warning" ? vscode.DiagnosticSeverity.Warning : item.severity === "info" ? vscode.DiagnosticSeverity.Information : vscode.DiagnosticSeverity.Error
  );
  value.code = item.code;
  value.source = item.source || "Aetheris / Firmament";
  return value;
}
function activate(context) {
  const output = vscode.window.createOutputChannel("Aetheris");
  const collection = vscode.languages.createDiagnosticCollection("Aetheris / Firmament");
  context.subscriptions.push(output, collection);
  const execute = async (command, document, quiet = false) => {
    if (!vscode.workspace.isTrusted) {
      if (!quiet) void vscode.window.showWarningMessage("Trust this workspace before running the Aetheris CLI.");
      return;
    }
    document ??= activeDocument(command);
    if (!document) return;
    if (document.isDirty && !await document.save()) return;
    collection.delete(document.uri);
    const invocation = commandInvocation(command, document.fileName, executableSetting());
    if (!quiet) {
      output.appendLine(`> ${invocation.executable} ${invocation.args.map((arg) => JSON.stringify(arg)).join(" ")}`);
    }
    try {
      let result;
      try {
        result = await run(invocation.executable, invocation.args);
      } catch (error) {
        const mayUseDevelopmentFallback = context.extensionMode === vscode.ExtensionMode.Development && !executableSetting().trim() && isMissingExecutableError(error);
        if (!mayUseDevelopmentFallback) throw error;
        const fallback = developmentInvocation(
          import_node_path2.default.resolve(context.extensionPath, "..", "..", "Aetheris.CLI", "Aetheris.CLI.csproj"),
          invocation
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
      const grouped = /* @__PURE__ */ new Map();
      for (const item of sortDiagnostics(parsed.diagnostics)) {
        const target = item.sourceSpan?.path ? import_node_path2.default.resolve(import_node_path2.default.dirname(document.fileName), item.sourceSpan.path) : document.fileName;
        grouped.set(target, [...grouped.get(target) ?? [], item]);
      }
      for (const [target, items] of grouped) {
        const uri = vscode.Uri.file(target);
        const text = target === document.fileName ? document.getText() : "";
        collection.set(
          uri,
          items.map((item) => asDiagnostic(item, text))
        );
      }
      if (result.stderr.trim()) output.appendLine(result.stderr.trim());
      if (!quiet) {
        output.appendLine(
          `${command} completed in ${result.elapsedMs.toFixed(0)} ms${parsed.artifactPath ? `; artifact: ${parsed.artifactPath}` : ""}`
        );
      }
      const errors = parsed.diagnostics.filter((item) => item.severity === "error").length;
      const succeeded = commandSucceeded(result.exitCode, parsed);
      const message = command === "build" && parsed.artifactPath && succeeded ? `Built ${import_node_path2.default.basename(parsed.artifactPath)}` : command === "view" && parsed.launched ? "Aetheris: opened in Cadmata" : command === "verify" && succeeded ? "Aetheris: verification complete" : errors > 0 || result.exitCode !== 0 ? `Aetheris: ${errors || parsed.diagnostics.length} problem(s)` : "Aetheris: Valid";
      if (!quiet || result.exitCode !== 0) vscode.window.setStatusBarMessage(message, 5e3);
    } catch (error) {
      const message = isMissingExecutableError(error) ? "Aetheris CLI was not found. Install Aetheris or set 'aetheris.executablePath'." : `Aetheris ${command} failed: ${error instanceof Error ? error.message : String(error)}`;
      output.appendLine(message);
      if (!quiet) void vscode.window.showErrorMessage(message);
    }
  };
  for (const command of ["validate", "build", "view", "verify"]) {
    context.subscriptions.push(vscode.commands.registerCommand(`aetheris.${command}`, () => execute(command)));
  }
  context.subscriptions.push(
    vscode.workspace.onDidSaveTextDocument((document) => {
      if (shouldValidateOnSave(
        document.languageId,
        vscode.workspace.getConfiguration("aetheris", document.uri).get("validateOnSave", true),
        vscode.workspace.isTrusted
      )) {
        void execute("validate", document, true);
      }
    })
  );
}
function deactivate() {
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  activate,
  deactivate
});
