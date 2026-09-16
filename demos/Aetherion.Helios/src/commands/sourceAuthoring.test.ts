import { describe, expect, it } from 'vitest';
import { appendCenteredHole } from './sourceAuthoring';

describe('bounded source authoring', () => {
  it('appends readable Firmament inside the model', () => {
    const result = appendCenteredHole('Model A {\n Box Body { Size: [1mm,1mm,1mm] }\n}\n');
    expect(result).toContain('Modify Body');
    expect(result.indexOf('Modify Body')).toBeLessThan(result.lastIndexOf('}'));
  });
  it('uses the requested semantic target', () => expect(appendCenteredHole('Model A {}', 'Plate')).toContain('Modify Plate'));
  it('allocates deterministic names', () => expect(appendCenteredHole('Model A { // HeliosHole1\n}')).toContain('HeliosHole2'));
  it('rejects text without a model block', () => expect(() => appendCenteredHole('Units: mm')).toThrow(/model block/i));
});
