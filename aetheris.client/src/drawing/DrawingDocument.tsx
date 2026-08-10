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
	return (
		<article
			className="drawing-page"
			data-orientation={page.orientation}
			data-machina-layout-rows={drawingPageLayoutRows.length}
			style={{ width: `${page.widthMillimetres}mm`, height: `${page.heightMillimetres}mm` }}
		>
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
						<dd>{drawing.metadata.revision ?? "-"}</dd>
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
					<table key={table.identity} data-source-identity={table.sourceIdentity}>
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
				{page.notes.length > 0 && (
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
			<footer className="drawing-footer">
				A4 {page.orientation} · {drawing.metadata.templateIdentity} · Page {page.pageNumber}/
				{drawing.pages.length}
			</footer>
		</article>
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
