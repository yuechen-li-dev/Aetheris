# Aetheris public libraries

Aetheris is a code-first engineering CAD stack for exact STEP AP242 workflows,
Firmament V2 compilation, typed Forge integration, assemblies, drawings,
analysis, and engineering modules.

The primary integration packages are:

- `Aetheris.Kernel.Core` for exact geometry and STEP APIs;
- `Aetheris.Kernel.Firmament` for the Firmament compiler/runtime;
- `Aetheris.Forge.Host` for ordinary application integration; and
- `Aetheris.Forge.KernelSDK` for advanced extension development.

KernelSDK extensions are Safe by default at the Aetheris capability boundary.
Extensions declaring `UNSAFE` require explicit host consent. Because extensions
execute in-process, this API policy is not a cryptographic CLR sandbox.

Preview 2 targets .NET 10. Documentation and examples are available at
<https://yuechen-li-dev.github.io/aetheris/>.
