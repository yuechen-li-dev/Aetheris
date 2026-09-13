// Browser projection of the public analytical mechanism contract. Inputs carry all dimensions.
export function sliderCrank(radius, length, shaft, bank = 0, phase = 0) {
  if (![radius, length, shaft, bank, phase].every(Number.isFinite) || radius <= 0 || length <= radius)
    throw Error('mechanism-slider-crank-domain');
  const pin = [radius * Math.sin(shaft + phase), radius * Math.cos(shaft + phase), 0];
  const axis = [Math.sin(bank), Math.cos(bank), 0];
  const along = pin[0] * axis[0] + pin[1] * axis[1];
  const px = pin[0] - axis[0] * along, py = pin[1] - axis[1] * along;
  const position = along + Math.sqrt(length * length - px * px - py * py);
  if (!Number.isFinite(position)) throw Error('mechanism-slider-crank-nonfinite-solution');
  const wrist = axis.map(v => v * position);
  return { pin, wrist, position, rodAngle: Math.atan2(wrist[0] - pin[0], wrist[1] - pin[1]) };
}
export function valveLift(phase, opening, duration, maximum, cycle = 720) {
  if (![phase, opening, duration, maximum, cycle].every(Number.isFinite) || cycle <= 0 || duration <= 0 || duration >= cycle || maximum < 0)
    throw Error('mechanism-lift-domain');
  const relative = ((phase - opening) % cycle + cycle) % cycle;
  if (!Number.isFinite(relative)) throw Error('mechanism-lift-nonfinite-phase');
  return relative >= duration ? 0 : maximum * Math.sin(Math.PI * relative / duration) ** 2;
}
