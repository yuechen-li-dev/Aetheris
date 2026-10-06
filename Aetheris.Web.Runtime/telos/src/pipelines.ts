import type { TelosDevice } from "./device.js";
import type { TelosField, TelosDepthMode } from "./contracts.js";
const vertexLayout: GPUVertexBufferLayout = {
  arrayStride: 12,
  attributes: [{ shaderLocation: 0, offset: 0, format: "float32x3" }],
};

/** Cache identity includes shader semantics, drawing state and authoritative attachment formats. */
export function telosPipeline(
  owner: TelosDevice,
  kind: "mesh" | "line" | "overlay-mesh" | "overlay-line",
  artifact?: TelosField["artifact"],
  depthMode?: TelosDepthMode,
  transparent = false,
) {
  const format = owner.format,
    resources = owner.resources;
  const resolvedDepth =
    depthMode ??
    (kind.startsWith("overlay")
      ? "always-on-top"
      : kind.includes("line")
        ? "depth-biased"
        : "depth-tested");
  const id =
    artifact?.shaderId ??
    "telos-" + (kind.includes("mesh") ? "mesh/1" : "line/2");
  const key = JSON.stringify([
    id,
    kind,
    resolvedDepth,
    format,
    "depth32float",
    owner.sampleCount,
    transparent,
    artifact?.vertexEntryPoint ?? "vertex",
    artifact?.fragmentEntryPoint ?? "fragment",
  ]);
  const shader = resources.module(
    id,
    artifact?.wgsl ??
      (kind.includes("mesh") ? owner.shaders.mesh : owner.shaders.line),
  );
  return resources.pipeline(key, () =>
    owner.device.createRenderPipeline({
      label: key,
      layout: "auto",
      vertex: {
        module: shader,
        entryPoint: artifact?.vertexEntryPoint ?? "vertex",
        buffers: artifact
          ? [
              {
                arrayStride: artifact.capabilities.includes(
                  "telos-field-rays/2",
                )
                  ? 76
                  : 72,
                attributes: [
                  ...[0, 1, 2].map((i) => ({
                    shaderLocation: i,
                    offset: i * 12,
                    format: "float32x3" as const,
                  })),
                  { shaderLocation: 3, offset: 36, format: "float32x4" },
                  { shaderLocation: 4, offset: 52, format: "float32x4" },
                  { shaderLocation: 5, offset: 68, format: "float32" },
                  ...(artifact.capabilities.includes("telos-field-rays/2")
                    ? [
                        {
                          shaderLocation: 6,
                          offset: 72,
                          format: "float32" as const,
                        },
                      ]
                    : []),
                ],
              },
            ]
          : kind.includes("mesh")
            ? [
                vertexLayout,
                {
                  arrayStride: 12,
                  attributes: [
                    { shaderLocation: 1, offset: 0, format: "float32x3" },
                  ],
                },
              ]
            : [
                {
                  arrayStride: 24,
                  stepMode: "instance",
                  attributes: [
                    { shaderLocation: 0, offset: 0, format: "float32x3" },
                    { shaderLocation: 1, offset: 12, format: "float32x3" },
                  ],
                },
              ],
      },
      fragment: {
        module: shader,
        entryPoint: artifact?.fragmentEntryPoint ?? "fragment",
        targets: [
          {
            format,
            blend: {
              color: {
                srcFactor: "src-alpha",
                dstFactor: "one-minus-src-alpha",
                operation: "add",
              },
              alpha: {
                srcFactor: "one",
                dstFactor: "one-minus-src-alpha",
                operation: "add",
              },
            },
          },
        ],
      },
      primitive: { topology: "triangle-list", cullMode: "none" },
      multisample: { count: owner.sampleCount },
      depthStencil: {
        format: "depth32float",
        depthWriteEnabled:
          !transparent && !kind.includes("line") && !kind.startsWith("overlay"),
        depthBias:
          resolvedDepth === "depth-biased" && kind.includes("mesh") ? -1 : 0,
        depthCompare:
          resolvedDepth === "always-on-top"
            ? "always"
            : kind.includes("line")
              ? "less-equal"
              : "less",
      },
    }),
  );
}
