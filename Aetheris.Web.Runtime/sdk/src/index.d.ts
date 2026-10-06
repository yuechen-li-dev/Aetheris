export type DiagnosticSeverity = 'info' | 'warning' | 'error';
export type Unit = 'mm' | 'cm' | 'm' | 'deg' | 'rad';
export interface SourceReference { readonly source: string; readonly line: number; readonly column: number; readonly start: number; readonly length: number }
export interface Diagnostic { readonly severity: DiagnosticSeverity; readonly code: string; readonly message: string; readonly source?: SourceReference; readonly details?: string }
export interface UnitValue { readonly value: number; readonly unit: Unit }
export interface EditableProperty { readonly id: string; readonly ownerEntityId: string; readonly name: string; readonly type: 'Length' | 'Angle' | 'Integer' | 'Number' | 'Boolean' | 'Enum'; readonly unit: Unit; readonly value: number; readonly writable: boolean; readonly source: SourceReference }
export interface ModelTreeNode { readonly id: string; readonly kind: string; readonly name: string; readonly parentId?: string; readonly children: readonly string[]; readonly visible: boolean; readonly source?: SourceReference; readonly holeDiameterMm?: number | null; readonly semanticConstruct?: string | null }
export interface ModelTree { readonly rootId: string; readonly nodes: readonly ModelTreeNode[] }
export type SourceAddressability = 'AuthoredStable' | 'DerivedStable' | 'ImportedStable' | 'RuntimeOnly' | 'Ambiguous' | 'Unstable';
export interface MeshRange { readonly startTriangle: number; readonly triangleCount: number; readonly faceId: string; readonly semanticEntityId: string | null; readonly semanticTopologyId?: string | null; readonly topologyKind?: 'Face'; readonly outputRole?: string | null; readonly originFeature?: string | null; readonly sourceAddressability?: SourceAddressability; readonly selector?: string | null; readonly selectorReason?: string | null; readonly source?: SourceReference | null; readonly buildRevision?: number }
export interface SelectionDescription { readonly semanticEntityId: string; readonly faceId: string; readonly occurrenceId: string; readonly definitionId: string; readonly semanticTopologyId: string | null; readonly topologyKind: string; readonly outputRole: string | null; readonly originFeature: string | null; readonly selector: string | null; readonly sourceAddressability: SourceAddressability; readonly selectorReason: string | null; readonly source: SourceReference | null; readonly buildRevision: number; readonly sourceAddressable: boolean }
export interface DisplayEdgePolyline { readonly edgeId: string; readonly points: readonly (readonly number[])[]; readonly closed: boolean; readonly semanticEntityId?: string; readonly semanticTopologyId?: string | null; readonly topologyKind?: 'Edge'; readonly outputRole?: string | null; readonly originFeature?: string | null; readonly sourceAddressability?: SourceAddressability; readonly selector?: string | null; readonly selectorReason?: string | null; readonly source?: SourceReference | null; readonly buildRevision?: number }
export interface CirRetention { readonly schema: 'aetheris/cir-retention/1'; readonly definitionId: string; readonly qualification: 'cir-qualified' | 'mesh'; readonly fallbackReason: string | null; readonly structuralIdentity: string | null; readonly fieldSource: string | null; readonly minimumMm: readonly number[] | null; readonly maximumMm: readonly number[] | null }
export interface ResolvedDisplayMaterial { readonly baseColor: readonly number[]; readonly roughness: number; readonly metallic: number; readonly opacity: number; readonly emissive: readonly number[] }
export interface DisplayShaderArtifact { readonly schema: 'aetheris/display-shader/1'; readonly shaderId: string; readonly wgsl: string; readonly vertexEntryPoint: string; readonly fragmentEntryPoint: string; readonly bindings: readonly { readonly group: number; readonly binding: number; readonly kind: 'uniform'; readonly byteSize: number }[]; readonly capabilities: readonly string[]; readonly sourceIdentity: string; readonly compilerVersion: string }
export interface DisplayCamera { readonly name: string; readonly transform: readonly number[]; readonly lookAtMm?: readonly number[]; readonly fovDegrees: number }
export interface DisplayMeshDefinition { readonly id: string; readonly identity: string; readonly positions: Float64Array; readonly normals: Float64Array; readonly indices: Uint32Array; readonly ranges: readonly MeshRange[]; readonly edges?: readonly DisplayEdgePolyline[]; readonly cir?: CirRetention | null; readonly displayPath?: 'mesh' | 'cir'; readonly fallbackReason?: string | null; readonly shader?: { readonly artifact: DisplayShaderArtifact | null; readonly status: string; readonly reason: string | null; readonly cacheHit: boolean; readonly generationMilliseconds: number }; readonly geometryRevision?: string | null; readonly diagnostics?: readonly { readonly code: string; readonly message: string; readonly faceId?: string | null }[] }
export interface DisplayMeshOccurrence { readonly id: string; readonly path: string; readonly parentId?: string; readonly definitionId?: string; readonly semanticEntityId: string; readonly transform: readonly number[]; readonly material?: ResolvedDisplayMaterial | null; readonly kind?: string }
export interface DisplayBoundary { readonly path: string; readonly room: string; readonly kind: string; readonly frame: readonly number[]; readonly widthMm: number; readonly heightMm: number; readonly openings: readonly string[] }
export interface DisplayMesh { readonly schema: 'aetheris/display-mesh/1' | 'aetheris/assembly-display-mesh/1' | 'aetheris/scene-display-mesh/1'; readonly name: string; readonly units: 'mm'; readonly definitions: readonly DisplayMeshDefinition[]; readonly occurrences: readonly DisplayMeshOccurrence[]; readonly cameras?: readonly DisplayCamera[]; readonly minimumMm?: readonly number[]; readonly maximumMm?: readonly number[]; readonly boundaries?: readonly DisplayBoundary[] }
export interface ChangeSet { readonly changedEntityIds: readonly string[]; readonly addedEntityIds: readonly string[]; readonly removedEntityIds: readonly string[]; readonly meshChanged: boolean; readonly diagnosticsChanged: boolean }
export interface RebuildResult { readonly success: boolean; readonly revision: number; readonly diagnostics: readonly Diagnostic[]; readonly retainedPreviousGeometry: boolean; readonly changes?: ChangeSet }
export interface RuntimeCapabilities { readonly compile: boolean; readonly solidModeling: boolean; readonly assembly: boolean; readonly displayMesh: boolean; readonly propertyInspection: boolean; readonly parameterRebuild: boolean; readonly stepExport: boolean; readonly sheetMetal: boolean; readonly fea: boolean; readonly externalStepImport: boolean; readonly forgeSubprocess: boolean; readonly cancellation: boolean; readonly worker: boolean }
export interface RuntimeInfo { readonly packageVersion: string; readonly runtimeVersion: string; readonly contractVersion: 'aetheris/web-editor-contract/1'; readonly language: 'Firmament'; readonly capabilities: RuntimeCapabilities }
export interface WorkerTiming { readonly operation: string; readonly executionMilliseconds: number; readonly transportMilliseconds: number; readonly payloadBytes: number; readonly initialization?: { readonly moduleImportMilliseconds: number; readonly runtimeCreateMilliseconds: number; readonly assemblyExportsMilliseconds: number } | null }
export interface OperationOptions { readonly signal?: AbortSignal; readonly sourceRevision?: string; readonly performance?: boolean }
export interface CompileOptions extends OperationOptions { readonly sourceName?: string; readonly projectDocuments?: Readonly<Record<string, string>> }
export interface LanguageOptions { readonly sourceName?: string; readonly sourceRevision: string }
export interface LanguageField { readonly name: string; readonly type: string; readonly required: boolean; readonly meaning: string; readonly default?: string | null; readonly choices?: readonly string[] | null }
export interface LanguageEntry { readonly constructId: string; readonly name: string; readonly context: string; readonly source: string }
export interface LanguageCompletion { readonly document: string; readonly revision: string; readonly context: string; readonly replaceStart: number; readonly replaceLength: number; readonly fields: readonly LanguageField[]; readonly missingRequiredFields: readonly string[]; readonly entries?: readonly LanguageEntry[] | null; readonly values?: readonly string[] | null }
export interface LanguageToken { readonly start: number; readonly length: number; readonly kind: 'keyword' | 'construct' | 'type' | 'identifier' | 'field' | 'number' | 'unit' | 'string' | 'comment' | 'punctuation' | 'selector' | 'value' }
export interface LanguageAnalysis { readonly document: string; readonly revision: string; readonly tokens: readonly LanguageToken[]; readonly diagnostics: readonly Diagnostic[] }
export interface LanguageHover { readonly document: string; readonly revision: string; readonly start: number; readonly length: number; readonly title: string; readonly description: string }
export interface LanguageFormat { readonly document: string; readonly revision: string; readonly text: string; readonly changed: boolean }
export interface SemanticFieldSchema { readonly id: string; readonly name: string; readonly kind: string; readonly unit: 'None' | 'Length' | 'Angle'; readonly required: boolean; readonly default: string | null; readonly choices: readonly string[]; readonly description: string | null; readonly sourceEditable: boolean }
export interface SemanticOutputSchema { readonly id: string; readonly name: string; readonly kind: string; readonly sourceAddressable: boolean; readonly sourceRole: string | null }
export interface SemanticConstructSchema { readonly id: string; readonly name: string; readonly context: string | null; readonly entry: string | null; readonly description: string | null; readonly compatibilityAlias: string | null; readonly fields: readonly SemanticFieldSchema[]; readonly outputs: readonly SemanticOutputSchema[] }
export interface SemanticSchema { readonly version: 'firmament-semantic-schema/1'; readonly projectionVersion: 'firmament-field-projection/1'; readonly constructs: readonly SemanticConstructSchema[] }
export interface ProjectedValue { readonly valueKind: string; readonly text: string; readonly number?: number | null; readonly components?: readonly number[] | null; readonly unit?: string | null }
export interface FieldProjection { readonly fieldId: string; readonly name: string; readonly kind: string; readonly unit: string; readonly effectiveValue: ProjectedValue | null; readonly authoredValue: string | null; readonly origin: 'Authored' | 'Defaulted' | 'Derived' | 'Inherited' | 'Unavailable'; readonly declaration: SourceReference | null; readonly source: SourceReference | null; readonly editable: boolean; readonly readOnlyReason: string | null }
export interface ConstructProjection { readonly version: 'firmament-field-projection/1'; readonly constructId: string; readonly semanticId: string; readonly sourceRevision: string; readonly buildRevision: number; readonly source: SourceReference | null; readonly fields: readonly FieldProjection[]; readonly outputs: readonly SemanticOutputSchema[] }
export interface FieldRewrite { readonly source: string; readonly sourceRevision: string; readonly replaced: SourceReference; readonly replacement: string }
export interface SelectorCandidate { readonly selector: string; readonly kind: string; readonly semanticKey: string | null; readonly outputRole: string | null; readonly source: SourceReference; readonly qualification: SourceAddressability; readonly buildRevision: number }
export interface AetherisOptions { readonly worker?: boolean; readonly wasmUrl?: string | URL; readonly diagnostics?: (diagnostics: readonly Diagnostic[]) => void }
export class AetherisError extends Error { readonly code: string; readonly details?: string }
export class Aetheris {
  static create(options?: AetherisOptions): Promise<Aetheris>;
  readonly runtimeInfo: RuntimeInfo;
  readonly workerTiming: WorkerTiming | null;
  readonly language: { complete(source: string, offset: number, options: LanguageOptions): Promise<LanguageCompletion>; analyze(source: string, options: LanguageOptions): Promise<LanguageAnalysis>; hover(source: string, offset: number, options: LanguageOptions): Promise<LanguageHover | null>; definition(source: string, offset: number, options: LanguageOptions): Promise<SourceReference | null>; format(source: string, options: LanguageOptions): Promise<LanguageFormat>; schema(): Promise<SemanticSchema> };
  info(): Promise<RuntimeInfo>;
  capabilities(): Promise<RuntimeCapabilities>;
  compile(source: string, options?: CompileOptions): Promise<{ model: ModelSession | null; diagnostics: readonly Diagnostic[] }>;
  dispose(): Promise<void>;
  terminate(): void;
}
export class ModelSession {
  readonly id: string; readonly name: string; readonly revision: number; readonly source: string; readonly sourceName: string;
  readonly tree: ModelTree; readonly properties: readonly EditableProperty[]; readonly mesh: DisplayMesh; readonly diagnostics: readonly Diagnostic[]; readonly changes: ChangeSet;
  readonly timings?: { readonly compileMilliseconds: number; readonly meshMilliseconds?: number; readonly totalBeforeSnapshotMilliseconds?: number; readonly profile?: { readonly phases: readonly { readonly name: string; readonly inclusiveMilliseconds: number; readonly exclusiveMilliseconds: number; readonly allocatedBytes: number }[]; readonly counts: Readonly<Record<string, number>>; readonly gc0: number; readonly gc1: number; readonly gc2: number } };
  readonly workerTiming: WorkerTiming | null;
  entity(id: string): ModelTreeNode | undefined;
  property(id: string): EditableProperty | undefined;
  setProperty(propertyId: string, value: UnitValue, options?: OperationOptions): Promise<RebuildResult>;
  setSource(source: string, options?: CompileOptions): Promise<RebuildResult>;
  describeConstruct(semanticId: string, options?: OperationOptions): Promise<ConstructProjection>;
  rewriteField(source: string, projection: ConstructProjection, fieldId: string, value: ProjectedValue, options?: OperationOptions): Promise<FieldRewrite>;
  rebuild(options?: OperationOptions): Promise<RebuildResult>;
  resolveSelection(definitionId: string, triangleIndex: number, occurrenceId?: string): Omit<SelectionDescription, 'sourceAddressable'> | null;
  describeSelection(definitionId: string, triangleIndex: number, occurrenceId?: string): SelectionDescription | null;
  describeEdgeSelection(definitionId: string, edgeId: string, occurrenceId?: string): SelectionDescription | null;
  selectionForSemanticId(semanticTopologyId: string): readonly ({ definitionId: string } & MeshRange)[];
  selectionForSourceSymbol(symbol: string): readonly ({ definitionId: string } & MeshRange)[];
  selectionForEntity(entityId: string): { occurrenceIds: readonly string[]; ranges: readonly ({ definitionId: string } & MeshRange)[] };
  geometrySourceMap(): readonly { definitionId: string; topologyId: string; topologyKind: string; semanticKey: string | null; outputRole: string | null; originFeature: string | null; source: SourceReference | null; selector: string | null; qualification: SourceAddressability; buildRevision: number }[];
  selectorCandidates(kind?: 'Face' | 'Edge'): readonly SelectorCandidate[];
  exportSTEP(options?: OperationOptions): Promise<Uint8Array>;
  exportSTEPBlob(options?: OperationOptions): Promise<Blob>;
  dispose(): Promise<void>;
}
