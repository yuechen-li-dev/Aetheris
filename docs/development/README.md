# Development documentation

This folder is for people working *on* Aetheris. It is not user documentation:
for current behavior, use [`docs/public`](../public/README.md) and the compiler.

| Folder | What is in it |
|---|---|
| [`history/`](history/README.md) | **Start here.** The project's story in nine eras, the [decision log](history/DECISIONS.md), and an [index of every original report](history/ORIGINALS.md) with how to restore it from git. |
| `architecture/` | Durable internal contracts: kernel ([Boolean lessons](architecture/kernel/brep-boolean-lessons.md), [BRep Surgery](architecture/kernel/brep-surgery.md)), [authoring and kernel boundaries](architecture/system/current-authoring-and-kernel-boundaries.md), face orientation, STEP pcurves, imported-hole revisions, the Firmament language service, and the [LLM CAD strategy notes](architecture/llm-cad-strategy/README.md). |
| `reports/2026-10/` | Working reports for the current era (October 2026): language decisions, syntax review, frontend migrations, Helios, Preview 4. Summarized in [era 9](history/eras/09-preview-4-and-consolidation.md); they move into the history when the era closes. |
| `proposals/` | Design proposals that are not implemented. A proposal never grants source admission. |
| `milestones/`, `implementation/`, `history/firmament`, `history/preview2` | Only files that code, tests or scripts read, and evidence beside tracked artifacts. Everything else from these folders is in the history. |

## Rules

- **User-facing behavior is documented in `docs/public` only.** If you change
  behavior, update the public guide in the same change.
- **New work reports** go in `reports/<yyyy-mm>/`. Keep them short: what changed,
  the evidence, the verdict (success, meaningful progression, or honest stop).
- **When an era closes**, summarize its reports into a page under
  `history/eras/`, add lasting decisions to `history/DECISIONS.md`, list the
  originals in `history/ORIGINALS.md`, and delete them. Git keeps them.
- **Do not delete** a file that code, tests or scripts read without moving the
  reference too. `git grep <file name>` outside Markdown before removing anything.
- **Generated output** is local by default. See the
  [generated-artifact policy](GENERATED-ARTIFACT-POLICY.md) before promoting any
  generated result into this tree.

Maintainer build and test policy is in [`AGENTS.md`](../../AGENTS.md) and
[`CONTRIBUTING.md`](../../CONTRIBUTING.md).
