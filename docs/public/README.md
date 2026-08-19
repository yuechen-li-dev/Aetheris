# Aetheris Preview 3 public documentation

This directory is the only authoritative in-repository guide to behavior shipped in Aetheris `2.0.0-preview.3`. It describes what users can do now. Files elsewhere under `docs/` are development history, release material, roadmap notes, or legal-review candidates; use them for their stated purpose, not as the current public contract.

Start with [Getting Started](getting-started.md), then use these guides:

- Firmament: [overview](firmament/overview.md), [language style](firmament/language-style.md), [syntax](firmament/syntax.md), [geometry](firmament/geometry.md), [Circular Sweep](firmament/sweep.md), [Paperclip Template](firmament/paperclip.md), [Templates](firmament/templates.md), [materials](firmament/materials.md), [PMI](firmament/pmi.md), [Sheet Metal](firmament/sheet-metal.md), [FEA](firmament/fea.md), and [STEP import](firmament/step-import.md)
- Interoperability: [Forge Host Protocol v1](forge/interop.md)
- Inspection: [Cadmata](cadmata/overview.md) and [Cadmata PMI](cadmata/pmi.md)
- Reference: [CLI](reference/cli.md), [targets/selectors](reference/targets.md), [diagnostics](reference/diagnostics.md), [supported features](reference/supported-features.md), [known issues](reference/known-issues.md), and [release notes](reference/release-notes.md)

The Windows ZIP begins with the tested [release-bundle walkthrough](release-bundle.md).

All linked example source files are exercised by the public-example qualification tests. Preview 3's reliability rule is simple: a supported requested semantic operation is produced, and an unsupported operation fails with a named diagnostic; successful builds must not silently discard engineering intent.
