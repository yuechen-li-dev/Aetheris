# CIR-F8.9 — internal trim identity tokens for coedge pairing promotion

CIR-F8.9 adds **internal-only dry-run identity tokens** to pairing evidence so planned edge uses from opposite face patches can be paired deterministically when they represent the same future trim interaction.

## SEM-A0 status

Preserved. Tokens are internal topology-planning evidence only. They are not selector names, user-facing topology IDs, PMI topology references, or stable external API.

## Token model

`InternalTrimIdentityToken` is attached optionally to each `PlannedEdgeUse` and carries canonicalized interaction evidence:

- operation key (`ReplayOpIndex` when available, otherwise `op:unknown`)
- canonical surface-side keys (`SurfaceAKey`, `SurfaceBKey`) with deterministic ordinal ordering
- trim curve family
- interaction role (retention role)
- deterministic ordering key

## Promotion rules

- Token emitted only for exact/special-case loop descriptors and non-deferred trim families.
- Deferred/unsupported/algebraic trim keeps token absent with explicit diagnostic.
- Pairing groups by token ordering key.
- Group size `== 2`: emits `SharedTrimIdentity` pairing with `ExactReady` when both exact, otherwise `SpecialCaseReady`.
- Group size `> 2`: deferred with explicit ambiguous multiplicity diagnostic.
- Missing token or unmatched token remains deferred.

## Expected dry-run outcomes

- **Box - Cylinder**: at least some pairings can promote via shared identity token.
- **Box - Sphere**: circular interactions can carry tokens and promote when both sides align.
- **Box - Torus**: deferred algebraic/quartic interactions remain deferred with no token promotion.

## Out of scope (unchanged)

- no BRep emission
- no trim solving
- no STEP behavior change
- no boolean behavior expansion
- no topology naming generation

## Recommended next step

Carry identity token provenance into future topology assembly planning so token-backed pairings can become candidate coedge assembly records while preserving deferred diagnostics for unresolved identities.
