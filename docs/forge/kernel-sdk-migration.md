# Forge SDK package migration

The former `Aetheris.Forge.Sdk` name mixed two audiences and has been removed before a stable NuGet release made compatibility forwarding necessary.

Use:

- `Aetheris.Forge.Host` for application-side loading, typed Template invocation, compilation, diagnostics, specialization identity, provenance, and generated artifacts.
- `Aetheris.Forge.KernelSDK` for advanced construction/semantic/compiler capability development and Aetheris kernel prototyping.
- `Aetheris.Forge` for the low-level contracts that sit below Firmament; application code should normally reach them through one of the packages above.

Migration:

1. Ordinary host applications replace the project/package reference with `Aetheris.Forge.Host` and change `using Aetheris.Forge.Sdk;` to `using Aetheris.Forge.Host;`.
2. Extension implementations replace the reference with `Aetheris.Forge.KernelSDK`. Existing extension contract namespaces remain under `Aetheris.Forge.*` so capability code does not need a cosmetic namespace rewrite.
3. Generated binding packages reference `Aetheris.Forge.Host`; they should not pull KernelSDK into their consumers.

No compatibility package is retained. The old project had not reached a stable package contract, and a forwarding package would preserve the ambiguous dependency boundary the rename is intended to remove.
