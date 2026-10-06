struct TemporalFrame {
    inverseCurrent: mat4x4<f32>,
    previous: mat4x4<f32>,
    inversePrevious: mat4x4<f32>,
    // width, height, history valid, baseline=1 / utility=2
    state: vec4<f32>,
    // debug: 0 color, 1 policy, 2 confidence, 3 motion, 4 depth
    debug: vec4<f32>,
}
@group(0) @binding(0) var currentColor: texture_2d<f32>;
@group(0) @binding(1) var currentDepth: texture_depth_2d;
@group(0) @binding(2) var historyColor: texture_2d<f32>;
@group(0) @binding(3) var historyDepth: texture_depth_2d;
@group(0) @binding(4) var historySampler: sampler;
@group(0) @binding(5) var<uniform> temporal: TemporalFrame;

@vertex fn resolveVertex(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32> {
    let positions = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));
    return vec4<f32>(positions[index], 0.0, 1.0);
}
fn worldAt(uv: vec2<f32>, depth: f32, inverse: mat4x4<f32>) -> vec3<f32> {
    let homogeneous = inverse * vec4<f32>(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0, depth, 1.0);
    return homogeneous.xyz / homogeneous.w;
}
fn luminance(color: vec3<f32>) -> f32 {
    return dot(color, vec3<f32>(0.2126, 0.7152, 0.0722));
}
@fragment fn resolveFragment(@builtin(position) position: vec4<f32>) -> @location(0) vec4<f32> {
    let size = temporal.state.xy;
    let pixel = vec2<i32>(position.xy);
    let uv = position.xy / size;
    let depth = textureLoad(currentDepth, pixel, 0);
    let current = textureLoad(currentColor, pixel, 0);
    let world = worldAt(uv, depth, temporal.inverseCurrent);
    let pixelWorld = max(length(worldAt(uv + vec2<f32>(1.0 / size.x, 0.0), depth, temporal.inverseCurrent) - world), 0.0000001);
    var low = current.rgb;
    var high = current.rgb;
    var edgeRisk = 0.0;
    // Nine bounded samples. RGB min/max protects hard CAD color boundaries.
    for (var y = -1; y <= 1; y++) {
        for (var x = -1; x <= 1; x++) {
            let neighbor = clamp(pixel + vec2<i32>(x, y), vec2<i32>(0), vec2<i32>(size) - vec2<i32>(1));
            let color = textureLoad(currentColor, neighbor, 0).rgb;
            low = min(low, color);
            high = max(high, color);
            let nd = textureLoad(currentDepth, neighbor, 0);
            let nuv = (vec2<f32>(neighbor) + vec2<f32>(0.5)) / size;
            // Compare at the same ray to distinguish a depth jump from pixel spacing.
            let separation = length(worldAt(nuv, nd, temporal.inverseCurrent) - worldAt(nuv, depth, temporal.inverseCurrent));
            edgeRisk = max(edgeRisk, clamp(separation / (pixelWorld * 4.0), 0.0, 1.0));
        }
    }
    let previousClip = temporal.previous * vec4<f32>(world, 1.0);
    let previousNdc = previousClip.xyz / previousClip.w;
    let previousUv = vec2<f32>(previousNdc.x * 0.5 + 0.5, 0.5 - previousNdc.y * 0.5);
    let valid = temporal.state.z > 0.5 && depth < 1.0 && previousClip.w > 0.0
        && all(previousUv >= vec2<f32>(0.0)) && all(previousUv < vec2<f32>(1.0))
        && previousNdc.z >= 0.0 && previousNdc.z <= 1.0;
    // Unconditional sampling keeps derivatives uniform. Invalid correspondence is never trusted.
    let history = textureSampleLevel(historyColor, historySampler, previousUv, 0.0);
    let previousPixel = clamp(vec2<i32>(previousUv * size), vec2<i32>(0), vec2<i32>(size) - vec2<i32>(1));
    let oldDepth = textureLoad(historyDepth, previousPixel, 0);
    let oldUv = (vec2<f32>(previousPixel) + vec2<f32>(0.5)) / size;
    let oldWorld = worldAt(oldUv, oldDepth, temporal.inversePrevious);
    let expectedWorld = worldAt(oldUv, previousNdc.z, temporal.inversePrevious);
    let depthError = length(oldWorld - expectedWorld) / pixelWorld;
    var depthConfidence = 0.0;
    if (valid && oldDepth < 1.0) { depthConfidence = 1.0 - clamp(depthError / 2.0, 0.0, 1.0); }
    let motionPixels = length((previousUv - uv) * size);
    // Allow sample jitter (<1 px); trust drops to zero by 8 px/frame.
    let motionConfidence = 1.0 - clamp((motionPixels - 1.0) / 7.0, 0.0, 1.0);
    let colorConfidence = 1.0 - clamp(abs(luminance(history.rgb) - luminance(current.rgb)) / 0.15, 0.0, 1.0);
    var decision = vec3<f32>(0.0);
    if (temporal.state.w == 1.0) {
        // Deliberately conventional baseline: fixed weight, depth rejection, same clamp.
        if (depthConfidence > 0.8) { decision = vec3<f32>(2.0, 0.5, depthConfidence); }
    } else {
        decision = telosPolicy(depthConfidence, motionConfidence, colorConfidence, edgeRisk);
    }
    let clipped = clamp(history.rgb, low, high);
    // Clamp all accepted policies; stable history is never allowed outside the local color range.
    var resolved = mix(current.rgb, clipped, decision.y);
    if (temporal.debug.x == 1.0) {
        resolved = vec3<f32>(1.0, 0.0, 0.0);
        if (decision.x == 1.0) { resolved = vec3<f32>(0.0, 1.0, 0.0); }
        if (decision.x == 2.0) { resolved = vec3<f32>(1.0, 1.0, 0.0); }
        if (decision.x == 3.0) { resolved = vec3<f32>(0.0, 0.4, 1.0); }
    }
    if (temporal.debug.x == 2.0) { resolved = vec3<f32>(decision.z); }
    if (temporal.debug.x == 3.0) { resolved = vec3<f32>(1.0 - motionConfidence); }
    if (temporal.debug.x == 4.0) { resolved = vec3<f32>(1.0 - depthConfidence); }
    return vec4<f32>(resolved, current.a);
}
