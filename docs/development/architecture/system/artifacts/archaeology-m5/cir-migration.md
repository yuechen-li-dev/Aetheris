# CIR migration

The `subtract_box_cylinder` Judgment strategy already knows box dimensions/translations, cylinder radius/span, and replay feature identity. It now creates a typed request and executes the through-hole Recipe directly. Two primitive builds plus Boolean operand/root/tool recognition are removed.

The `subtract_box_box` strategy still invokes the bounded compatibility facade because no recognized reusable construction Recipe describes that result. `subtract_box_torus` remains a precise unsupported result. Arbitrary CIR trees still receive `no-strategy-matched`; CIR field composition and intersection witnesses have not become topology authority.
