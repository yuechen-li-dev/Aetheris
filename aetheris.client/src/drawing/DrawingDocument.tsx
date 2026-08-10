import type { DrawingAnnotationIr, DrawingIr, DrawingPageIr } from "./drawingIr";
import { drawingPageLayoutRows } from "./drawingLayout";
import "./drawingDocument.css";

export function DrawingDocument({ drawing }: { drawing: DrawingIr }) {
	return (
		<main className="drawing-document" data-schema={drawing.schemaVersion}>
			{drawing.pages.map((page) => (
				<DrawingPage key={page.pageNumber} drawing={drawing} page={page} />
			))}
		</main>
	);
}

function DrawingPage({ drawing, page }: { drawing: DrawingIr; page: DrawingPageIr }) {
	const revision = drawing.metadata.revision;
	const revisionText = revision ? `${revision.major}.${revision.minor}.${revision.patch}` : "-";
	return (
		<article
			className="drawing-page"
			data-orientation={page.orientation}
			data-machina-layout-rows={drawingPageLayoutRows.length}
			style={{ width: `${page.widthMillimetres}mm`, height: `${page.heightMillimetres}mm` }}
		>
			{page.zoneScheme && <DrawingZones page={page} />}
			<header className="drawing-header">
				<h1>{drawing.metadata.title}</h1>
				<dl>
					<div>
						<dt>Product</dt>
						<dd>{drawing.metadata.productName}</dd>
					</div>
					<div>
						<dt>Part</dt>
						<dd>{drawing.metadata.partNumber ?? "-"}</dd>
					</div>
					<div>
						<dt>Rev</dt>
						<dd>{revisionText}</dd>
					</div>
					<div>
						<dt>Material</dt>
						<dd>{drawing.metadata.material ?? "-"}</dd>
					</div>
				</dl>
			</header>
			<section className="drawing-content">
				{page.views.length > 0 && (
					<svg
						viewBox={`0 0 ${page.widthMillimetres} ${page.heightMillimetres}`}
						aria-label="Projected engineering views"
					>
						{page.views.map((view) => (
							<g key={view.identity} data-view={view.identity}>
								<text x={view.viewport.x} y={view.viewport.y + 3.5}>
									{view.identity}
								</text>
								{view.primitives.map((primitive) => (
									<polyline
										key={primitive.stableId}
										className={primitive.kind.toLowerCase()}
										points={primitive.points.map((p) => `${p.x},${p.y}`).join(" ")}
									/>
								))}
							</g>
						))}
						{page.annotations.map((annotation) => (
							<DrawingAnnotation key={annotation.identity} annotation={annotation} />
						))}
					</svg>
				)}
				{page.tables.map((table) => (
					<table
						key={table.identity}
						data-source-identity={table.sourceIdentity}
						data-table-kind={table.kind}
						data-zone={table.location?.zone}
						style={
							table.bounds && page.contentRect
								? {
										position: "absolute",
										left: `${table.bounds.x - page.contentRect.x}mm`,
										top: `${table.bounds.y - page.contentRect.y}mm`,
										width: `${table.bounds.width}mm`,
									}
								: undefined
						}
					>
						<caption>{table.identity}</caption>
						<thead>
							<tr>
								{table.columns.map((column) => (
									<th key={column} scope="col">
										{column}
									</th>
								))}
							</tr>
						</thead>
						<tbody>
							{table.rows.map((row, rowIndex) => (
								<tr key={rowIndex}>
									{row.map((cell, index) => (
										<td key={table.columns[index]}>{cell}</td>
									))}
								</tr>
							))}
						</tbody>
					</table>
				))}
				{page.locatedNotes && page.contentRect
					? page.locatedNotes.map((note) => (
							<p
								className="drawing-located-note"
								key={note.identity}
								data-zone={note.location.zone}
								style={{
									left: `${note.bounds.x - page.contentRect!.x}mm`,
									top: `${note.bounds.y - page.contentRect!.y}mm`,
									width: `${note.bounds.width}mm`,
								}}
							>
								{note.text}
							</p>
						))
					: page.notes.length > 0 && (
							<aside>
								<h2>Notes</h2>
								<ol>
									{page.notes.map((note) => (
										<li key={note}>{note}</li>
									))}
								</ol>
							</aside>
						)}
			</section>
			{page.informationBlock && (
				<dl
					className="drawing-information-block"
					data-zone={page.informationBlock.location.zone}
					style={{
						left: `${page.informationBlock.bounds.x}mm`,
						top: `${page.informationBlock.bounds.y}mm`,
						width: `${page.informationBlock.bounds.width}mm`,
						minHeight: `${page.informationBlock.bounds.height}mm`,
					}}
				>
					{Object.entries(page.informationBlock.fields).map(([key, value]) => (
						<div key={key}>
							<dt>{key}</dt>
							<dd>{value}</dd>
						</div>
					))}
				</dl>
			)}
			<footer className="drawing-footer">
				A4 {page.orientation} · {drawing.metadata.templateIdentity} · Page {page.pageNumber}/
				{drawing.pages.length}
			</footer>
		</article>
	);
}

function DrawingZones({ page }: { page: DrawingPageIr }) {
	const scheme = page.zoneScheme!;
	const first = scheme.zones[0].bounds;
	const last = scheme.zones.at(-1)!.bounds;
	const right = last.x + last.width;
	const bottom = last.y + last.height;
	return (
		<svg
			className="drawing-zones"
			viewBox={`0 0 ${page.widthMillimetres} ${page.heightMillimetres}`}
			aria-label="A4 page zones"
		>
			<rect
				className="zone-border"
				x={first.x}
				y={first.y}
				width={right - first.x}
				height={bottom - first.y}
			/>
			{scheme.zones.map((zone) => (
				<rect
					key={zone.address}
					x={zone.bounds.x}
					y={zone.bounds.y}
					width={zone.bounds.width}
					height={zone.bounds.height}
					data-zone={zone.address}
				/>
			))}
			{scheme.columnLabels.map((label, index) => {
				const z = scheme.zones[index];
				const x = z.bounds.x + z.bounds.width / 2;
				return (
					<g key={`c-${label}`}>
						<text x={x} y={first.y - 2}>
							{label}
						</text>
						<text x={x} y={bottom + 4}>
							{label}
						</text>
						{index > 0 && (
							<>
								<line x1={z.bounds.x} y1={first.y} x2={z.bounds.x} y2={first.y + 3} />
								<line x1={z.bounds.x} y1={bottom} x2={z.bounds.x} y2={bottom - 3} />
							</>
						)}
					</g>
				);
			})}
			{scheme.rowLabels.map((label, index) => {
				const z = scheme.zones[index * scheme.columns];
				const y = z.bounds.y + z.bounds.height / 2;
				return (
					<g key={`r-${label}`}>
						<text x={first.x - 3} y={y}>
							{label}
						</text>
						<text x={right + 3} y={y}>
							{label}
						</text>
						{index > 0 && (
							<>
								<line x1={first.x} y1={z.bounds.y} x2={first.x + 3} y2={z.bounds.y} />
								<line x1={right} y1={z.bounds.y} x2={right - 3} y2={z.bounds.y} />
							</>
						)}
					</g>
				);
			})}
		</svg>
	);
}

function DrawingAnnotation({ annotation }: { annotation: DrawingAnnotationIr }) {
	const body = annotation.selectedCandidate.body;
	return (
		<g data-semantic-reference={annotation.semanticReference}>
			<polyline
				className="leader"
				points={annotation.selectedCandidate.leader.map((p) => `${p.x},${p.y}`).join(" ")}
			/>
			<rect
				className="annotation-body"
				x={body.x}
				y={body.y}
				width={body.width}
				height={body.height}
			/>
			<text x={body.x + 1} y={body.y + 4}>
				{annotation.engineeringDisplay}
			</text>
		</g>
	);
}
