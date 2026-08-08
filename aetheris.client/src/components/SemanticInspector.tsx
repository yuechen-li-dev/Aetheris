import { matchKind } from "machinalayout/match";
import { Table } from "machinalayout/table";
import { useMemo } from "react";
import type { ReactNode } from "react";
import { PropertyTable, type PropertyRecord } from "./PropertyTable";
import {
	indexSemanticInspection,
	semanticTree,
	type SemanticTreeNode,
} from "../viewer/semanticInspection";
import type { CadmataEntity, CadmataVisualizationArtifact } from "../viewer/conceptVisualization";

function TreeNode({
	node,
	selectedId,
	onSelect,
	depth = 0,
}: {
	node: SemanticTreeNode;
	selectedId: string | null;
	onSelect: (id: string) => void;
	depth?: number;
}) {
	return (
		<li>
			<button
				type="button"
				className={
					node.entity.stableId === selectedId
						? "semantic-tree__item active-row"
						: "semantic-tree__item"
				}
				style={{ paddingLeft: 8 + depth * 12 }}
				onClick={() => onSelect(node.entity.stableId)}
			>
				{node.entity.kind.replace("Feature", "")} {node.entity.label}
			</button>
			{node.children.length ? (
				<ul>
					{node.children.map((child) => (
						<TreeNode
							key={child.entity.stableId}
							node={child}
							selectedId={selectedId}
							onSelect={onSelect}
							depth={depth + 1}
						/>
					))}
				</ul>
			) : null}
		</li>
	);
}
function rows(entity: CadmataEntity): PropertyRecord[] {
	return [
		{ property: "Kind", value: entity.kind },
		{ property: "Stable ID", value: entity.stableId },
		...(entity.sourceSpan ? [{ property: "Source", value: entity.sourceSpan }] : []),
		...(entity.topology?.faceIds?.length
			? [{ property: "Faces", value: entity.topology.faceIds.join(", ") }]
			: []),
		...(entity.topology?.edgeIds?.length
			? [{ property: "Edges", value: entity.topology.edgeIds.join(", ") }]
			: []),
		...Object.entries(entity.metadata ?? {}).map(([property, value]) => ({
			property,
			value: String(value),
		})),
	];
}
function PmiDetails({ entity }: { entity: CadmataEntity }) {
	return (
		<>
			<PropertyTable id={`pmi-${entity.stableId}`} rows={rows(entity)} />
			{entity.metadata?.require ? (
				<p className="semantic-provenance">
					<strong>From:</strong> {String(entity.metadata.require)}
					<br />
					<strong>Subject:</strong> {String(entity.metadata.subject ?? "not published")}
					<br />
					<strong>Expected:</strong> {String(entity.metadata.expected ?? "not published")}
				</p>
			) : null}
		</>
	);
}
function BrepDetails({
	entity,
	artifact,
}: {
	entity: CadmataEntity;
	artifact: CadmataVisualizationArtifact;
}) {
	const index = indexSemanticInspection(artifact);
	const owners = [
		...(entity.topology?.faceIds ?? []).flatMap((id) => index.faceOwners.get(id) ?? []),
		...(entity.topology?.edgeIds ?? []).flatMap((id) => index.edgeOwners.get(id) ?? []),
	].filter(
		(owner, position, all) =>
			all.findIndex((item) => item.stableId === owner.stableId) === position,
	);
	const pmi = owners
		.flatMap((owner) => index.pmiByTarget.get(owner.stableId) ?? [])
		.filter(
			(item, position, all) =>
				all.findIndex((candidate) => candidate.stableId === item.stableId) === position,
		);
	return (
		<>
			<PropertyTable id={`brep-${entity.stableId}`} rows={rows(entity)} />
			<p className="semantic-provenance">
				<strong>Semantic owner(s):</strong>{" "}
				{owners.length
					? owners
							.map(
								(owner) =>
									`${owner.kind} ${owner.label}${owner.sourceSpan ? ` · ${owner.sourceSpan}` : ""}`,
							)
							.join("; ")
					: "None published"}
				<br />
				<strong>Associated PMI:</strong>{" "}
				{pmi.length ? pmi.map((item) => item.label).join(", ") : "None"}
			</p>
			<p className="semantic-provenance">
				Advanced traceability: Aetheris topology IDs are shown above. STEP entity IDs are not yet
				published by this fixture channel.
			</p>
		</>
	);
}
function TemplateParameterTable({ entity }: { entity: CadmataEntity }) {
	const records = useMemo(() => {
		const metadata = entity.metadata ?? {};
		const names = Object.keys(metadata)
			.filter((key) => key.startsWith("parameter."))
			.map((key) => key.slice("parameter.".length))
			.sort();
		return Table.toObjects(
			Table.define({
				id: `template-parameters-${entity.stableId}`,
				columns: {
					parameter: names,
					type: names.map((name) => String(metadata[`parameterType.${name}`] ?? "Unknown")),
					value: names.map((name) => String(metadata[`parameter.${name}`] ?? "")),
					source: names.map(
						(name) =>
							`${String(metadata[`parameterSource.${name}`] ?? "Authored")} · ${String(metadata[`parameterStatus.${name}`] ?? "Bound")}`,
					),
				},
			}),
		);
	}, [entity]);
	if (!records.length) return null;
	return (
		<>
			<h4>Template parameters</h4>
			<table className="property-table">
				<thead>
					<tr>
						<th>Parameter</th>
						<th>Type</th>
						<th>Value</th>
						<th>Source</th>
					</tr>
				</thead>
				<tbody>
					{records.map((record) => (
						<tr key={String(record.parameter)}>
							<th scope="row">{record.parameter}</th>
							<td>{record.type}</td>
							<td>{record.value}</td>
							<td>{record.source}</td>
						</tr>
					))}
				</tbody>
			</table>
		</>
	);
}
function GenericDetails({ entity }: { entity: CadmataEntity }) {
	return (
		<>
			<PropertyTable
				id={`semantic-${entity.stableId}`}
				rows={rows(entity).filter((row) => !row.property.startsWith("parameter"))}
			/>
			{entity.kind === "TemplateInstance" ? <TemplateParameterTable entity={entity} /> : null}
		</>
	);
}
export function SemanticInspector({
	artifact,
	selectedId,
	onSelect,
}: {
	artifact: CadmataVisualizationArtifact | null;
	selectedId: string | null;
	onSelect: (id: string) => void;
}) {
	if (!artifact)
		return <p>Load a real Firmament fixture to inspect compiler-published semantic structure.</p>;
	const entity = artifact.entities.find((candidate) => candidate.stableId === selectedId) ?? null;
	const details: ReactNode = entity
		? (matchKind(
				entity.kind.startsWith("BRep")
					? { kind: "BRep" as const, entity }
					: entity.kind === "HoleDiameter" || entity.kind === "Datum"
						? { kind: "Pmi" as const, entity }
						: { kind: "Generic" as const, entity },
				{
					Pmi: ({ entity: item }) => <PmiDetails entity={item} />,
					BRep: ({ entity: item }) => <BrepDetails entity={item} artifact={artifact} />,
					Generic: ({ entity: item }) => <GenericDetails entity={item} />,
				},
			) as ReactNode)
		: null;
	return (
		<div className="semantic-inspector">
			<p>
				<strong>{artifact.fixtureId}</strong> · {artifact.metrics?.entityCount ?? 0} published
				entities
			</p>
			<div className="semantic-tree">
				<ul>
					{semanticTree(artifact).map((node) => (
						<TreeNode
							key={node.entity.stableId}
							node={node}
							selectedId={selectedId}
							onSelect={onSelect}
						/>
					))}
				</ul>
			</div>
			{entity ? (
				<div className="cadmata-inspector__details">
					<h3>{entity.label}</h3>
					{details}
				</div>
			) : null}
		</div>
	);
}
