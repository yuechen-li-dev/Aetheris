# Learn Firmament

Six short lessons take you from an empty file to a checked, annotated part and a
two-part assembly. Each lesson builds one complete example, and every example is
a real file under [`fixtures/Canonical/Learn`](../../../fixtures/Canonical/Learn/)
that the test suite builds on every change. If a lesson's code is in this
repository, it compiles.

| Lesson | You will write | New ideas |
|---|---|---|
| [1. A first part](01-first-part.md) | a plate with a hole | `Model`, units, `Box`, `Modify`, `Hole`, the CLI loop |
| [2. Profiles](02-profiles.md) | an L-shaped bracket | construction geometry, `Profile`, loops, `Extrude` |
| [3. Features](03-features.md) | a machined block | `Compose`, `Boss`, `Pocket`, hole variants, `EdgeFinish` |
| [4. Reuse](04-reuse.md) | a family of mounting plates | `Record`, `Static`, `with`, `Feature`, `Set`, `Pattern`, `Template`, `Require` |
| [5. Requirements and checks](05-requirements.md) | a toleranced, self-checking plate | `tol`, `Pmi`, `Assert`, `measure` |
| [6. Assemblies](06-assemblies.md) | a post mounted on a plate | parts, published frames, `Interface<Fixed>` |

Before you start, install the CLI or use a source checkout, as described in
[Getting started](../getting-started.md). The commands below call it
`aetheris`. Run them from the repository root, or from the root of the extracted
Windows bundle, which ships the same `fixtures` folder.

## How to read Firmament

Firmament describes a finished object, not a sequence of commands. A file says
*what the part is*: this much stock, these holes, that chamfer. The compiler
works out the geometry, checks that every request was honoured, and writes exact
STEP AP242. If it cannot do what you asked, it stops and tells you why; it does
not quietly build something else.

Three habits will save you time:

1. **Write units on every length.** `8mm`, not `8`.
2. **Validate before you build.** `validate` is fast and points at the exact
   line that is wrong.
3. **Check the result, not just the exit code.** `measure` and `analyze` report
   what was actually built.

After lesson 6, the [language reference](../reference/language/README.md)
covers each construct in detail, and the [guides](../README.md#guides) cover
specific domains such as sheet metal, FEA and STEP import.
