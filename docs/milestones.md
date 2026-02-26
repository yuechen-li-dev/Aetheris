# Milestones

## M00 — Project Charter + Repo Skeleton Hardening

- [x] Add kernel core and kernel test scaffolding projects.
- [x] Establish project docs for vision, non-goals, architecture, and numerics policy.
- [x] Set contribution/editor guardrails.
- [x] Ensure CI restores, builds, and tests .NET projects.

## M01 — Diagnostics and Result Model Baseline

- [x] Define a kernel operation result envelope (`KernelResult<T>`) for success/failure with diagnostics.
- [x] Introduce shared kernel diagnostic primitives (severity + stable codes + immutable payload).
- [x] Enforce guardrails for success/failure diagnostic shape (no success errors, failure has at least one error).
- [x] Add focused unit tests that lock result/diagnostic semantics.

## M02 — Tolerance and Numerics Primitives

- [ ] Introduce centralized tolerance primitives and policy-backed defaults.
- [ ] Add foundational numeric utility types required for later geometric operations.
- [ ] Add focused tests that enforce tolerance policy behavior.
