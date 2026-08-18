# Numerics Policy (Direction)

This document sets baseline numeric discipline for upcoming milestones.

## Policy statements

- Centralized tolerance policy: tolerance values must be defined in shared kernel locations.
- No ad hoc epsilon literals in random files.
- Use `ToleranceContext` and `ToleranceMath` helpers for tolerance-aware comparisons instead of inline literals like `1e-6`.
- `double` precision is the default numeric type unless a milestone explicitly introduces alternatives.
- Prefer exact analytic representations where available; use tolerant operations where required by computation.
- Diagnostics and failure modes must be deterministic and observable.
- Kernel APIs should be failure-aware (result/diagnostic-oriented) rather than exception-led for expected operation outcomes.
- Guiding principle: **exact where represented, tolerant where operated**.
