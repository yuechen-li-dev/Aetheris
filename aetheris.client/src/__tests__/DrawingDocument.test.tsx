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
});
