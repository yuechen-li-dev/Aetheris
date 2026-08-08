import { Table } from "machinalayout/table";
import { useMemo } from "react";

export interface PropertyRecord {
	property: string;
	value: string | number;
}

export function PropertyTable({ id, rows }: { id: string; rows: readonly PropertyRecord[] }) {
	const records = useMemo(() => {
		const table = Table.define({
			id,
			columns: {
				property: rows.map((row) => row.property),
				value: rows.map((row) => row.value),
			},
		});
		return Table.toObjects(table);
	}, [id, rows]);

	return (
		<table className="property-table">
			<tbody>
				{records.map((record) => (
					<tr key={record.property}>
						<th scope="row">{record.property}:</th>
						<td>{record.value}</td>
					</tr>
				))}
			</tbody>
		</table>
	);
}
