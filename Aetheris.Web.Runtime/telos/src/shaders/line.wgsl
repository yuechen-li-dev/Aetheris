struct State { vp: mat4x4<f32>, model: mat4x4<f32>, color: vec4<f32>, viewport: vec4<f32> }
@group(0) @binding(0) var<uniform> state: State;
struct LineOutput {
  @builtin(position) position: vec4<f32>,
  @location(0) lateral: f32,
}
@vertex fn vertex(@builtin(vertex_index) vertex: u32, @location(0) start: vec3<f32>, @location(1) end: vec3<f32>) -> LineOutput {
  var a = state.vp * state.model * vec4(start,1); var b = state.vp * state.model * vec4(end,1);
  // Clip before dividing by w: segments crossing the near plane remain finite.
  if (a.z < 0 && b.z >= 0) { a = mix(a,b, -a.z / (b.z-a.z)); }
  if (b.z < 0 && a.z >= 0) { b = mix(b,a, -b.z / (a.z-b.z)); }
  if (a.z < 0 && b.z < 0) { return LineOutput(vec4<f32>(2,2,2,1), 0.0); }
  let delta = (b.xy/b.w-a.xy/a.w) * state.viewport.xy;
  let direction = delta / max(length(delta), 0.00001);
  let perpendicular = vec2(-direction.y, direction.x);
  let corners = array<vec2<f32>,6>(vec2(0,-1),vec2(1,-1),vec2(0,1),vec2(0,1),vec2(1,-1),vec2(1,1));
  let corner = corners[vertex]; var p = mix(a,b,corner.x);
  let paddedHalfWidth = state.viewport.z * 0.5 + 1.0;
  p = vec4(p.xy + perpendicular * corner.y * (2.0 * paddedHalfWidth) / state.viewport.xy * p.w, p.z - state.viewport.w * p.w, p.w);
  return LineOutput(p, corner.y * paddedHalfWidth);
}
@fragment fn fragment(input: LineOutput) -> @location(0) vec4<f32> {
  let halfWidth = state.viewport.z * 0.5;
  let filterWidth = max(fwidth(input.lateral), 0.0001);
  let coverage = 1.0 - smoothstep(halfWidth - filterWidth * 0.5, halfWidth + filterWidth * 0.5, abs(input.lateral));
  return vec4<f32>(state.color.rgb, state.color.a * coverage);
}
