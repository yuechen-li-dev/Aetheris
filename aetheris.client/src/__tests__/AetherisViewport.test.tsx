import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AetherisViewport } from "../viewer/AetherisViewport";
import type { DisplayScene } from "../viewer/displayRenderables";

vi.mock("@react-three/fiber", () => ({
	Canvas: ({ children }: { children: React.ReactNode }) => (
		<div data-testid="canvas">{children}</div>
	),
	useFrame: () => undefined,
	useThree: () => ({
		camera: { position: { x: 0, z: 0 }, zoom: 1 },
		controls: null,
		size: { width: 800, height: 600 },
		gl: { domElement: { addEventListener: vi.fn(), removeEventListener: vi.fn() } },
	}),
}));

vi.mock("@react-three/drei", () => ({
	Line: ({ points }: { points: unknown }) => (
		<div data-testid="line" data-points={JSON.stringify(points)} />
	),
	OrbitControls: () => <div data-testid="orbit" />,
	Text: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
}));

vi.mock("three", async () => {
	const actual = await vi.importActual<typeof import("three")>("three");
	return {
		...actual,
		BufferGeometry: class {
			setAttribute() {
				return this;
			}
			setIndex() {
				return this;
			}
			computeBoundingSphere() {
				return this;
			}
		},
		MeshStandardMaterial: class {},
	};
});

const baseScene: DisplayScene = {
	renderables: [],
	sourceAuthority: "BRep",
	displayAuthority: "DisplayIR",
	lanes: [],
	diagnostics: [],
	status: "Complete",
};

describe("AetherisViewport", () => {
	it("RendersMeshPatch", () => {
		const scene: DisplayScene = {
			...baseScene,
			renderables: [
				{
					kind: "MeshPatch",
					faceId: 1,
					surfaceKind: "Plane",
					status: "Mesh",
					patchKind: "MeshPatch",
					materializationLane: "BoundedMesh",
					diagnostics: [],
					mesh: {
						faceId: 1,
						positions: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
						normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]),
						indices: new Uint32Array([0, 1, 2]),
					},
				},
			],
		};
		expect(() =>
			render(<AetherisViewport displayScene={scene} showGrid={false} showAxisGuide={false} />),
		).not.toThrow();
	});

	it("RendersWirePatchWithoutMeshArrays", () => {
		const scene: DisplayScene = {
			...baseScene,
			renderables: [
				{
					kind: "WirePatch",
					faceId: 2,
					surfaceKind: "Plane",
					status: "WireframeOnly",
					patchKind: "WirePatch",
					materializationLane: "WirePatch",
					diagnostics: [],
					wires: [{ edgeId: 20, points: new Float32Array([0, 0, 0, 1, 0, 0]) }],
				},
			],
		};
		const { getAllByTestId } = render(
			<AetherisViewport displayScene={scene} showGrid={false} showAxisGuide={false} />,
		);
		expect(getAllByTestId("line").length).toBeGreaterThan(0);
	});

	it("AcceptsDiagnosticOnlyFace", () => {
		const scene: DisplayScene = {
			...baseScene,
			status: "Partial",
			renderables: [
				{
					kind: "DiagnosticPatch",
					faceId: 3,
					surfaceKind: "Plane",
					status: "DiagnosticOnly",
					patchKind: "DiagnosticPatch",
					materializationLane: "DiagnosticOnly",
					diagnostics: [
						{
							code: "Viewer.Tessellation.Timeout",
							message: "timeout",
							faceId: 3,
							surfaceKind: "Plane",
							phase: "test",
							suggestedNextAction: null,
						},
					],
				},
			],
		};
		expect(() =>
			render(<AetherisViewport displayScene={scene} showGrid={false} showAxisGuide={false} />),
		).not.toThrow();
	});
});
