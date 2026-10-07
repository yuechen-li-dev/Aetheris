struct State { viewport: vec4<f32>, base: vec4<f32>, accent: vec4<f32>, settings: vec4<f32> }
@group(0) @binding(0) var<uniform> state: State;
struct Output { @builtin(position) position: vec4<f32>, @location(0) uv: vec2<f32> }
@vertex fn vertex(@builtin(vertex_index) index: u32) -> Output {
  let corners = array<vec2<f32>, 3>(vec2(-1,-1), vec2(3,-1), vec2(-1,3));
  let p = corners[index]; return Output(vec4(p,0.99999,1), p * 0.5 + 0.5);
}
fn hash(p: vec2<f32>) -> f32 {
  var q = fract(p * vec2(123.34,456.21)); q += vec2(dot(q,q + vec2(45.32))); return fract(q.x*q.y);
}
fn noise(p: vec2<f32>) -> f32 {
  let i = floor(p); let f = fract(p); let u = f*f*(vec2(3.0)-2.0*f);
  return mix(mix(hash(i),hash(i+vec2(1,0)),u.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),u.x),u.y);
}
fn fbm(p: vec2<f32>) -> f32 { return noise(p)*0.55 + noise(p*2.03+vec2(11.7))*0.28 + noise(p*4.11+vec2(27.2))*0.17; }
fn ring(p: vec2<f32>, radius: f32, width: f32) -> f32 { return exp(-abs(length(p)-radius)/width); }
fn stars(uv: vec2<f32>) -> f32 {
  let cell = uv*state.viewport.xy/3.0; let h = hash(floor(cell));
  return smoothstep(0.995,1.0,h) * exp(-length(fract(cell)-vec2(0.5))*5.0);
}
@fragment fn fragment(input: Output) -> @location(0) vec4<f32> {
  let uv = input.uv; var p = uv*2.0-1.0; p.x *= state.viewport.x/max(state.viewport.y,1.0);
  let mode = u32(state.viewport.z); let accent = state.accent.rgb;
  var color = state.base.rgb;
  switch mode {
    case 0u: { // A graphite cyclorama: quiet centre, crisp silhouette.
      color += accent*exp(-dot(p-vec2(-0.3,0.5),p-vec2(-0.3,0.5))*0.65)*0.10;
      color *= 0.8+0.2*uv.y;
    }
    case 1u: { // Warm gallery paper, restrained grain.
      color = mix(color*0.88, color+accent*0.12, uv.y);
      color += vec3((hash(floor(uv*state.viewport.xy))-0.5)*0.006);
    }
    case 2u: { // Mars: low amber sun above eroded copper strata.
      color += vec3(0.23,0.055,0.02)*smoothstep(0.15,0.95,uv.y);
      let sun = p-vec2(-0.98,0.52);
      color += accent*(exp(-length(sun)*5.0)*0.35 + (1.0-smoothstep(0.047,0.054,length(sun)))*1.2);
      let strata = pow(0.5+0.5*sin(p.y*100.0+fbm(p*2.5)*9.0),16.0);
      color += vec3(0.19,0.047,0.02)*strata*(1.0-smoothstep(-0.8,0.0,p.y));
      color += accent*exp(-abs(p.y+0.56)*10.0)*0.18;
    }
    case 3u: { // Sirius: spectral star, diffraction rays and glacial nebula.
      color += vec3(0.008,0.021,0.055)*uv.y + accent*stars(uv)*0.6;
      let q = p-vec2(1.08,0.58); let r = length(q);
      let rays = exp(-abs(q.x)*95.0)+exp(-abs(q.y)*95.0);
      color += accent*(exp(-r*6.0)*0.3+exp(-r*60.0)*2.0+rays*exp(-r*12.0)*0.5);
      color += vec3(0.015,0.03,0.08)*fbm(p*2.0+vec2(3.0))*smoothstep(0.4,1.5,length(p));
      color += accent*ring(q*vec2(1,1.3),0.27,0.002)*0.16;
    }
    case 4u: { // Singular accretion composition, deliberately outside the model centre.
      let q = (p-vec2(1.10,0.42))*vec2(1,1.45); let r = length(q); let a = atan2(q.y,q.x);
      let disc = ring(q,0.28,0.016)*(0.7+0.3*sin(a));
      color += mix(vec3(0.45,0.055,0.16),accent,0.5+0.5*sin(a))*disc;
      color += vec3(1.0,0.87,0.6)*ring(q,0.273,0.003)*0.85;
      color += vec3(0.15,0.055,0.21)*(ring(q,0.39,0.009)+ring(q,0.51,0.005)*0.5);
      let warp = pow(0.5+0.5*sin(log(r+0.025)*43.0+a*3.0),12.0);
      color += accent*warp*exp(-abs(r-0.31)*8.0)*0.12;
      color *= smoothstep(0.21,0.245,r); color += vec3(0.22,0.16,0.28)*stars(uv)*0.18;
    }
    case 5u: { // Aeons: engraved observatory arcs in violet haze.
      let q = p-vec2(-1.10,-0.74);
      color += accent*(ring(q,0.80,0.002)+ring(q,1.0,0.004)*0.6+ring(q,1.23,0.002)*0.4)*0.35;
      color += vec3(0.08,0.035,0.13)*exp(-abs(length(q)-1.05)*5.0);
      color += accent*stars(uv)*0.35;
      let r = p-vec2(1.0,0.70); let a = atan2(r.y,r.x);
      color += accent*pow(abs(sin(a*12.0)),90.0)*exp(-length(r)*9.0)*0.25;
    }
    case 6u: { // Drafting blueprint: square paper coordinates, no invented model edges.
      color += accent*exp(-dot(p,p)*0.4)*0.04;
      let g = abs(fract(uv*state.viewport.xy/48.0)-0.5);
      let line = smoothstep(0.48,0.5,max(g.x,g.y));
      color += accent*line*0.026;
    }
    default: { // Aurora: layered static curtains, mint light above an indigo horizon.
      let y = p.y-0.45; let wave = sin(p.x*2.0+fbm(vec2(p.x*2.4,1.7))*2.0)*0.20;
      let curtain = exp(-abs(y-wave)*8.0)*(0.25+0.75*fbm(vec2(p.x*14.0,p.y*0.5)));
      color += mix(vec3(0.15,0.07,0.35),accent,0.5+0.5*sin(p.x*2.0))*curtain*0.55;
      color += accent*ring((p-vec2(-1.1,0.55))*vec2(1,1.7),0.1,0.012)*0.3;
      color += vec3(0.35,0.46,0.65)*stars(uv)*0.45;
      color += vec3(0.014,0.02,0.06)*exp(-abs(p.y+0.5)*5.0);
    }
  }
  let vignette = 1.0 - clamp(dot(uv-vec2(0.5),uv-vec2(0.5))*state.settings.x*1.2,0.0,0.5);
  return vec4(clamp(color*state.viewport.w*vignette,vec3(0),vec3(1)),1);
}
