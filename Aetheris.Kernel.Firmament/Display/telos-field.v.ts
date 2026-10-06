@space(clip.position) type ClipPosition = float4;
@material @binding(0)
record FieldFrame { tint: float4; roughness: f32; }
stream Resources { @binding(0) frame: FieldFrame; }
stream Input {
    @location(0) position: float3;
    @location(1) origin: float3;
    @location(2) delta: float3;
    @location(3) depthRow: float4;
    @location(4) wRow: float4;
    @location(5) epsilon: f32;
    @location(6) metallic: f32;
}
stream Varyings {
    @builtin(position) position: ClipPosition;
    @location(0) origin: float3;
    @location(1) delta: float3;
    @location(2) depthRow: float4;
    @location(3) wRow: float4;
    @location(4) epsilon: f32;
    @location(5) metallic: f32;
}
stream Output { @target(0) color: float4; @builtin(frag_depth) depth: f32; }
@vertex function VertexMain(input: Input): Varyings {
    return { position: float4(input.position, 1.0), origin: input.origin, delta: input.delta, depthRow: input.depthRow, wRow: input.wRow, epsilon: input.epsilon, metallic: input.metallic };
}
@pixel function PixelMain(input: Varyings, resources: Resources): Output {
    const length: f32 = Sqrt(input.delta.x * input.delta.x + input.delta.y * input.delta.y + input.delta.z * input.delta.z);
    const dx: f32 = input.delta.x / length;
    const dy: f32 = input.delta.y / length;
    const dz: f32 = input.delta.z / length;
    var t: f32 = 0.0;
    for (var i: u32 = 0; i < 512; i = i + 1) {
        const x: f32 = input.origin.x + t * dx;
        const y: f32 = input.origin.y + t * dy;
        const z: f32 = input.origin.z + t * dz;
        const d: f32 = Field(x, y, z);
        if (Abs(d) < input.epsilon) {
            const h: f32 = input.epsilon * 2.0;
            const gx: f32 = Field(x+h,y,z)-Field(x-h,y,z);
            const gy: f32 = Field(x,y+h,z)-Field(x,y-h,z);
            const gz: f32 = Field(x,y,z+h)-Field(x,y,z-h);
            const n: f32 = Max(Sqrt(gx*gx+gy*gy+gz*gz),0.00000001);
            const light: f32 = 0.28 + 0.72 * Abs((gx*0.287347886+gy*0.766261029+gz*0.574695772)/n);
            const s: f32 = Min(Abs((gx*0.161+gy*0.432+gz*0.887)/n),1.0);
            const s2: f32 = s*s;
            const s4: f32 = s2*s2;
            const s8: f32 = s4*s4;
            const s16: f32 = s8*s8;
            const s32: f32 = s16*s16;
            const s64: f32 = s32*s32;
            const rough: f32 = Min(Max(resources.frame.roughness,0.04),1.0);
            const spec: f32 = (s64*(1.0-rough)+s2*rough)*(0.05+Min(Max(input.metallic,0.0),1.0)*0.2);
            const clipZ: f32 = input.depthRow.x*x+input.depthRow.y*y+input.depthRow.z*z+input.depthRow.w;
            const clipW: f32 = input.wRow.x*x+input.wRow.y*y+input.wRow.z*z+input.wRow.w;
            return { color: float4(Sqrt(Max(resources.frame.tint.x*light+spec,0.0)),Sqrt(Max(resources.frame.tint.y*light+spec,0.0)),Sqrt(Max(resources.frame.tint.z*light+spec,0.0)),resources.frame.tint.w), depth: clipZ/clipW };
        }
        t = t + Abs(d) * 0.9;
        if (t > length) { break; }
    }
    Discard();
    return { color: float4(0.0,0.0,0.0,0.0), depth: 1.0 };
}
