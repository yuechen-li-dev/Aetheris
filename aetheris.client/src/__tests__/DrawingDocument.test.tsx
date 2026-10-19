import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { DrawingDocument } from "../drawing/DrawingDocument";
import type { DrawingIr } from "../drawing/drawingIr";
import { drawingPageLayoutRows } from "../drawing/drawingLayout";

const fixture: DrawingIr = {
	schemaVersion: "aetheris-drawing-m0",
	identity: "TestDrawing",
	metadata: {
		title: "Test drawing",
		productName: "Block",
		drawingIdentity: "TestDrawing",
		templateIdentity: "StandardDrawing",
	},
	pages: [
		{
			pageNumber: 1,
			orientation: "Landscape",
			widthMillimetres: 297,
			heightMillimetres: 210,
			views: [],
			annotations: [],
			notes: [],
			tables: [
				{
					identity: "Sizes",
					sourceIdentity: "Sizes",
					columns: ["Size", "Width"],
					rows: [["A", "20mm"]],
				},
			],
		},
	],
};

describe("DrawingDocument", () => {
	it("uses MachinaLayout regions and renders design data as a real React table", () => {
		render(<DrawingDocument drawing={fixture} />);
		expect(drawingPageLayoutRows.map((row) => row.id)).toEqual([
			"drawing-page",
			"drawing-header",
			"drawing-content",
			"drawing-footer",
		]);
		expect(screen.getByRole("table", { name: "Sizes" })).toBeTruthy();
		expect(screen.getByRole("cell", { name: "20mm" })).toBeTruthy();
	});

	it("preserves physical A4 dimensions in the document element", () => {
		const { container } = render(<DrawingDocument drawing={fixture} />);
		const article = container.querySelector("article");
		expect(article?.getAttribute("style")).toContain("width: 297mm");
		expect(article?.getAttribute("style")).toContain("height: 210mm");
	});

	it("renders M0B semantic zones, information metadata, and a real BOM table", () => {
		const m0b: DrawingIr = {
			...fixture,
			schemaVersion: "aetheris-drawing-m0b",
			metadata: {
				...fixture.metadata,
				revision: { major: 1, minor: 1, patch: 0 },
				author: "CODEX",
			},
			pages: [
				{
					...fixture.pages[0],
					zoneScheme: {
						rows: 4,
						columns: 6,
						rowLabels: ["A", "B", "C", "D"],
						columnLabels: ["1", "2", "3", "4", "5", "6"],
						zones: Array.from({ length: 24 }, (_, index) => ({
							address: `${["A", "B", "C", "D"][Math.floor(index / 6)]}${(index % 6) + 1}`,
							bounds: {
								x: 7 + (index % 6) * 47,
								y: 12 + Math.floor(index / 6) * 46,
								width: 47,
								height: 46,
							},
						})),
					},
					informationBlock: {
						bounds: { x: 175, y: 176, width: 115, height: 22 },
						location: { page: 1, zone: "D5" },
						fields: { Author: "CODEX", Revision: "1.1.0" },
					},
					tables: [
						{
							identity: "BOM",
							sourceIdentity: "assembly:machine",
							kind: "BillOfMaterials",
							columns: ["ITEM", "QTY"],
							rows: [["1", "3"]],
							location: { page: 1, zone: "A2" },
						},
					],
				},
			],
		};
		const { container } = render(<DrawingDocument drawing={m0b} />);
		expect(screen.getByRole("table", { name: "BOM" })).toBeTruthy();
		expect(screen.getByText("CODEX")).toBeTruthy();
		expect(container.querySelector('[data-zone="D5"]')).toBeTruthy();
		expect(container.querySelector('[data-table-kind="BillOfMaterials"]')).toBeTruthy();
	});
    it("projects compiler-owned strokes and surface-grid primitives without recomputing layout", () => {
        const drawing: DrawingIr = { ...fixture, pages: [{ ...fixture.pages[0],
            views: [{ identity: "ISO", viewport: { x: 10, y: 20, width: 200, height: 120 }, primitives: [
                { stableId: "surface-grid", kind: "SurfaceGrid", points: [{ x: 30, y: 40 }, { x: 60, y: 70 }] }
            ] }],
            annotations: [{ identity: "diameter", semanticReference: "Size1", kind: "DiameterDimension", engineeringDisplay: "Ø4 mm ±0.05",
                selectedCandidate: { body: { x: 100, y: 40, width: 40, height: 6 }, leader: [] },
                strokes: [{ start: { x: 60, y: 40 }, end: { x: 100, y: 43 } }, { start: { x: 60, y: 40 }, end: { x: 62, y: 41 } }]
            }]
        }] };
        const { container } = render(<DrawingDocument drawing={drawing} />);
        expect(container.querySelectorAll("line.leader")).toHaveLength(2);
        expect(container.querySelector("polyline.surfacegrid")).toBeTruthy();
        expect(container.querySelector('[data-semantic-reference="Size1"] text')?.textContent).toBe("Ø4 mm ±0.05");
        expect(container.querySelector(".annotation-body")?.getAttribute("style")).toContain("stroke: none");
    });

    it("renders the compiler title block as a real table with unperformed approvals blank", () => {
        const bounds = { x: 120, y: 157, width: 170, height: 41 };
        const cell = (key: string, label: string, value: string) => ({ key, label, value, columnSpan: 2,
            bounds, valueLines: value ? [value] : [], valueSizeMillimetres: 2.6, emphasized: false });
        const drawing: DrawingIr = { ...fixture, pages: [{ ...fixture.pages[0], informationBlock: {
            bounds, location: { page: 1, zone: "D5" }, fields: {},
            table: { columnWidths: [1, 1, 1, 1, 1, 1], rows: [
                { height: 8, cells: [cell("DrawnBy", "DRAWN BY", "Codex (AI)"), cell("CheckedBy", "CHECKED BY", ""), cell("ApprovedBy", "APPROVED BY", "")] },
                { height: 7, cells: [cell("DrawnDate", "DRAWN DATE", "2026-10-18"), cell("CheckedDate", "CHECKED DATE", ""), cell("ApprovedDate", "APPROVED DATE", "")] }
            ] }
        } }] };
        const { container } = render(<DrawingDocument drawing={drawing} />);
        expect(screen.getByRole("table", { name: "Drawing title block" })).toBeTruthy();
        expect(container.querySelectorAll(".drawing-title-block td[colspan='2']")).toHaveLength(6);
        expect(container.querySelector('[data-field="DrawnBy"]')?.textContent).toContain("Codex (AI)");
        expect(container.querySelector('[data-field="DrawnDate"]')?.textContent).toContain("2026-10-18");
        expect(container.querySelector('[data-field="ApprovedBy"]')?.textContent).toBe("APPROVED BY");
    });

});
