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
}
stream Varyings {
    @builtin(position) position: ClipPosition;
    @location(0) origin: float3;
    @location(1) delta: float3;
    @location(2) depthRow: float4;
    @location(3) wRow: float4;
    @location(4) epsilon: f32;
}
stream Output { @target(0) color: float4; @builtin(frag_depth) depth: f32; }
@vertex function VertexMain(input: Input): Varyings {
    return { position: float4(input.position, 1.0), origin: input.origin, delta: input.delta, depthRow: input.depthRow, wRow: input.wRow, epsilon: input.epsilon };
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
            const light: f32 = 0.28 + 0.72 * Abs((gx*0.3+gy*0.8+gz*0.6)/n);
            const clipZ: f32 = input.depthRow.x*x+input.depthRow.y*y+input.depthRow.z*z+input.depthRow.w;
            const clipW: f32 = input.wRow.x*x+input.wRow.y*y+input.wRow.z*z+input.wRow.w;
            return { color: float4(resources.frame.tint.x*light,resources.frame.tint.y*light,resources.frame.tint.z*light,resources.frame.tint.w), depth: clipZ/clipW };
        }
        t = t + Abs(d) * 0.9;
        if (t > length) { break; }
    }
    Discard();
    return { color: float4(0.0,0.0,0.0,0.0), depth: 1.0 };
}
