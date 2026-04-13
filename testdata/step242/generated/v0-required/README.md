# STEP242 v0 Generated Corpus — Status Notice

## Summary

This directory contains the original **v0 STEP242 generated corpus** that was produced early in the Aetheris project.

These files were generated freehand by Codex **before the existence of the Aetheris kernel pipeline and CLI analysis tools**.

---

## ⚠️ Important: These files are NOT reliable references

With the introduction of:

- `aetheris build` (kernel-backed generation)
- `aetheris analyze` (geometry + topology inspection)

we have re-evaluated this corpus.

### Findings

Using the current CLI analyzer, we have determined:

- Some files are **geometrically incorrect** relative to their intended primitive.
- Some files are **topologically incomplete** (e.g., open surfaces instead of closed solids).
- Some files are **ambiguous**, where geometry is correct but topology does not match expected solid semantics.

Examples of issues observed:

- A “box” represented as a single planar face instead of a closed volume.
- A “torus” represented as a trimmed surface patch instead of a full toroidal body.
- Cone/cylinder files represented as single open faces rather than volumetric models.

---

## Root Cause

These files were created by directly generating STEP (EXPRESS) structures without:

- a kernel enforcing geometric invariants
- a topology validation layer
- or a reliable inspection tool

STEP is a low-level representation where:
- many invalid or incomplete models still appear superficially valid
- small structural mistakes lead to semantically incorrect geometry

As a result, these files **do not reliably represent correct primitives**.

---

## Current Standard

All authoritative geometry in Aetheris should now be produced and validated through:

- **Firmament → Aetheris kernel → STEP export**
- Verified using:
```

aetheris analyze <file.step> --json

```

The CLI analyzer provides:
- topology validation (manifold / open / non-manifold)
- surface classification
- geometric anchors and parameters
- explicit detection of structural issues

---

## Guidance

### Do NOT:
- Use these files as ground truth for primitives
- Assume they represent correct STEP constructions
- Base new features or tests on these models

### DO:
- Treat this corpus as **historical / diagnostic only**
- Use it as a regression dataset for:
- analyzer validation
- error detection capability
- Generate new reference geometry via the kernel pipeline

---

## Status

This corpus is **deprecated as a reference set**.

It is retained only for:
- debugging
- analyzer testing
- historical comparison

---

## Next Steps

A new **canonical corpus** will be established using:

- Firmament-authored models
- Kernel-validated geometry
- CLI-verified STEP outputs

This will replace the v0 corpus as the authoritative reference.

---

## Final Note

The introduction of `aetheris analyze` revealed that:

> Generating STEP directly without a kernel is not a reliable way to produce correct geometry.

This corpus is a concrete demonstration of that fact.
