export interface LogGridSelection {
	primarySpacing: number;
	secondarySpacing: number;
	primaryWeight: number;
	secondaryWeight: number;
	exponent: number;
	blend: number;
}

export interface GridBounds {
	minX: number;
	maxX: number;
	minZ: number;
	maxZ: number;
}

export interface AdaptiveGridLayer {
	spacing: number;
	weight: number;
	minorPositions: Float32Array;
	majorPositions: Float32Array;
	lineCount: number;
	minorLineCount: number;
	majorLineCount: number;
}

export interface AdaptiveGridPlan {
	bounds: GridBounds;
	layers: readonly AdaptiveGridLayer[];
	lineCount: number;
	segmentCount: number;
	drawCallCount: number;
	allocatedBytes: number;
}

const MIN_SPACING = 1e-6;

export function selectLogarithmicGridScales(
	worldSpan: number,
	targetCellCount = 14,
): LogGridSelection {
	const normalizedSpan = Math.max(worldSpan, MIN_SPACING);
	const normalizedTarget = Math.max(targetCellCount, 1);
	const rawExponent = Math.log10(normalizedSpan / normalizedTarget);
	const exponent = Math.floor(rawExponent);
	const blend = rawExponent - exponent;
	const primarySpacing = 10 ** exponent;
	const secondarySpacing = 10 ** (exponent + 1);

	return {
		primarySpacing,
		secondarySpacing,
		primaryWeight: 1 - blend,
		secondaryWeight: blend,
		exponent,
		blend,
	};
}

function appendLine(
	target: number[],
	ax: number,
	ay: number,
	az: number,
	bx: number,
	by: number,
	bz: number,
) {
	target.push(ax, ay, az, bx, by, bz);
}

function buildLayer(
	bounds: GridBounds,
	requestedSpacing: number,
	weight: number,
	majorStep: number,
	maxLinesPerAxis: number,
	y: number,
): AdaptiveGridLayer {
	const requestedXCount = Math.ceil((bounds.maxX - bounds.minX) / requestedSpacing) + 1;
	const requestedZCount = Math.ceil((bounds.maxZ - bounds.minZ) / requestedSpacing) + 1;
	const stride = Math.max(
		1,
		Math.ceil(Math.max(requestedXCount, requestedZCount) / maxLinesPerAxis),
	);
	const spacing = requestedSpacing * stride;
	const xStart = Math.floor(bounds.minX / spacing);
	const xEnd = Math.ceil(bounds.maxX / spacing);
	const zStart = Math.floor(bounds.minZ / spacing);
	const zEnd = Math.ceil(bounds.maxZ / spacing);
	const minor: number[] = [];
	const major: number[] = [];
	let minorLineCount = 0;
	let majorLineCount = 0;

	for (let xIndex = xStart; xIndex <= xEnd; xIndex += 1) {
		const target = xIndex % majorStep === 0 ? major : minor;
		appendLine(target, xIndex * spacing, y, bounds.minZ, xIndex * spacing, y, bounds.maxZ);
		if (target === major) majorLineCount += 1;
		else minorLineCount += 1;
	}
	for (let zIndex = zStart; zIndex <= zEnd; zIndex += 1) {
		const target = zIndex % majorStep === 0 ? major : minor;
		appendLine(target, bounds.minX, y, zIndex * spacing, bounds.maxX, y, zIndex * spacing);
		if (target === major) majorLineCount += 1;
		else minorLineCount += 1;
	}

	return {
		spacing,
		weight,
		minorPositions: new Float32Array(minor),
		majorPositions: new Float32Array(major),
		lineCount: minorLineCount + majorLineCount,
		minorLineCount,
		majorLineCount,
	};
}

export function buildAdaptiveGridPlan(options: {
	bounds: GridBounds;
	targetCellCount?: number;
	maxLinesPerAxis?: number;
	majorStep?: number;
	y?: number;
}): AdaptiveGridPlan {
	const targetCellCount = options.targetCellCount ?? 14;
	const maxLinesPerAxis = Math.max(4, options.maxLinesPerAxis ?? 48);
	const majorStep = Math.max(2, options.majorStep ?? 5);
	const y = options.y ?? 0.001;
	const worldSpan = Math.max(
		options.bounds.maxX - options.bounds.minX,
		options.bounds.maxZ - options.bounds.minZ,
		MIN_SPACING,
	);
	const selection = selectLogarithmicGridScales(worldSpan, targetCellCount);
	const layers = [
		buildLayer(
			options.bounds,
			selection.primarySpacing,
			selection.primaryWeight,
			majorStep,
			maxLinesPerAxis,
			y,
		),
		buildLayer(
			options.bounds,
			selection.secondarySpacing,
			selection.secondaryWeight,
			majorStep,
			maxLinesPerAxis,
			y,
		),
	].filter((layer) => layer.weight > 0.015);
	const lineCount = layers.reduce((sum, layer) => sum + layer.lineCount, 0);
	const allocatedBytes = layers.reduce(
		(sum, layer) => sum + layer.minorPositions.byteLength + layer.majorPositions.byteLength,
		0,
	);

	return {
		bounds: options.bounds,
		layers,
		lineCount,
		segmentCount: lineCount,
		drawCallCount: layers.reduce(
			(sum, layer) => sum + Number(layer.minorLineCount > 0) + Number(layer.majorLineCount > 0),
			0,
		),
		allocatedBytes,
	};
}
