# Telos viewport themes

Use **THEME** in Cadmata's viewport toolbar. The choice is remembered locally; `?theme=aurora` (or another theme ID) also selects a theme when opening Cadmata. Switching themes keeps the current camera, geometry, source identities and inspection selection.

| Theme | Treatment | Best use |
|---|---|---|
| Atelier | Graphite desk, neutral metal, balanced task lighting | General engineering work |
| Monument | Warm paper, clay surfaces, soft gallery lighting | Form, architecture and light-background screenshots |
| Blueprint | Cobalt drafting desk, porcelain blue surfaces, measured cyan grid | Topology and trim inspection |
| Mars | Amber low sun, copper strata, oxidized metal | Dramatic warm presentation |
| Sirius | Blue-white star, spectral halo, cold polished hardware | Cool hardware presentation |
| Singularity | Off-centre accretion rings, black stage, ember rim | High-contrast presentation |
| Aeons | Violet observatory, engraved gold arcs, antique metal | Quiet cosmic presentation |
| Aurora | Mint curtains, violet fill, titanium highlights | Colourful presentation |

All eight are static, on-demand WebGPU presentations. There is no continuous animation loop for decoration. Native standalone WGSL supplies one fullscreen backdrop pass; the existing Telos color/depth attachments still own the frame. Mesh lighting includes key/fill directions, hemisphere light, a roughness-dependent highlight, a coloured silhouette rim and tone mapping. It is an artistic lighting approximation, not an environment-map PBR renderer or a physical lighting simulation. Glow is drawn in the procedural background; no HDR bloom, shadow-map or fog postprocessing is claimed.

Minor and major grid lines follow the product palette and retain physical world spacing. The grid is a reference overlay, not a new model boundary. **GRID** still toggles it; Singularity intentionally starts without it. Source BRep edges, selection accents, axes and annotation panels use their product theme colours. Imported standalone parts use the chosen display material; authored assembly material facts remain in the source packet. CIR/SDF fields retain their compiler-generated shading ABI and authored materials, while sharing the theme backdrop, grid and selection treatment. The new mesh lighting does not imply new field shader lighting support.

Helios retains its Mars/Sirius product choices and now projects both into the same native Telos presentation API. The WebGL fallback retains its older, simpler presentation; the eight-look qualification is for WebGPU.

For surface comparison, use [Surface and trim inspection](surface-inspection.md). Its wire and face-isolation modes remain available under every theme. Prefer Atelier, Monument or Blueprint when assessing small details; presentation themes intentionally use stronger colour contrast.

Reusable configuration lives in `@aetheris/three-telos`: `host.setPresentation(TelosPresentation)`. Product adapters project palette and light settings into that contract. Backdrop/grid colours are display encoded, lighting colours are linear. `setPresentation` resets temporal history after an appearance change, retains geometry and camera, and requests an on-demand frame. Each backdrop shares one pipeline per attachment/sample configuration; changing palette does not generate shader programs.

`scripts/qualify-viewport-themes.mts` records actual Cadmata uploads, matched-camera images, stable source-face clicks and bounded pipeline cycling. Supply the Playwright module, local McMaster directory, ignored output directory, host URL and specimen ID. Generated captures belong under `artifacts/local/viewport-themes/`.
