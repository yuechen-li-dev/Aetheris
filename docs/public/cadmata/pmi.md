# PMI in Cadmata

Cadmata reads semantic AP242 associations rather than inferring requirements from label positions. Users can filter and select datums, dimensions, geometric tolerances, and engineer-authored notes; the selected presentation remains associated with its semantic feature or face.

The authoritative state is the semantic requirement and its target. Label orientation, camera placement, occlusion handling, and screen layout are presentation choices. Preview 3's public native Firmament export subset is documented in the [PMI guide](../firmament/pmi.md); Cadmata can present broader semantic AP242 content where the importer recognizes it, but that does not expand Firmament authoring support.

In WebGPU browsers, Three Telos draws the model and PMI leaders. Readable DOM callouts use the same Telos camera and remain on top of the model. Drag a callout to adjust its presentation offset; its engineering anchor and target stay unchanged. Selecting a callout highlights its associated geometry, and selecting geometry highlights related visible PMI. Datums and GD&T appear by default; DIM and NOTES enable the other categories. Dense views retain the existing bounded collision layout and may hide low-priority notes.
