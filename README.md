<p align="center">
<img width="256" height="256" alt="image" src="https://github.com/user-attachments/assets/98eac13c-d7b3-4726-979a-877417499f44" />
</p>

<p align="center"><i>Gordian Knot</i> - virtual sculpture, by Claude 5 Sonnet</p>

<h1 align="center">Aetheris</h1>

## Showcase Demo - THE DIFFERENCE ENGINE STUDY
<img width="2497" height="1279" alt="image" src="https://github.com/user-attachments/assets/e1fe63d5-9ce1-4a84-8e71-6046f2a40df3" />

[See the Aetheris difference, designed/created by GPT-6 Astra.](https://aetheris-difference-engine-showcase.yuechenli.workers.dev/)

## About

Aetheris is a next generation open-source geometric modeling kernel/engineering runtime platform, currently primarily used for mechanical computer aided design (mCAD) applications, similar to Parasolid/ACIS/OpenCascade, developed solo by Yuechen Li with the assistance of GPT-5/6, Claude 4/5, and Gemini 3 from scratch over the course of the year 2026. 

Think of it as the operating system for software such as SolidWorks/Creo/Catia/NX/Fusion 360/Inventor/FreeCAD, and long has it been considered "one of the most complex software engineering challenges, requiring deep expertise in computational geometry, topology, and numerical stability". This repo stands as the testament that it is now a solved problem.

Aetheris ships with Cadmata, a webpage-based viewer. Visual editor support currently in development. 

Aetheris supports a variety of standard modules for different industries and manufacturing processes: sheet metal, weldaments, piping auto-routing, injection molding (alpha), advanced surfacing, wire forming, and virtual sculpting/3D art.

<img width="2506" height="1274" alt="image" src="https://github.com/user-attachments/assets/768ff11d-5c57-4f73-b1ae-fee5223d251c" />

Automated design-for-manufacturing (DFM) checks, finite element analysis (FEA), GD&T style product manufacturing information (PMI),  material/parts database, and 2D drawing/PowerPoint review slide generation are included runtime features.

## Architecture

Aetheris' native representation format is **Firmament**, a domain specific programming language designed specifically for 3D modeling applications. Unlike other CAD software, Aetheris is AI-native and so designed to be **code-first**, enabling full headless usage from the CLI.

Similar to OpenSCAD, the simplified core architecture description of Aetheris is that it's a compiler for 3D object, you can also think of it as LLVM for CAD. Unlike OpenSCAD, Aetheris emits 3D objects in exact boundary representations instead of SCAD's mesh. The native export format of Aetheris is industry standard STEP AP242, enabling full interop with any other 3D parametric CAD software.

Unlike most other vibe coded geometry kernel, C# and .NET are used as the primary development language/runtime from the beginning instead of Rust/C++ due to the strength of the .NET tooling and ecosystem instead of prematurely optimize for theoretical performance. As such, Aetheris is available for download on Nuget [here](https://www.nuget.org/packages/Aetheris.Kernel.Core/).

Aetheris does not use 2D constraint solving as legacy CAD kernels do. Instead, inspired by TypeScript, constraints are defined ahead of time in code and erased at compile time.

Unlike the current industry standard reckless usage of non-uniform rational B-spline (NURBS), Aetheris uses exact analytical geometry as its foundation, the generated STEP artifacts are exact and requires no post-import healing/patching. 

Dangerous operations which may generate degenerate/sliver/zero-dimension geometries are instead blocked from materializing to 3D at a compiler level. 

## Try it

The easiest complete experience is the `Aetheris-2.0.0-preview.3-win-x64.zip` release bundle. It includes the CLI, Cadmata, NativeAOT Forge Host, public documentation, examples, and material catalog. The standalone .NET tool provides the CLI but not Cadmata:

```powershell
dotnet tool install --global Aetheris.CLI --version 2.0.0-preview.3
aetheris --version
aetheris build fixtures/Canonical/PMI/hole-diameter-and-datum.firmament --output out/first-part.step --json
aetheris analyze out/first-part.step --json
```

Go next to [Getting Started](docs/public/getting-started.md), the [public documentation](docs/public/README.md), or the [CLI reference](docs/public/reference/cli.md). 

The [machined mounting block](fixtures/Canonical/Integration/machined-mounting-block.firmament) is the canonical first serious CAD example; 

See the [L-bracket](fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament) for the introductory Sheet Metal example; 

See [cantilever](fixtures/Canonical/FEA/material-resolved-cantilever.firmament) for the analytically interpretable meshless cut-cell FEA witness.

![GPT authored sheet metal](docs/public/assets/sheet-metal-flat-pattern.png)

*A qualified flat-pattern sheet metal artifact, created entirely by GPT 5.6 Sol with human guidance, including formed STEP, flat STEP, SVG, bend identity, material identity, and DFM evidence on the documented Sheet Metal routes.*

```powershell
$host = ".\forge-host\Aetheris.Forge.Host.exe"
& $host list
& $host describe Standard.SheetMetal.ElectronicsEnclosure
python .\samples\forge-interop-x1\python\client.py $host .\samples\forge-interop-x1\request.json .\out\forge-python
```

The bundle contains equivalent [Go, Rust, and TypeScript clients](samples/forge-interop-x1/README.md). Forge Host Protocol v1 is independently versioned from Aetheris `2.0.0-preview.3`.

## Installation surfaces

| Surface | Use | Preview 3 status |
|---|---|---|
| Windows release ZIP | Complete CLI + Cadmata + Forge.Host experience | Qualified on `win-x64` |
| `Aetheris.CLI` .NET tool | Firmament, STEP, Sheet Metal, and FEA commands | Published package; Cadmata not included |
| Public NuGet libraries | Direct .NET integration | 16 version-aligned packages |
| Forge.Host | Language-neutral Template invocation | NativeAOT `win-x64`, Protocol v1 |
| VS Code extension | Firmament syntax and CLI-backed commands | Independently versioned `0.3.0-preview.3` VSIX |

Linux and macOS release binaries are **not** qualified in Preview 3. Framework-level portability tests do not expand the release-binary promise.

## License and acknowledgments

Aetheris code is licensed under the [GNU Affero General Public License v3.0](LICENSE). Alternative licensing is available on request. Third-party assets retain their own terms and provenance; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Special thanks to the National Institute of Standards and Technology (NIST) for STEP AP242 test models used in development and validation. Their presence does not imply NIST endorsement or place those models under the Aetheris license. Stanford Bunny provenance is recorded separately in the third-party notices.

External contributions are welcome under the process in [CONTRIBUTING.md](CONTRIBUTING.md). The proposed contributor license agreement is a **candidate pending human attorney review** and is not yet an operative acceptance mechanism.
