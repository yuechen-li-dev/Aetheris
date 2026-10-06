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
  let linear = select(state.color.rgb * diffuse + vec3<f32>(specular), state.color.rgb, state.parameters.z > 0.5);
  return vec4(pow(max(linear, vec3<f32>(0)), vec3<f32>(1.0 / 2.2)), state.color.a);
}
