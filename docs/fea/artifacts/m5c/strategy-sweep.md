# M5C bounded strategy sweep

| candidate | admission/result |
|---|---|
| Ordinary Q1 + strong | control; canonical 869 PCG, 2.986 mm, 18.6 GPa, diagonal ratio 2.86e10 |
| arbitrary stiffness floor | M5B rejected control; not reintroduced |
| affine Q1 aggregation + strong | canonical production choice; 519 PCG, 14.05 um, 34.72 MPa, diagonal ratio 50.0 |
| affine Q1 aggregation + symmetric Nitsche, gamma 20 | rejected after non-positive PCG curvature at 21 iterations |
| affine Q1 aggregation + symmetric Nitsche, gamma 100 | admitted for bounded traces; X90/Z45 exact BC max 1.27e-8 m and equilibrium 5.43e-6 N |
| gamma 100 on 3.05e-5 minimum-fraction compound | outside validated trace admission; strong fallback selected |
| ghost penalty | not retained; aggregation removed the motivating independent tiny-support carrier without adding a stabilization parameter |
| boundary MPC | not retained; exact weak enforcement is supplied by Nitsche and affine coefficient constraints already provide the bounded multipoint mechanism for aggregation |

The Z31 held-out case was not used to alter the frozen 2% basis crossing or gamma tier. It selects 132 aggregations and Nitsche, producing 10.95 um displacement, 24.30 MPa maximum recovered stress, 3.09e-6 N equilibrium residual, and 1.11e-8 m exact-boundary maximum violation.

