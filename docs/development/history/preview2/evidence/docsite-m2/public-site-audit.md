# P2-DOCSITE-M2 public-site audit

Audit performed before the Preview 2 rewrite on 2026-08-09.

## Existing architecture

- React 19/Vite SPA with TSPack-managed dependencies and static HTML copies for each Aetheris route.
- 21 Aetheris routes in one hand-authored `content.ts`; custom CSS grid shell, sidebar, rail, mobile drawer, code renderer, and keyword search.
- One synchronization script copied the Preview 1 capability freeze and verified linked fixture paths.
- No MachinaLayout.JS dependency, no site test target, and no lint target in the TSPack manifest.

## Content and navigation findings

- Global and manual branding said Preview 1.
- Navigation covered exact CAD authoring well but did not expose Assembly, FEA, Forge, Continuum, or SurfaceMeshIR as first-class systems.
- SemanticValue, Point/Axis/Plane/Dimension, tolerance propagation, Template-produced assemblies, and the tree-versus-Mate-graph model were absent or scattered.
- The language reference was a short manually duplicated summary rather than the authoritative Firmament V2 reference.
- Status UI read `preview1-capabilities.json`; it could not represent the current language audit plus Preview 2 platform manifest.
- Existing STEP content covered bounded recognition but did not connect imported/native/Forge values through the common semantic contract.
- CLI examples predated current `asm inspect`, `fea`, and SurfaceMeshIR flags.

## Implementation and layout findings

- Useful foundations: stable routes, readable engineering visual language, responsive table wrappers, source attribution, and static route generation.
- Friction: one large content module, manual discriminated-union rendering, no copy button, limited deep-link/reference rendering, and a fixed three-column shell whose rail disappears at tablet width.
- Mobile had a drawer and horizontal code/table overflow, but focus visibility, skip navigation, dialog semantics, long feature matrices, and code toolbar behavior needed work.
- There was no site-local test coverage for routes, headings, feature status, aliases, or synchronized fixture content.

## Disposition

The rewrite preserved the SPA, stable routes, typography, colors, and restrained square-edged engineering identity. It replaced the stale data boundary and navigation model instead of redesigning the entire site.
