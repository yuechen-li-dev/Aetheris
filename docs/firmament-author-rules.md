# Firmament Author Rules

These rules apply to **any LLM or developer generating or editing `.firmament` files** in the Aetheris repository.

The goal is to maintain **stable canonical examples** of the Firmament DSL.

---

# Rule 1 — Never emit JSON

`.firmament` files must not contain JSON object literals.

Forbidden example:
```
{
  "firmament": { "version": "1" },
  "ops": []
}
```
Always use canonical indentation syntax.

---

# Rule 2 — Follow canonical section order

Top-level sections must appear in this order:
```
firmament
model
schema (optional)
ops
pmi (optional)
```
Do not reorder sections.

---

# Rule 3 — Use explicit array length syntax

Arrays must include their length.

### Correct
```
ops[0]:
```
```
size[3]:
  1
  2
  3
```
### Incorrect
```
ops: []
```
```
size: [1,2,3]
```
---

# Rule 4 — Operation entries must use object blocks

Each operation must appear as a block under `ops[n]`.

### Correct
```
ops[1]:
  -
    op: box
    id: base
```
### Incorrect
```
ops:
  box:
    id: base
```
---

# Rule 5 — Always use `op:` field

Operations must explicitly declare the operation type.

### Correct
```
op: box
```
### Incorrect
```
box:
```
---

# Rule 6 — Do not invent new syntax

LLM authors must not introduce:

- new keywords
- alternate field names
- shorthand operation syntax
- JSON literals

Use only documented shapes.

---

# Rule 7 — Preserve canonical examples

Files in:

`testdata/firmament/fixtures/`

serve as language examples.

When editing them:

- maintain canonical formatting
- keep them minimal and readable
- avoid stylistic variation

---

# Rule 8 — If unsure, emit the smallest valid structure

When generating new examples:

1. follow canonical section order
2. emit minimal valid fields
3. avoid guessing additional syntax

---

# Rule 9 — Do not mix languages

`.firmament` files must not include:

- C#
- JSON
- scripting code
- host-language constructs

Comments are not currently supported in `.firmament` files.

---

# Rule 10 — Treat language-shape.md as authoritative

Before generating or modifying `.firmament` source, read:

`docs/firmament-language-shape.md`

That document defines the canonical syntax.
