import { M } from "machinalayout/machina";

// MachinaLayout owns the coarse A4 document regions. React owns semantic
// document structure and SVG owns only projected engineering graphics.
export const drawingPageLayoutRows = M.rows(
	M.vstack("drawing-page", { gap: 3, padding: 10 }, [
		M.fixed("drawing-header", 12, { view: "DrawingHeader" }),
		M.fill("drawing-content", 1, { view: "DrawingContent" }),
		M.fixed("drawing-footer", 8, { view: "DrawingFooter" }),
	]),
);
