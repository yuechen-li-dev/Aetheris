export type DiagnosticSeverity = 'info' | 'warning' | 'error';
export type Unit = 'mm' | 'cm' | 'm' | 'deg' | 'rad';
export interface SourceReference { readonly source: string; readonly line: number; readonly column: number; readonly start: number; readonly length: number }
export interface Diagnostic { readonly severity: DiagnosticSeverity; readonly code: string; readonly message: string; readonly source?: SourceReference; readonly details?: string }
export interface UnitValue { readonly value: number; readonly unit: Unit }
export interface EditableProperty { readonly id: string; readonly ownerEntityId: string; readonly name: string; readonly type: 'Length' | 'Angle' | 'Integer' | 'Number' | 'Boolean' | 'Enum'; readonly unit: Unit; readonly value: number; readonly writable: boolean; readonly source: SourceReference }
export interface ModelTreeNode { readonly id: string; readonly kind: string; readonly name: string; readonly parentId?: string; readonly children: readonly string[]; readonly visible: boolean; readonly source?: SourceReference }
export interface ModelTree { readonly rootId: string; readonly nodes: readonly ModelTreeNode[] }
export interface MeshRange { readonly startTriangle: number; readonly triangleCount: number; readonly faceId: string; readonly semanticEntityId: string }
export interface DisplayMeshDefinition { readonly id: string; readonly identity: string; readonly positions: Float64Array; readonly normals: Float64Array; readonly indices: Uint32Array; readonly ranges: readonly MeshRange[] }
export interface DisplayMeshOccurrence { readonly id: string; readonly path: string; readonly parentId?: string; readonly definitionId?: string; readonly semanticEntityId: string; readonly transform: readonly number[] }
export interface DisplayMesh { readonly schema: 'aetheris/display-mesh/1' | 'aetheris/assembly-display-mesh/1'; readonly name: string; readonly units: 'mm'; readonly definitions: readonly DisplayMeshDefinition[]; readonly occurrences: readonly DisplayMeshOccurrence[] }
export interface ChangeSet { readonly changedEntityIds: readonly string[]; readonly addedEntityIds: readonly string[]; readonly removedEntityIds: readonly string[]; readonly meshChanged: boolean; readonly diagnosticsChanged: boolean }
export interface RebuildResult { readonly success: boolean; readonly revision: number; readonly diagnostics: readonly Diagnostic[]; readonly retainedPreviousGeometry: boolean; readonly changes?: ChangeSet }
export interface RuntimeCapabilities { readonly compile: boolean; readonly solidModeling: boolean; readonly assembly: boolean; readonly displayMesh: boolean; readonly propertyInspection: boolean; readonly parameterRebuild: boolean; readonly stepExport: boolean; readonly sheetMetal: boolean; readonly fea: boolean; readonly externalStepImport: boolean; readonly forgeSubprocess: boolean; readonly cancellation: boolean; readonly worker: boolean }
export interface RuntimeInfo { readonly packageVersion: string; readonly runtimeVersion: string; readonly contractVersion: 'aetheris/web-editor-contract/1'; readonly language: 'Firmament'; readonly capabilities: RuntimeCapabilities }
export interface OperationOptions { readonly signal?: AbortSignal }
export interface CompileOptions extends OperationOptions { readonly sourceName?: string }
export interface AetherisOptions { readonly worker?: boolean; readonly wasmUrl?: string | URL; readonly diagnostics?: (diagnostics: readonly Diagnostic[]) => void }
export class AetherisError extends Error { readonly code: string; readonly details?: string }
export class Aetheris {
  static create(options?: AetherisOptions): Promise<Aetheris>;
  readonly runtimeInfo: RuntimeInfo;
  info(): Promise<RuntimeInfo>;
  capabilities(): Promise<RuntimeCapabilities>;
  compile(source: string, options?: CompileOptions): Promise<{ model: ModelSession | null; diagnostics: readonly Diagnostic[] }>;
  dispose(): Promise<void>;
}
export class ModelSession {
  readonly id: string; readonly name: string; readonly revision: number; readonly source: string; readonly sourceName: string;
  readonly tree: ModelTree; readonly properties: readonly EditableProperty[]; readonly mesh: DisplayMesh; readonly diagnostics: readonly Diagnostic[]; readonly changes: ChangeSet;
  entity(id: string): ModelTreeNode | undefined;
  property(id: string): EditableProperty | undefined;
  setProperty(propertyId: string, value: UnitValue, options?: OperationOptions): Promise<RebuildResult>;
  setSource(source: string, options?: CompileOptions): Promise<RebuildResult>;
  rebuild(options?: OperationOptions): Promise<RebuildResult>;
  resolveSelection(definitionId: string, triangleIndex: number, occurrenceId?: string): { semanticEntityId: string; faceId: string; occurrenceId: string; definitionId: string } | null;
  selectionForEntity(entityId: string): { occurrenceIds: readonly string[]; ranges: readonly ({ definitionId: string } & MeshRange)[] };
  exportSTEP(options?: OperationOptions): Promise<Uint8Array>;
  exportSTEPBlob(options?: OperationOptions): Promise<Blob>;
  dispose(): Promise<void>;
}
