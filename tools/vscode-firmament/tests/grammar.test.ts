import assert from "node:assert/strict";
import fs from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import oniguruma from "vscode-oniguruma";
import textmate from "vscode-textmate";

const { Registry, parseRawGrammar } = textmate;
const { createOnigScanner, createOnigString, loadWASM } = oniguruma;

const root = path.resolve(import.meta.dirname, "..");

async function grammar() {
  const wasm = await fs.readFile(path.join(root, "node_modules", "vscode-oniguruma", "release", "onig.wasm"));
  await loadWASM(wasm.buffer);
  const registry = new Registry({
    onigLib: Promise.resolve({ createOnigScanner, createOnigString }),
    loadGrammar: async (scope) =>
      scope === "source.firmament"
        ? parseRawGrammar(
            await fs.readFile(path.join(root, "syntaxes", "firmament.tmLanguage.json"), "utf8"),
            "firmament.tmLanguage.json",
          )
        : null,
  });
  const loaded = await registry.loadGrammar("source.firmament");
  assert.ok(loaded);
  return loaded;
}

test("critical canonical syntax receives stable TextMate scopes", async () => {
  const loaded = await grammar();
  const source = `Model Plate {
    Concept Path Outline On XY { Start: Point2(0mm, 0mm) }
    Profile Face From Outline
    Hole<Counterbore> Mount { Diameter: 8mm }
    Pattern Mounts Over Specs { ShaftHole(Current) }
    EdgeFinish Rim { Kind: Chamfer }
    Require HoleSize { Expected: 8mm }
    Pmi { HoleDiameter D From HoleSize As HoleDiameter { DatumRefs: [A] } }
    Assert Volume Check { Expected: 123mm^3 }
    InlineStep Source { Path: "part.step" }
    Recognize Source { Region H { Kind: HoleShaft } }
    Replace Source.H With Hole<Shaft> H { End: ThroughAll }
  }`;
  const scopeAt = (needle: string) => {
    const line = source.split("\n").find((value) => value.includes(needle));
    assert.ok(line);
    const token = loaded
      .tokenizeLine(line)
      .tokens.find((value) => value.startIndex <= line.indexOf(needle) && value.endIndex > line.indexOf(needle));
    assert.ok(token, needle);
    return token.scopes;
  };
  assert.ok(scopeAt("Model").includes("keyword.declaration.firmament"));
  assert.ok(scopeAt("Concept Path").includes("keyword.declaration.compound.firmament"));
  assert.ok(scopeAt("Counterbore").includes("entity.name.type.variant.firmament"));
  assert.ok(scopeAt("Diameter:").includes("variable.other.property.firmament"));
  assert.ok(scopeAt("123mm^3").includes("constant.numeric.dimension.firmament"));
  assert.ok(scopeAt("ThroughAll").includes("constant.language.firmament"));
});
