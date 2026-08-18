# M1 performance evidence

Debug net10.0, Stopwatch wall time, 100,000 operations per case. These are smoke measurements, not optimization claims.

| Case | elapsed ms | ns/op |
|---|---:|---:|
| curve-evaluate | 40.743 | 407.4 |
| curve-first-jet | 52.316 | 523.2 |
| expression-first-jet | 51.759 | 517.6 |
| panel-edge-adapter | 40.468 | 404.7 |
| piping-route-adapter | 46.464 | 464.6 |
