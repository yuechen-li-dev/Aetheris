# DFM summary

M3 retains positive-thickness, minimum R/t, cut-to-bend, cut-to-edge, and flat-overlap rules and adds semantic corner-resolution/relief sizing findings. Cuts reaching a region boundary or bend zone reject during compilation with `sheetmetal-cut-crosses-bend`. Duplicate flange ownership and disconnected graph references are typed failures.

Electronics tray: all four R/t checks, four corner-resolution checks, two holes, two slots, and overlap check pass under the experimental policy. CTC-03: R/t = 3.333 on all seven bends; four auto-relief corner checks, both vent clearance checks, and flat overlap pass. Suggestions remain advisory and are never auto-applied.
