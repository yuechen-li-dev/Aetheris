/** Browser downloads use the product's leaf name, never host paths or runtime IDs. */
export function stepExportFileName(path: string | null | undefined): string {
	const name = (path ?? "").split(/[\\/]/).pop() ?? "";
	const leaf = [...name].map(char => char.charCodeAt(0) < 32 || '<>:"|?*'.includes(char) ? "_" : char).join("").trim();
	return leaf ? leaf.replace(/\.(?:step|stp)$/i, "") + ".step" : "part.step";
}
