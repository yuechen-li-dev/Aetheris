# Dead, legacy, and incomplete syntax inventory

- `.firmasm`: Deprecated transform-first JSON/YAML compatibility input; deprecation diagnostic is mandatory.
- Firmament V1 TOON-style `op`/`expect` fixtures: Legacy regression language, not V2 canonical style.
- `FirmamentV2TemplateDecl` manufacturing-process AST: Internal/legacy representation used by bounded DFM routes; not canonical generic Template syntax.
- lowercase/alternate PMI record spellings: compatibility aliases; canonical docs use the named V2 records.
- Template-authored Assembly/subassembly application: Future/incomplete; no parser/binder/materializer path.
- native AP242 assembly occurrences and reimport: Future/incomplete; no exporter entities or structural importer result.
- general equation Relations, recognizer-generated axis/plane/dimension semantics, arbitrary kinematics/contact: Future/incomplete.

No branch met the safe-removal bar of parsed + unbound + untested + unused: broad regex paths overlap current compatibility fixtures. Classification is safer than deletion in M1.

