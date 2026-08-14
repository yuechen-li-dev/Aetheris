# CTC-03 flat comparison

Generated authored blank: 392.051790 x 612.597761 mm, seven bend lines, two cuts, no overlap, deterministic hash `d812b205f1d6e221c3a493fe5ffb1f599d0c3fb011aedb0662dee28a555c822b`.

Against the accepted M2 recovered flat interpretation:

- width residual: 12.702447 mm;
- height residual: 12.707820 mm;
- contour RMS/p95/max: 52.7763 / 128.1137 / 128.1137 mm;
- cut centers and sizes pass;
- bend-line count delta: zero;
- overlap: false;
- status: `Fail` at the flat-comparison sublevel, producing overall `NeedsReview`.

The mismatch is caused by bounded rectangular/open-corner authored trims and canonical tangent-to-edge flange dimensions not yet reproducing every recovered source outline. The generated flat STEP itself reimports as one enclosed manifold.
