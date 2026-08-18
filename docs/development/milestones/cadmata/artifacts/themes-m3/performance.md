# Theme performance evidence

Measured on the development machine in the in-app Chromium browser at a consistent 1157 × 1216 application window using `fixtures/AssemblyM1/template-block-pair.firmament`, the same fitted orthographic camera, one selected occurrence, and the `?perf` renderer probe. Values are approximate 120–150-frame samples after switching.

| Theme | Average frame | Approx. FPS | Draw calls | Triangles | Geometries | Textures | Shader programs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Atelier | 7.01 ms | 142.7 | 30 | 64 | 30 | 2 | 4 |
| Monument | 7.17 ms | 139.4 | 30 | 64 | 30 | 2 | 5 |
| Mars | 6.93 ms | 144.3 | 31 | 66 | 31 | 2 | 6 |
| Sirius | 7.08 ms | 141.2 | 31 | 66 | 31 | 2 | 6 |
| Singularity | 6.94 ms | 144.0 | 27 | 66 | 27 | 2 | 5 |
| Aeons | 7.12 ms | 140.5 | 31 | 66 | 31 | 2 | 6 |

The procedural layer costs one draw call and two triangles. Singularity disables four adaptive-grid draws, so its total draw count is below baseline. No theme added textures. Browser diagnostics contained no WebGL or shader compilation errors; the only warning was Three.js's upstream `Clock` deprecation from the existing renderer stack.
