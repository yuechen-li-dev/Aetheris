// Offline Khronos validation. Install only in ignored local tools:
// npm install --prefix artifacts/local/presentation-3d-x0/tools gltf-validator@2.0.0-dev.3.10
// node scripts/validate-presentation-glb.cjs <tools/node_modules/gltf-validator> <file.glb> ...
const fs = require('node:fs');
const path = require('node:path');
const validator = require(path.resolve(process.argv[2]));
(async () => {
  let failed = false;
  for (const filename of process.argv.slice(3)) {
    const bytes = fs.readFileSync(filename);
    const report = await validator.validateBytes(new Uint8Array(bytes), { uri: path.basename(filename), maxIssues: 1000 });
    fs.writeFileSync(filename.replace(/\.glb$/i, '.validator.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify({ file: path.basename(filename), validatorVersion: validator.version(), ...report.issues }));
    if (report.issues.numErrors) failed = true;
  }
  if (failed) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
