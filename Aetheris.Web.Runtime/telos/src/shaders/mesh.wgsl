struct State { vp: mat4x4<f32>, model: mat4x4<f32>, normal: mat4x4<f32>, color: vec4<f32>, parameters: vec4<f32>,
  camera: vec4<f32>, key: vec4<f32>, keyColor: vec4<f32>, fill: vec4<f32>, fillColor: vec4<f32>,
  sky: vec4<f32>, ground: vec4<f32>, rim: vec4<f32>, tuning: vec4<f32>, up: vec4<f32> }
@group(0) @binding(0) var<uniform> state: State;
struct Output { @builtin(position) position: vec4<f32>, @location(0) normal: vec3<f32>, @location(1) world: vec3<f32> }
@vertex fn vertex(@location(0) position: vec3<f32>, @location(1) normal: vec3<f32>) -> Output {
  var out: Output; out.position = state.vp * state.model * vec4(position, 1);
  out.normal = (state.normal * vec4(normal, 0)).xyz; out.world = (state.model * vec4(position,1)).xyz; return out;
}
@fragment fn fragment(input: Output, @builtin(front_facing) front: bool) -> @location(0) vec4<f32> {
  if (state.tuning.w > 0.5 && state.parameters.z < 0.5) {
    let n = select(-normalize(input.normal),normalize(input.normal),front);
    let v = normalize(state.camera.xyz-input.world); let key = normalize(state.key.xyz); let fill = normalize(state.fill.xyz);
    let roughness = clamp(state.parameters.x,0.08,1.0); let metallic = clamp(state.parameters.y,0.0,1.0);
    let hemisphere = mix(state.ground.rgb,state.sky.rgb,dot(n,state.up.xyz)*0.5+0.5)*state.sky.w*0.5;
    let light = hemisphere + vec3(state.tuning.x) + state.keyColor.rgb*max(dot(n,key),0.0)*state.key.w*0.5
      + state.fillColor.rgb*max(dot(n,fill),0.0)*state.fill.w*0.6;
    let spec = pow(max(dot(n,normalize(key+v)),0.0),mix(120.0,4.0,roughness))*mix(0.10,0.55,metallic)*state.key.w;
    let rim = pow(1.0-max(dot(n,v),0.0),3.0)*state.rim.w*0.32;
    let reflectance = mix(vec3(1),state.color.rgb,metallic);
    let c = (state.color.rgb*light + reflectance*state.keyColor.rgb*spec + state.rim.rgb*rim)*state.tuning.y;
    let mapped = clamp((c*(2.51*c+vec3(0.03)))/(c*(2.43*c+vec3(0.59))+vec3(0.14)),vec3(0),vec3(1));
    return vec4(pow(mapped,vec3(1.0/2.2)),state.color.a);
  }
  let n = normalize(input.normal); let light = normalize(vec3<f32>(0.3, 0.8, 0.6));
  let diffuse = 0.28 + 0.72 * abs(dot(n, light));
  let roughness = clamp(state.parameters.x, 0.04, 1.0); let metallic = clamp(state.parameters.y, 0.0, 1.0);
  let specular = pow(abs(dot(n, normalize(light + vec3<f32>(0,0,1)))), mix(64.0, 2.0, roughness)) * (0.05 + metallic * 0.2);
  let linear = select(state.color.rgb * diffuse + vec3<f32>(specular), state.color.rgb, state.parameters.z > 0.5);
  return vec4(pow(max(linear, vec3<f32>(0)), vec3<f32>(1.0 / 2.2)), state.color.a);
}
