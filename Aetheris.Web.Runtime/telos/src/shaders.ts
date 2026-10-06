export const meshShader = `
struct State { vp: mat4x4<f32>, model: mat4x4<f32>, normal: mat4x4<f32>, color: vec4<f32>, parameters: vec4<f32> }
@group(0) @binding(0) var<uniform> state: State;
struct Output { @builtin(position) position: vec4<f32>, @location(0) normal: vec3<f32> }
@vertex fn vertex(@location(0) position: vec3<f32>, @location(1) normal: vec3<f32>) -> Output {
  var out: Output; out.position = state.vp * state.model * vec4(position, 1);
  out.normal = (state.normal * vec4(normal, 0)).xyz; return out;
}
@fragment fn fragment(input: Output) -> @location(0) vec4<f32> {
  let n = normalize(input.normal); let light = normalize(vec3<f32>(0.3, 0.8, 0.6));
  let diffuse = 0.28 + 0.72 * abs(dot(n, light));
  let roughness = clamp(state.parameters.x, 0.04, 1.0); let metallic = clamp(state.parameters.y, 0.0, 1.0);
  let specular = pow(abs(dot(n, normalize(light + vec3<f32>(0,0,1)))), mix(64.0, 2.0, roughness)) * (0.05 + metallic * 0.2);
  let linear = state.color.rgb * diffuse + vec3<f32>(specular);
  return vec4(pow(max(linear, vec3<f32>(0)), vec3<f32>(1.0 / 2.2)), state.color.a);
}`;

// Expanded segment quads. Width is CSS pixels; bias is 2e-6 in normalized depth.
export const lineShader = `
struct State { vp: mat4x4<f32>, model: mat4x4<f32>, color: vec4<f32>, viewport: vec4<f32> }
@group(0) @binding(0) var<uniform> state: State;
@vertex fn vertex(@builtin(vertex_index) vertex: u32, @location(0) start: vec3<f32>, @location(1) end: vec3<f32>) -> @builtin(position) vec4<f32> {
  var a = state.vp * state.model * vec4(start,1); var b = state.vp * state.model * vec4(end,1);
  // Clip before dividing by w: segments crossing the near plane remain finite.
  if (a.z < 0 && b.z >= 0) { a = mix(a,b, -a.z / (b.z-a.z)); }
  if (b.z < 0 && a.z >= 0) { b = mix(b,a, -b.z / (a.z-b.z)); }
  if (a.z < 0 && b.z < 0) { return vec4<f32>(2,2,2,1); }
  let delta = (b.xy/b.w-a.xy/a.w) * state.viewport.xy;
  let direction = delta / max(length(delta), 0.00001);
  let perpendicular = vec2(-direction.y, direction.x);
  let corners = array<vec2<f32>,6>(vec2(0,-1),vec2(1,-1),vec2(0,1),vec2(0,1),vec2(1,-1),vec2(1,1));
  let corner = corners[vertex]; var p = mix(a,b,corner.x);
  p = vec4(p.xy + perpendicular * corner.y * state.viewport.z / state.viewport.xy * p.w, p.z - 0.000002 * p.w, p.w);
  return p;
}
@fragment fn fragment() -> @location(0) vec4<f32> { return state.color; }
`;
