# Implicit Base identity compatibility

These four original canonical sources predate the shared Profile migration.
Their `Base: Rectangle(width, depth)` field selects the implicit Base identity
family (`region-base`, edge-based flange/bend IDs). Their current counterparts
use `base: blankProfile` and the common rectangular Profile binder.

`SharedProfileOnImplicitBasePreservesLegacyGeometryAndIdentities` compares exact
formed STEP, region/bend/feature identities and flat-pattern hashes. The original
sources were preserved unchanged; do not rebaseline them to hide identity drift.
An explicitly named `Base deck { profile: blank }` retains its named identity
family and is a distinct public authoring choice.
