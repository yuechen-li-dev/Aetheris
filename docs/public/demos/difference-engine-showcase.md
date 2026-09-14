# Difference Engine showcase

The [showcase README](../../../demos/Aetheris.DifferenceEngine.Showcase/README.md) is the runnable entry point, including regeneration and the admitted 80–100 mm digit pitch configuration.

Aetheris supplies the exact hierarchical machine: reusable digit wheels, digit modules, carry modules, registers, transfer banks, frame and crank. The Gear Standard Library owns tooth geometry. Include, Subassembly, Expose and typed Gear/Revolute/Fixed interfaces establish the real assembly. Shared display meshes preserve the same occurrence IDs as the compiler and AP242 rest assembly.

The Three.js site supplies numbered wheel overlays, lighting, cameras, deterministic prescribed motion, carry highlighting, transparency and hierarchy-aware explosion. Demonstration programs include squares, triangular numbers and the chained carry 0099 → 0100. Public copy is provisional. The design is inspired by Babbage; it is not a historical reconstruction.

## Visualization and analysis

**Browser visualizations communicate. Headless analysis validates.**

Presentation arithmetic is deliberately prescribed. Future contact, clutch, torque, interference and mechanism qualification belongs in dedicated headless engineering tools, not the public browser scene. The earlier indexed-storage qualification and rejected physical transfer candidate remain independent research; this showcase does not change their verdict.

## Production assets

`Run.ps1 -NoServe` creates an ignored `dist/` with relative URLs, local Three.js dependencies and its license, complete STEP, shared meshes and presentation manifest. Serve that directory with `serve.py` for the local production test. The checked-in `wrangler.jsonc` names the static asset directory for a future Cloudflare Workers deployment; deployment requires a separate explicit request. No worker backend, account registration or deployment is performed.

The hero preview is captured from the real browser into `dist/preview.png` during visual review. On later regeneration `Prepare-Viewer.ps1` reuses `review/assembly-hero.png` if present; regenerate that capture when the design changes. Do not substitute synthetic machine geometry or an AI image.

## Authoring findings

Nested reusable assemblies must retain parent-relative local transforms. Reapplying definition-root transforms at each nested level doubles offsets; the canonical nested-occurrence witness covers a translated and rotated hierarchy and checks mesh/STEP realization. Anchor-owner parts should be authored at the module origin; a semantic datum alone does not relocate the solver's anchor owner. Positioned stock templates keep offset geometry explicit while the anchor occurrence remains at zero. Extrusion bounds use explicit `Zlow`/`Zhigh` parameters in this bounded author.
