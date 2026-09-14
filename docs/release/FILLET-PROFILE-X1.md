# FILLET-PROFILE-X1 — reuse the concave fillet section path

Outcome: meaningful progression. A curvature-continuous section now works in the existing bounded concave fillet kernel. The phone's closed polynomial loop is not yet filleted.

`BoundedFilletProfile.CurvatureContinuous` retains the original contacts, topology allocation, admitted edge selections, and shell builder. It changes circular section edges to quintic B-splines and the cylindrical patch to their degree-five by degree-one extrusion. Circular remains the default. The fixed controls have zero endpoint curvature, nonzero speed, and stay in the existing contact rectangle. There is no general equation solver, alternate executor, or post-build repair.

The real CLI compatibility-path witness exports an enclosed-manifold STEP solid: 11 faces (10 planes and one B-spline), 27 edges, 18 vertices. Its topology counts match the circular baseline. Realized pcurve recovery succeeds with maximum residual 2.02e-12 mm. Kernel tests check the realized section's endpoint curvature, contact position, and oriented support tangent planes at five axial stations for three setback values. Existing circular and chained fillet tests still pass.

The next blocker is now an explicit canonical V2 error: `ProfileBoundaryFilletProfileModeUnsupported:CurvatureContinuous:polynomial-contact-shell-required`. The corresponding invalid fixture produces no STEP. The legacy concave kernel is a straight-edge construction; the V2 closed-loop Profile planner assumes line/arc-derived analytic contact curves and patches. Replacing its section equation alone cannot supply contacts and trims on a cubic footprint. No claim is made that the phone plateau-to-body blend is delivered.

See [the capability and limits](../public/firmament/fillet-profiles.md).

STEP SHA-256: `bdb47dea884d742c7c40cf75870162bb37eb8da8184a8b2123f858f33e0065ee`. The realized top-view wireframe was visually inspected.

## Validation

Release solution build passed with zero warnings/errors. All 20 test-project commands completed serially with exit code zero; 3588 tests passed (FrictionLab reports no tests). Source and report link checks passed. Detailed logs remain under `artifacts/local/fillet-profile-x1/`.

## Reproduce

```powershell
dotnet build Aetheris.slnx -c Release
$cli = Resolve-Path Aetheris.CLI/bin/Release/net10.0/aetheris.exe
& $cli build fixtures/Regression/Fillet/concave-curvature-continuous.firmament --out artifacts/local/fillet-profile-x1/concave.step --json
& $cli analyze artifacts/local/fillet-profile-x1/concave.step --json
& $cli wireframe artifacts/local/fillet-profile-x1/concave.step --out artifacts/local/fillet-profile-x1/concave-top.svg --view top --density 2 --samples 128 --json
& $cli build fixtures/Invalid/Fillet/smooth-loop-contact-shell.firmament --out artifacts/local/fillet-profile-x1/unsupported.step --json
```

The last command must fail without writing `unsupported.step`.
