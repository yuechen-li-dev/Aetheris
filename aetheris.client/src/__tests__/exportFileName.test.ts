import { expect, it } from "vitest";
import { stepExportFileName } from "../application/exportFileName";

it("preserves a product leaf name across Windows/browser paths without runtime identities", () => {
	expect(stepExportFileName("exports/products/mounting-plate.step")).toBe("mounting-plate.step");
	expect(stepExportFileName("C:\\parts\\M6 screw.STP")).toBe("M6 screw.step");
	expect(stepExportFileName(null)).toBe("part.step");
});
