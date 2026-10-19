"""Check rendered canvas pixels independently of DOM/debug counters (requires Pillow)."""
import json
from pathlib import Path
from PIL import Image

output = Path('artifacts/local/concept-wireframes')
report = {}
for product in ('cadmata', 'helios'):
    counts = {}
    for state in ('off', 'gold', 'off-again', 'webgl-gold', 'webgl-off'):
        image = Image.open(output / f'{product}-{state}.png').convert('RGB')
        pixels = getattr(image, 'get_flattened_data', image.getdata)()
        counts[state] = sum(r > 180 and 95 < g < 235 and b < 150 for r, g, b in pixels)
    assert counts['gold'] > counts['off'] + 200, counts
    assert counts['webgl-gold'] > counts['webgl-off'] + 200, counts
    assert abs(counts['off-again'] - counts['off']) < 100, counts
    report[product] = counts
(output / 'pixel-qualification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
