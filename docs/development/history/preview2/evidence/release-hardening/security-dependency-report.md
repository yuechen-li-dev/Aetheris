# Security and dependency audit

Release validation records the final NuGet vulnerability audit, frontend
dependency checks, and secret/path scan in `validation-report.md` after artifact
construction.

KernelSDK denies `UNSAFE` extensions by default with a typed diagnostic. Safe
extensions are constrained to Aetheris-provided typed seams by API design; the
remaining in-process CLR restriction is contractual and documented.
