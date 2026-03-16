# Firmament Identity Invariants

This document defines the identity invariants for the Firmament language
and its compiler pipeline inside Aetheris. These invariants protect the
language from subtle long‑term architectural problems, especially those
related to topological identity, execution artifacts, and compiler
passes.

The goal is to ensure that **source identity remains stable and
meaningful**, even as the compiler and kernel evolve.

------------------------------------------------------------------------

# Core Principle

**Source feature identity is the only language-stable identity.**

Everything else in the pipeline (lowering artifacts, execution
artifacts, kernel topology) is compiler‑internal and must not be treated
as language-level identity unless explicitly exported.

------------------------------------------------------------------------

# Identity Layers

Firmament operates across three conceptual layers.

## 1. Source Layer (Language Level)

Feature identifiers written in `.firmament` files represent **semantic
features defined by the author**.

Examples:

-   `base_plate`
-   `mount_hole`
-   `boss_1`
-   `outer_shell`

These identifiers:

-   belong to the author
-   are stable across compilation
-   are used by validation, selectors, and diagnostics
-   must remain deterministic

These are the **only identifiers the language guarantees to remain
stable**.

------------------------------------------------------------------------

## 2. Compiler Layer (Lowering / Execution)

During compilation, source features are transformed into internal
records such as:

-   parsed operations
-   validated feature records
-   lowering plans
-   execution results

These may introduce:

-   op indices
-   execution ordering
-   intermediate bodies
-   temporary tool geometry

These identifiers are **compiler-owned and ephemeral**.

They must never become language-visible identities unless explicitly
promoted by a feature contract.

Examples of compiler identities:

-   lowering op indices
-   execution result records
-   intermediate boolean tool bodies
-   temporary geometry used during evaluation

These are implementation details and **must remain invisible to the
language model**.

------------------------------------------------------------------------

## 3. Kernel Layer (Geometry / Topology)

The kernel (Aetheris BRep system) produces real geometric artifacts such
as:

-   bodies
-   faces
-   edges
-   vertices
-   topology handles

These artifacts are real geometry but are **not language identities by
default**.

Kernel identifiers must never be exposed directly as Firmament
identifiers unless they are intentionally exported through a
higher-level semantic contract.

This prevents classic CAD issues such as:

-   unstable topology references
-   face identity drift after boolean operations
-   references changing due to rebuild or reorder

------------------------------------------------------------------------

# Required Invariants

The following invariants must hold across the Firmament compiler
pipeline.

## Invariant 1 --- Source IDs are primary

Source feature IDs defined by the author are the **only language-stable
identifiers**.

Compiler and kernel artifacts must not replace them.

------------------------------------------------------------------------

## Invariant 2 --- Lowering IDs are internal

Lowering artifacts such as operation indices or internal records must
never become language-visible identifiers.

They exist only for deterministic compiler execution.

------------------------------------------------------------------------

## Invariant 3 --- Execution artifacts are ephemeral

Bodies created during execution (including tool bodies and intermediate
boolean results) must not be treated as semantic features unless
explicitly assigned a feature ID.

------------------------------------------------------------------------

## Invariant 4 --- Kernel topology is not language identity

Kernel topology handles (faces, edges, vertices) must never be exposed
as stable language identifiers unless promoted intentionally through a
higher-level feature contract.

This prevents topological naming instability from leaking into the
language layer.

------------------------------------------------------------------------

## Invariant 5 --- Selectors start from source features

Selectors must always begin from **source feature identifiers**, never
from execution artifacts or raw kernel topology.

Example conceptual rule:

selector root → source feature id

not

selector root → execution body or topology handle

------------------------------------------------------------------------

## Invariant 6 --- Rebuild does not change feature identity

Compiler rebuild, execution reorder, or optimizer passes may change
internal execution artifacts but must **not change the identity of
source features**.

------------------------------------------------------------------------

# Why This Matters

Without these invariants, CAD systems often develop severe long-term
problems:

-   selectors silently binding to different geometry
-   rebuilds changing feature meaning
-   diagnostics referencing unknown internal identifiers
-   topology naming failures propagating into the language layer
-   incremental compilation becoming impossible

Locking these invariants early prevents those failure modes.

------------------------------------------------------------------------

# Practical Implications for Implementation

Compiler passes should follow these rules:

1.  Always attach execution artifacts back to their source feature ID.
2.  Never expose kernel handles as language identifiers.
3.  Keep execution ordering metadata separate from semantic identity.
4.  Ensure diagnostics reference source features whenever possible.
5.  Only promote execution artifacts to language-visible identities
    through explicit feature contracts.

------------------------------------------------------------------------

# Status

This document records the intended identity invariants for the Firmament
compiler pipeline as of early Firmament development (pre‑M4 milestone).

Future milestones may expand selector systems, feature contracts, or
topology exports, but must preserve the invariants defined here.
