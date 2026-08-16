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

Preview 3 targets .NET 10. Aetheris is licensed under the GNU Affero General
Public License v3.0 (`AGPL-3.0`); third-party assets retain their respective
licenses and provenance. Alternative licensing is available on request.
Documentation and examples are available at
<https://yuechen-li-dev.github.io/aetheris/>.
