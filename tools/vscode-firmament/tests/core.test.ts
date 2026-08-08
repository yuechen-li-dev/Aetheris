import assert from "node:assert/strict";
import test from "node:test";
import path from "node:path";
import {
  commandInvocation,
  commandSucceeded,
  developmentInvocation,
  diagnosticRange,
  isMissingExecutableError,
  parseCliJson,
  shouldValidateOnSave,
  sortDiagnostics,
} from "../src/core.ts";

test("constructs safe argument arrays for paths with spaces and Unicode", () => {
  const file = path.resolve("fixtures", "My 部件.firmament");
  assert.deepEqual(commandInvocation("build", file, "C:\\Program Files\\Aetheris\\aetheris.exe"), {
    executable: "C:\\Program Files\\Aetheris\\aetheris.exe",
    args: ["build", file, "--json"],
  });
});

test("uses PATH discovery when executable is not configured", () => {
  assert.equal(commandInvocation("validate", "part.firmament").executable, "aetheris");
});

test("constructs the repository-only dotnet development fallback", () => {
  const invocation = commandInvocation("validate", "part.firmament");
  const fallback = developmentInvocation("Aetheris.CLI/Aetheris.CLI.csproj", invocation);
  assert.equal(fallback.executable, "dotnet");
  assert.deepEqual(fallback.args.slice(0, 4), [
    "run",
    "--project",
    path.resolve("Aetheris.CLI/Aetheris.CLI.csproj"),
    "--",
  ]);
});

test("parses validate diagnostics and removes compiler lifecycle markers", () => {
  const result = parseCliJson(
    JSON.stringify({
      firmamentV2Validation: {
        status: "invalid",
        diagnostics: [
          { code: "firmament-v2-parser-invoked", severity: "warning", message: "firmament-v2-parser-invoked" },
          { code: "E1", severity: "fatal", message: "Bad field", sourceSpan: { start: 8, length: 3 } },
        ],
      },
    }),
    "validate",
  );
  assert.equal(result.success, false);
  assert.deepEqual(
    result.diagnostics.map((item) => item.code),
    ["E1"],
  );
});

test("parses build artifacts and nonzero-style diagnostics", () => {
  const result = parseCliJson(
    JSON.stringify({
      success: false,
      outputPath: "C:/out/part.step",
      diagnostics: [{ source: "FirmamentV2", severity: "Error", message: "unsupported", code: "BUILD1" }],
    }),
    "build",
  );
  assert.equal(result.artifactPath, "C:/out/part.step");
  assert.equal(result.diagnostics[0]?.severity, "error");
});

test("parses view and verify command shapes", () => {
  assert.equal(parseCliJson('{"success":true,"launched":true,"diagnostics":[]}', "view").launched, true);
  assert.equal(
    parseCliJson('{"overallAdmission":"ExternalInspectionPending","artifact":{"path":"x.step"}}', "verify").success,
    true,
  );
});

test("rejects malformed and unexpected CLI JSON", () => {
  assert.throws(() => parseCliJson("not json", "build"), /malformed JSON/);
  assert.throws(() => parseCliJson("{}", "validate"), /firmamentV2Validation/);
});

test("maps one-based line spans and zero-based offsets without off-by-one errors", () => {
  assert.deepEqual(diagnosticRange("abc\ndef", { line: 2, column: 2, endLine: 2, endColumn: 4 }), {
    start: { line: 1, character: 1 },
    end: { line: 1, character: 3 },
  });
  assert.deepEqual(diagnosticRange("abc\ndef", { start: 4, length: 3 }), {
    start: { line: 1, character: 0 },
    end: { line: 1, character: 3 },
  });
  assert.deepEqual(diagnosticRange("", undefined), {
    start: { line: 0, character: 0 },
    end: { line: 0, character: 1 },
  });
});

test("orders diagnostics deterministically", () => {
  const sorted = sortDiagnostics([
    { code: "B", severity: "error", message: "b", sourceSpan: { line: 2, column: 1 } },
    { code: "A", severity: "error", message: "a", sourceSpan: { line: 1, column: 3 } },
  ]);
  assert.deepEqual(
    sorted.map((item) => item.code),
    ["A", "B"],
  );
});

test("validate-on-save triggers only for enabled trusted Firmament documents", () => {
  assert.equal(shouldValidateOnSave("firmament", true, true), true);
  assert.equal(shouldValidateOnSave("firmament", false, true), false);
  assert.equal(shouldValidateOnSave("firmament", true, false), false);
  assert.equal(shouldValidateOnSave("plaintext", true, true), false);
});

test("treats CLI nonzero exit and structured failure as command failure", () => {
  assert.equal(commandSucceeded(1, { success: true, diagnostics: [] }), false);
  assert.equal(commandSucceeded(0, { success: false, diagnostics: [] }), false);
  assert.equal(commandSucceeded(0, { success: true, diagnostics: [] }), true);
});

test("recognizes missing or malformed executable paths from spawn errors", () => {
  const error = Object.assign(new Error("spawn missing"), { code: "ENOENT" });
  assert.equal(isMissingExecutableError(error), true);
  assert.equal(isMissingExecutableError(new Error("other")), false);
});
