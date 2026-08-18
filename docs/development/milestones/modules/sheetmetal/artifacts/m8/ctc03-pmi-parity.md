# CTC-03 PMI and datum audit

## Preserved source evidence

The AP242 file contains six instantiated `DATUM_FEATURE` entities, datums labelled A–F, six instantiated `DATUM_SYSTEM` entities, eight `PLUS_MINUS_TOLERANCE` entities, six instantiated `POSITION_TOLERANCE` entities, nine dimensional-size entities, and one dimensional-location entity. The raw model contains 416 PMI-indicator occurrences recognized by the current CLI audit.

Source dimensional evidence used during reconstruction includes nominal inch callouts for Ø0.438, Ø0.625, Ø2.000, Ø1.500, sheet thickness 0.075, and Ø1.065; the final Firmament stores their source-derived metric values. The source STEP remains the evidence authority and is not modified.

## Inferred authored semantics

The M8 program adds three construction datums (`RearMountCenter`, `FrontMountCenter`, and `ServiceHoleCenter`) and pattern relationships inferred from repeated geometry. These are authoring aids, not claims that the AP242 file explicitly named the same construction datums. Equal size and pitch constraints are likewise geometric/authoring interpretations.

## Parity limitation

The current importer detects and inventories PMI-bearing AP242 entities but does not lower the complete semantic GD&T graph into editable Firmament tolerance objects or export equivalent PMI on regenerated STEP. M8 therefore preserves source PMI as audited evidence and preserves nominal dimensions in the engineering program, but does **not** claim PMI/GD&T round-trip parity.
