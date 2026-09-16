export function appendCenteredHole(source: string, target = 'Body'): string {
  const closingBrace = findFinalModelBrace(source);
  const suffix = source.slice(closingBrace);
  const prefix = source.slice(0, closingBrace).trimEnd();
  const name = nextHoleName(source);
  return `${prefix}\n    Modify ${target} {\n        Hole<Shaft> ${name} { On: +Z Center: Point2(0mm, 0mm) Diameter: 6mm End: ThroughAll }\n    }\n${suffix.startsWith('}') ? suffix : `}\n${suffix}`}`;
}

function findFinalModelBrace(source: string) {
  let depth = 0;
  let lastTopLevelClose = -1;
  for (let i = 0; i < source.length; i++) {
    if (source[i] === '{') depth++;
    if (source[i] === '}' && --depth === 0) lastTopLevelClose = i;
  }
  if (lastTopLevelClose < 0) throw new Error('A top-level Firmament model block is required.');
  return lastTopLevelClose;
}

function nextHoleName(source: string) {
  let index = 1;
  while (source.includes(`HeliosHole${index}`)) index++;
  return `HeliosHole${index}`;
}
