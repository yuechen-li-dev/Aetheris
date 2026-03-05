# M39 UI Probe + Plan (shadcn/ui adoption)

## Probe findings
- Frontend uses **React 19 + TypeScript + Vite 7** (`vite` scripts + `vite.config.ts`).
- Tailwind was **not** previously configured (no Tailwind/PostCSS config and no `@tailwind` directives before this change).
- Existing UI layer was custom React + handwritten CSS (`src/App.css`, `src/index.css`), with no MUI/Chakra/AntD/Bootstrap dependency.
- There is a minimal global baseline in `src/index.css` (font family, box-sizing, body defaults).
- Existing design tokens were limited and not centralized as CSS custom properties.
- Routing/state libraries that affect layout are not present (single `App.tsx` stateful view, no router package).

## Decision
Because Tailwind was absent, this change follows the **minimal Tailwind + shadcn path**:
1. Add Tailwind + PostCSS config for Vite.
2. Add shadcn-style component registry/config (`components.json`).
3. Add only foundational shadcn primitives (`Button`, `cn` utility).
4. Apply a skin-only luxury-minimal pass to top bar, left panel, and viewport container.

## Scope guardrails honored
- Client-only changes under `aetheris.client`.
- No backend contracts, routes, or API behavior modified.
- No kernel project changes.
- Styling and component-surface polish only (no feature additions).
