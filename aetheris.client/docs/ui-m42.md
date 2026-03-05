# UI M42 — Rhythm Layout Discipline

## Step 1 audit snapshot
- Before refactor, `App.tsx` rendered two different trees: `viewer-grid` with left-side cards + secondary viewport, and `main-grid` with viewport left + debug panel right.
- Refactor point: move both tabs into one shared shell with fixed top bar, shared viewport host placement, and shared right tool rail.

## Before/after summary
- Before: viewer and modeling demo each had different main layout containers.
- After: both tabs use one shell (`top-bar` + `main-layout`) with identical viewport location.
- Before: Viewer controls rendered in a left panel.
- After: Viewer controls are right-rail tool sections in this order: STEP Import, STEP Export, Inspector.
- Before: Modeling controls sat in a dark debug panel.
- After: Modeling controls are right-rail tool sections in this order: notice, create box, body list, translate, boolean, debug/status.
- Before: heading typography mixed with different weights/sizes.
- After: section titles use uppercase, lighter weight, and unified spacing rhythm.
- Before: border/radius treatment varied between sections.
- After: rail/cards/viewport/top-bar share consistent border thickness and reduced corner radius.
- Before: spacing varied by section.
- After: layout rhythm is aligned to an 8px scale via CSS variables.

## Final layout rules
- Top bar remains stable across tabs with left title, centered tab switcher, and right global actions.
- Main area is always two columns: centered viewport area + right tool rail.
- No left-side panel column exists in either tab.
- Viewport shell uses the same structure and styling in both tabs.

## Deferred follow-up candidates
- Optional accordion behavior for tool sections was deferred intentionally to avoid feature changes.
- Fine-grained responsive rail collapsing behavior was deferred beyond M42 scope.
