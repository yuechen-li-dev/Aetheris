# RULED-TOOLING-A0 Inline STEP probe harness

RULED-ANALYZE-A0 updated analyze/reporting so exact swept surface kinds are recognized in analyzer summaries. `SURFACE_OF_LINEAR_EXTRUSION` imports now appear as `linear-extrusion`, and `SURFACE_OF_REVOLUTION` imports now appear as `surface-of-revolution`.

Open ruled/swept surface bodies still do not have exact volume integration. `aetheris analyze volume` reports that state as unsupported instead of throwing a dictionary-key exception; this milestone did not add ruled-surface area or volume integration.
