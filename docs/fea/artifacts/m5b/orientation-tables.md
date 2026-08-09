# M5B load and mechanics tables

| case | exact area m2 | area error m2 | force residual N | moment residual N m | min fraction | below 1% | below 5% | below 10% |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline | 1.000e-3 | 0 | 1.82e-12 | 0 | 4.722e-1 | 0 | 0 | 0 |
| Z45 | 1.000e-3 | 1.30e-18 | 1.03e-11 | 3.46e-13 | 8.333e-2 | 0 | 0 | 8 |
| X90/Z45 | 1.000e-3 | 6.51e-19 | 6.43e-12 | 3.42e-13 | 9.259e-3 | 4 | 4 | 4 |
| X15/Y20/Z45 | 1.000e-3 | 1.95e-18 | 1.90e-11 | 9.52e-13 | 3.052e-5 | 124 | 178 | 228 |

Each directory persists AnalysisIR, native results, boundary evidence, sparse metrics, residual history, and Abaqus policy deck.

The `convergence` directory persists the post-change 8x4x1 and 24x12x2 reruns; the 16x8x2 result is `baseline`.
