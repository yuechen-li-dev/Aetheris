# Firmament VS Code M1 audit

- No existing VS Code extension, TextMate grammar, language configuration, or snippet set was present in Aetheris.
- Canonical `.firmament` fixtures under `fixtures/FirmamentV2/Canonical` are the authoring and grammar-test source of truth.
- The public site used a small hand-written tokenizer in `src/aetheris/FirmamentCode.tsx`; it already grouped keywords, types, fields, dimensions, strings, and comments. The TextMate grammar preserves those conceptual categories and adds structural field/member matching.
- CLI commands already expose JSON. `validate` uses `firmamentV2Validation`; `build` and `view` use command envelopes; `verify` returns its verification report directly. The extension adapts these shapes and never parses human-readable output.
- Current validation and build diagnostic envelopes generally omit source spans even though internal Firmament AST nodes use zero-based character offsets. The extension supports future offset and one-based line/column spans and uses a minimum one-character range when absent.
- `tools/vscode-firmament` is independently buildable/packageable. TSPack owns dependency resolution and run targets; VS Code's official `@vscode/vsce` remains the narrow external packaging boundary.
