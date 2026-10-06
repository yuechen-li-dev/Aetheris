# Three Telos display

Cadmata's normal model/assembly view and Helios share the Three Telos WebGPU host. It owns the camera, grid, mesh drawing, semantic topology lines, selection projection and GPU resources. A supported browser/GPU is required for that path.

When WebGPU is unavailable, both products label their temporary WebGL fallback. Cadmata's semantic authoring and PMI views also retain a labelled WebGL path while their annotation/selection adapter is migrated. Full X0 product acceptance is pending that migration.

Three Telos supports direct WGSL field artifacts, with a real CIR cylinder/mixed-depth browser smoke witness. Automatic CIR product packet construction and default field rendering are future CIR-DISPLAY-X1B work. The [release report](../release/THREE-TELOS-X0.md) records exact qualification and reproduction commands.
