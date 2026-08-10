import type { DisplayScene } from "./displayRenderables";

export interface SceneBounds {
	min: [number, number, number];
	max: [number, number, number];
	center: [number, number, number];
	size: [number, number, number];
	diagonal: number;
	radius: number;
	isValid: boolean;
}

export interface OrthographicCameraFit {
	position: [number, number, number];
	target: [number, number, number];
	zoom: number;
	near: number;
	far: number;
}

interface MutableBounds {
	minX: number;
	minY: number;
	minZ: number;
	maxX: number;
	maxY: number;
	maxZ: number;
	pointCount: number;
}

interface Vec3 {
	x: number;
	y: number;
	z: number;
}

const EMPTY_BOUNDS: SceneBounds = {
	min: [0, 0, 0],
	max: [0, 0, 0],
	center: [0, 0, 0],
	size: [0, 0, 0],
	diagonal: 0,
	radius: 0,
	isValid: false,
};

function createBounds(): MutableBounds {
	return {
		minX: Number.POSITIVE_INFINITY,
		minY: Number.POSITIVE_INFINITY,
		minZ: Number.POSITIVE_INFINITY,
		maxX: Number.NEGATIVE_INFINITY,
		maxY: Number.NEGATIVE_INFINITY,
		maxZ: Number.NEGATIVE_INFINITY,
		pointCount: 0,
	};
}

function includePoint(bounds: MutableBounds, x: number, y: number, z: number): void {
	if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) {
		return;
	}

	bounds.minX = Math.min(bounds.minX, x);
	bounds.minY = Math.min(bounds.minY, y);
	bounds.minZ = Math.min(bounds.minZ, z);
	bounds.maxX = Math.max(bounds.maxX, x);
	bounds.maxY = Math.max(bounds.maxY, y);
	bounds.maxZ = Math.max(bounds.maxZ, z);
	bounds.pointCount += 1;
}

function includeTriplets(bounds: MutableBounds, values: Float32Array): void {
	for (let index = 0; index <= values.length - 3; index += 3) {
		includePoint(bounds, values[index], values[index + 1], values[index + 2]);
	}
}

function finalizeBounds(bounds: MutableBounds): SceneBounds {
	if (bounds.pointCount === 0) {
		return EMPTY_BOUNDS;
	}

	const min: [number, number, number] = [bounds.minX, bounds.minY, bounds.minZ];
	const max: [number, number, number] = [bounds.maxX, bounds.maxY, bounds.maxZ];
	const size: [number, number, number] = [
		bounds.maxX - bounds.minX,
		bounds.maxY - bounds.minY,
		bounds.maxZ - bounds.minZ,
	];
	const center: [number, number, number] = [
		(bounds.minX + bounds.maxX) * 0.5,
		(bounds.minY + bounds.maxY) * 0.5,
		(bounds.minZ + bounds.maxZ) * 0.5,
	];
	const diagonal = Math.hypot(size[0], size[1], size[2]);

	return {
		min,
		max,
		center,
		size,
		diagonal,
		radius: diagonal > 0 ? diagonal * 0.5 : 0.5,
		isValid: true,
	};
}

export function computeDisplaySceneBounds(displayScene: DisplayScene | null): SceneBounds {
	if (!displayScene) {
		return EMPTY_BOUNDS;
	}

	const bounds = createBounds();

	for (const renderable of displayScene.renderables) {
		if (renderable.kind === "AnalyticPatch") {
			includeTriplets(bounds, renderable.previewMesh.positions);
			continue;
		}

		if (renderable.kind === "MeshPatch") {
			includeTriplets(bounds, renderable.mesh.positions);
			continue;
		}

		if (renderable.kind === "WirePatch") {
			for (const wire of renderable.wires) {
				includeTriplets(bounds, wire.points);
			}
		}
	}

	return finalizeBounds(bounds);
}

export function sceneBoundsFromExtents(minimum: number[], maximum: number[]): SceneBounds {
	if (
		minimum.length !== 3 ||
		maximum.length !== 3 ||
		[...minimum, ...maximum].some((value) => !Number.isFinite(value))
	)
		return EMPTY_BOUNDS;
	const bounds = createBounds();
	includePoint(bounds, minimum[0], minimum[1], minimum[2]);
	includePoint(bounds, maximum[0], maximum[1], maximum[2]);
	return finalizeBounds(bounds);
}

function normalize(vector: Vec3): Vec3 {
	const length = Math.hypot(vector.x, vector.y, vector.z);
	if (length <= 1e-9) {
		return { x: 0, y: 0, z: 1 };
	}

	return {
		x: vector.x / length,
		y: vector.y / length,
		z: vector.z / length,
	};
}

function cross(a: Vec3, b: Vec3): Vec3 {
	return {
		x: a.y * b.z - a.z * b.y,
		y: a.z * b.x - a.x * b.z,
		z: a.x * b.y - a.y * b.x,
	};
}

function dot(a: Vec3, b: Vec3): number {
	return a.x * b.x + a.y * b.y + a.z * b.z;
}

function getBoundsCorners(bounds: SceneBounds): Vec3[] {
	const [minX, minY, minZ] = bounds.min;
	const [maxX, maxY, maxZ] = bounds.max;

	return [
		{ x: minX, y: minY, z: minZ },
		{ x: minX, y: minY, z: maxZ },
		{ x: minX, y: maxY, z: minZ },
		{ x: minX, y: maxY, z: maxZ },
		{ x: maxX, y: minY, z: minZ },
		{ x: maxX, y: minY, z: maxZ },
		{ x: maxX, y: maxY, z: minZ },
		{ x: maxX, y: maxY, z: maxZ },
	];
}

export function computeOrthographicCameraFit(
	bounds: SceneBounds,
	frustumWidth: number,
	frustumHeight: number,
	viewDirection: [number, number, number] = [1, 1, 1],
): OrthographicCameraFit | null {
	if (!bounds.isValid || frustumWidth <= 0 || frustumHeight <= 0) {
		return null;
	}

	const target: Vec3 = { x: bounds.center[0], y: bounds.center[1], z: bounds.center[2] };
	const forward = normalize({ x: viewDirection[0], y: viewDirection[1], z: viewDirection[2] });
	const worldUp = Math.abs(forward.y) > 0.95 ? { x: 0, y: 0, z: 1 } : { x: 0, y: 1, z: 0 };
	const right = normalize(cross(worldUp, forward));
	const up = normalize(cross(forward, right));

	let projectedHalfWidth = 0;
	let projectedHalfHeight = 0;

	for (const corner of getBoundsCorners(bounds)) {
		const relative = {
			x: corner.x - target.x,
			y: corner.y - target.y,
			z: corner.z - target.z,
		};

		projectedHalfWidth = Math.max(projectedHalfWidth, Math.abs(dot(relative, right)));
		projectedHalfHeight = Math.max(projectedHalfHeight, Math.abs(dot(relative, up)));
	}

	const margin = 1.15;
	const zoom = Math.max(
		0.01,
		Math.min(
			frustumWidth / (Math.max(projectedHalfWidth * 2, 1e-3) * margin),
			frustumHeight / (Math.max(projectedHalfHeight * 2, 1e-3) * margin),
		),
	);
	const distance = Math.max(bounds.radius * 4, 6);

	return {
		position: [
			target.x + forward.x * distance,
			target.y + forward.y * distance,
			target.z + forward.z * distance,
		],
		target: [target.x, target.y, target.z],
		zoom,
		near: 0.01,
		far: Math.max(distance + bounds.radius * 8, 100),
	};
}
