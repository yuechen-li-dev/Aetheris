export type DiagnosticSeverity = 'info' | 'warning' | 'error';
export type Unit = 'mm' | 'cm' | 'm' | 'deg' | 'rad' | 's' | 'ms';
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
export interface ConceptDisplayWire { readonly id: string; readonly owner: string; readonly kind: string; readonly points: readonly number[]; readonly provenance: string }
export interface DisplayMeshDefinition { readonly concepts?: readonly ConceptDisplayWire[]; readonly id: string; readonly identity: string; readonly positions: Float64Array; readonly normals: Float64Array; readonly indices: Uint32Array; readonly ranges: readonly MeshRange[]; readonly edges?: readonly DisplayEdgePolyline[]; readonly cir?: CirRetention | null; readonly displayPath?: 'mesh' | 'cir'; readonly fallbackReason?: string | null; readonly shader?: { readonly artifact: DisplayShaderArtifact | null; readonly status: string; readonly reason: string | null; readonly cacheHit: boolean; readonly generationMilliseconds: number }; readonly geometryRevision?: string | null; readonly diagnostics?: readonly { readonly code: string; readonly message: string; readonly faceId?: string | null }[] }
export interface DisplayMeshOccurrence { readonly concepts?: readonly ConceptDisplayWire[]; readonly id: string; readonly path: string; readonly parentId?: string; readonly definitionId?: string; readonly semanticEntityId: string; readonly transform: readonly number[]; readonly material?: ResolvedDisplayMaterial | null; readonly kind?: string }
export interface DisplayBoundary { readonly path: string; readonly room: string; readonly kind: string; readonly frame: readonly number[]; readonly widthMm: number; readonly heightMm: number; readonly openings: readonly string[] }
export interface DisplayMesh { readonly schema: 'aetheris/display-mesh/1' | 'aetheris/assembly-display-mesh/1' | 'aetheris/scene-display-mesh/1'; readonly name: string; readonly units: 'mm'; readonly definitions: readonly DisplayMeshDefinition[]; readonly occurrences: readonly DisplayMeshOccurrence[]; readonly cameras?: readonly DisplayCamera[]; readonly minimumMm?: readonly number[]; readonly maximumMm?: readonly number[]; readonly boundaries?: readonly DisplayBoundary[]; readonly annotations?: readonly PmiDisplayAnnotation[] }
/** AP242-authoritative requirements and separate presentation curves, Z-up millimetres. */
export interface PmiDisplayAnnotation {
  readonly id: string; readonly kind: string; readonly name: string; readonly text: string;
  readonly value: number | null; readonly tolerancePlus: number | null; readonly toleranceMinus: number | null;
  readonly stepFaceEntities: readonly number[]; readonly anchor: readonly number[];
  readonly labelPosition: readonly number[]; readonly textHeight: number;
  readonly lines: readonly (readonly (readonly number[])[])[];
  readonly target?: { readonly kind: 'PlanePatch' | 'CylinderWall'; readonly normal: readonly number[]; readonly stemLength: number; readonly surfaceOffset: number;
    readonly positions: readonly number[]; readonly normals: readonly number[]; readonly indices: readonly number[];
    readonly lines: readonly (readonly (readonly number[])[])[] } | null;
}
export interface MeshRange { readonly surfaceKind?: string | null; readonly diameterMm?: number | null }
export interface SelectionDescription { readonly surfaceKind?: string | null; readonly diameterMm?: number | null }
export interface DisplayMesh { readonly importedAsset?: { readonly path: string; readonly sha256: string; readonly evidence: string; readonly qualification: { readonly status: string; readonly geometryStatus: string } | null } | null }
export interface ChangeSet { readonly changedEntityIds: readonly string[]; readonly addedEntityIds: readonly string[]; readonly removedEntityIds: readonly string[]; readonly meshChanged: boolean; readonly diagnosticsChanged: boolean }
export interface RebuildResult { readonly success: boolean; readonly revision: number; readonly diagnostics: readonly Diagnostic[]; readonly retainedPreviousGeometry: boolean; readonly changes?: ChangeSet }
export interface RuntimeCapabilities { readonly compile: boolean; readonly solidModeling: boolean; readonly assembly: boolean; readonly displayMesh: boolean; readonly propertyInspection: boolean; readonly parameterRebuild: boolean; readonly stepExport: boolean; readonly artifactExport?: boolean; readonly sheetMetal: boolean; readonly fea: boolean; readonly externalStepImport: boolean; readonly forgeSubprocess: boolean; readonly cancellation: boolean; readonly worker: boolean }
export interface RuntimeInfo { readonly packageVersion: string; readonly runtimeVersion: string; readonly contractVersion: 'aetheris/web-editor-contract/1'; readonly language: 'Firmament'; readonly capabilities: RuntimeCapabilities }
export interface WorkerTiming { readonly operation: string; readonly executionMilliseconds: number; readonly transportMilliseconds: number; readonly payloadBytes: number; readonly initialization?: { readonly moduleImportMilliseconds: number; readonly runtimeCreateMilliseconds: number; readonly assemblyExportsMilliseconds: number } | null }
export interface OperationOptions { readonly signal?: AbortSignal; readonly sourceRevision?: string; readonly performance?: boolean }
export interface CompileOptions extends OperationOptions { readonly sourceName?: string; readonly projectDocuments?: Readonly<Record<string, string>> }
export interface LanguageOptions { readonly sourceName?: string; readonly sourceRevision: string; readonly projectDocuments?: Readonly<Record<string, string>>; readonly projectRoot?: string }
export interface LanguageField { readonly name: string; readonly type: string; readonly required: boolean; readonly meaning: string; readonly default?: string | null; readonly choices?: readonly string[] | null }
export interface LanguageEntry { readonly constructId: string; readonly name: string; readonly context: string; readonly source: string }
export interface LanguageCompletion { readonly document: string; readonly revision: string; readonly context: string; readonly replaceStart: number; readonly replaceLength: number; readonly fields: readonly LanguageField[]; readonly missingRequiredFields: readonly string[]; readonly entries?: readonly LanguageEntry[] | null; readonly values?: readonly string[] | null }
export interface LanguageToken { readonly start: number; readonly length: number; readonly kind: 'keyword' | 'construct' | 'type' | 'identifier' | 'field' | 'number' | 'unit' | 'string' | 'comment' | 'punctuation' | 'selector' | 'value' }
export interface LanguageAnalysis { readonly document: string; readonly revision: string; readonly tokens: readonly LanguageToken[]; readonly diagnostics: readonly Diagnostic[] }
export interface OutlineSpan { readonly start: number; readonly length: number }
export interface OutlineField { readonly name: string; readonly text: string; readonly shape: 'quantity' | 'number' | 'string' | 'boolean' | 'version' | 'entityreference' | 'reference' | 'member' | 'sequence' | 'call' | 'specialization' | 'object' | 'expression' | 'other'; readonly span: OutlineSpan; readonly valueSpan: OutlineSpan }
/** One declaration. `id` is its name path (`MotorMount/Modify:Body/Pilot`): stable across edits that keep names, kinds and nesting. */
export interface OutlineNode { readonly id: string; readonly kind: string; readonly variant: string | null; readonly modifiers: readonly string[]; readonly name: string | null; readonly occurrence: boolean; readonly parentId: string | null; readonly order: number; readonly span: OutlineSpan; readonly nameSpan: OutlineSpan | null; readonly schemaId: string | null; readonly parameters: readonly string[]; readonly fields: readonly OutlineField[]; readonly children: readonly string[]; /** A bare call placed in a body (`MountHole(center: …)`): `kind` is the callee, `fields` its arguments (positional ones named `#1`…). */ readonly placementControls?: { readonly translationMm: readonly number[]; readonly axis: string; readonly angleDegrees: number; readonly readOnlyReason?: string | null } | null; readonly call?: boolean; /** The initializer of a value declaration (`Static Thickness: Length = 8mm`), named `=`. */ readonly value?: OutlineField | null; /** The declared type annotation, as written. */ readonly annotation?: string | null; /** Declared parameters with types and defaults (Feature, Template). */ readonly signature?: readonly OutlineParameter[] }
export interface OutlineParameter { readonly name: string; readonly type: string; readonly default: string | null }
/** A resolved name use from one declaration to another. `via` is `field:<Name>`, `clause:<Using|Over|On|From|Key>`, `target`, `body`, `argument:<Name|type>`, `annotation`, `result` or `initializer`; nested object fields append `.<Name>`. */
export interface OutlineEdge { readonly from: string; readonly to: string; readonly kind: 'reference' | 'call' | 'specialization' | 'modifies' | 'asserts' | 'recognizes'; readonly via: string; readonly member: string | null; readonly span: OutlineSpan; readonly ambiguous: boolean; readonly toDocument: string | null }
/** A name use no declaration answers: a builtin, a vocabulary word, or a declaration outside the given documents. */
export interface OutlineExternal { readonly from: string; readonly name: string; readonly member: string | null; readonly kind: string; readonly via: string; readonly span: OutlineSpan }
export interface LanguageOutline { readonly version: 'firmament-outline/1'; readonly document: string; readonly revision: string; readonly nodes: readonly OutlineNode[]; readonly edges: readonly OutlineEdge[]; readonly externals: readonly OutlineExternal[]; readonly diagnostics: readonly { readonly severity: string; readonly code: string; readonly message: string; readonly span: OutlineSpan | null }[] }
/** A declaration to write: `Modifiers Kind<Variant> Name { Field: Value ... }`. Values are Firmament source text. */
export interface DeclarationSpec { readonly kind: string; readonly variant?: string | null; readonly modifiers?: readonly string[] | null; readonly name?: string | null; readonly fields?: readonly { readonly name: string; readonly value: string }[] | null }
/** One structural edit, addressed by outline node ids. Operations apply in order; each later one sees the ids the earlier ones produced. */
export type LanguageEditOperation =
  | { readonly kind: 'setPatternSource' | 'setPatternBody'; readonly node: string; readonly value: string }
  | { readonly kind: 'setSetEntry'; readonly node: string; readonly field: string; readonly value: string }
  | { readonly kind: 'setValue'; readonly node: string; readonly value: string }
  | { readonly kind: 'setField'; readonly node: string; readonly field: string; readonly value: string }
  | { readonly kind: 'removeField'; readonly node: string; readonly field: string }
  | { readonly kind: 'insertDeclaration'; readonly parent: string | null; readonly declaration?: DeclarationSpec; readonly source?: string; readonly name?: string; readonly after?: string; readonly first?: boolean }
  | { readonly kind: 'removeDeclaration'; readonly node: string }
  | { readonly kind: 'renameDeclaration'; readonly node: string; readonly name: string }
  | { readonly kind: 'moveDeclaration'; readonly node: string; readonly after?: string; readonly first?: boolean }
  | { readonly kind: 'insertFeature'; readonly node: string; readonly source: string; readonly name?: string }
  | { readonly kind: 'extractFeature'; readonly node: string; readonly name: string; readonly parameters?: readonly string[] }
  | { readonly kind: 'nameFeature'; readonly node?: string; readonly span?: OutlineSpan; readonly name?: string }
  | { readonly kind: 'replaceDeclaration'; readonly node: string; readonly source: string }
  | { readonly kind: 'patternFeature'; readonly node: string; readonly name: string; readonly grid: { readonly columns: number; readonly rows: number; readonly pitchXmm: number; readonly pitchYmm: number } }
  | { readonly kind: 'extrudeProfile'; readonly node: string; readonly value: string; readonly name?: string }
  | { readonly kind: 'pocketProfile'; readonly node?: string; readonly parent: string; readonly value: string; readonly name?: string; readonly declaration?: DeclarationSpec }
  | { readonly kind: 'setPlacement'; readonly node: string; readonly placement: { readonly translationMm: readonly number[]; readonly axis: 'X' | 'Y' | 'Z'; readonly angleDegrees: number } };
/** The edited source and the single replacement `[start, start + length)` → `replacement` that produces it from the original. `nodes` holds the outline id each operation left behind (created, renamed or edited), in order; removals add none. */
export interface LanguageEdit { readonly document: string; readonly revision: string; readonly text: string; readonly changed: boolean; readonly edit: { readonly start: number; readonly length: number; readonly replacement: string }; readonly nodes: readonly string[]; readonly warnings: readonly string[] }
/** Something a visual editor can add. `placement` `model` inserts with `insertDeclaration`; `feature` with `insertFeature` on a body. `source` declares `nameStem`; pass a unique `name` with the insert. Every item is proven to build. */
export interface PaletteItem { readonly id: string; readonly label: string; readonly category: string; readonly description: string; readonly placement: 'model' | 'feature'; readonly nameStem: string; readonly source: string; readonly requiresEmptyModel?: boolean }
export interface LanguagePalette { readonly version: 'firmament-palette/1'; readonly items: readonly PaletteItem[] }
export interface PatternRow { readonly authored?: string | null; readonly key: string; readonly ordinal: number; readonly value: string; readonly coordinates: readonly number[] | null; readonly stableId: string | null; readonly generatedIds: readonly string[]; readonly span: OutlineSpan | null }
export interface PatternLayout { readonly node: string; readonly name: string; readonly elementType: string; readonly kind: string; readonly rows: readonly PatternRow[] }
export interface PatternApplication { readonly node: string; readonly name: string; readonly source: string | null; readonly body: string; readonly controls: readonly string[]; readonly consumers: readonly string[]; readonly readOnlyReason: string | null }
export interface PatternProjection { readonly version: 'firmament-patterns/1'; readonly document: string; readonly revision: string; readonly layouts: readonly PatternLayout[]; readonly patterns: readonly PatternApplication[]; readonly diagnostics: readonly { severity: string; code: string; message: string; span: OutlineSpan | null }[]; readonly validationLevel: 'LayoutResolvedNotBuilt' | 'Unresolved' }
export interface SketchControl { id: string; label: string; authored: string; value: number; unit: string; editable: boolean; parameter: string | null; readOnlyReason: string | null; span: { start: number; length: number } }
export interface SketchShape { id: string; name: string; kind: string; center: readonly number[]; controls: readonly SketchControl[] }
export interface SketchCurve {
  id: string; name: string; kind: string; points: readonly (readonly number[])[]; center: readonly number[] | null;
  radius: number; startAngle: number; sweep: number; minorRadius: number; rotation: number;
  /** Resolver evidence. Its sourceSpan can identify lowered source; use control spans for authored navigation. */
  provenance?: { stableId: string; conceptStableId: string; sourceSpan: string; derivation: string; sourceFrame: string; tracedFrom?: string | null; reversed: boolean; pipelineIndex?: number | null; invocationSourceRange?: string | null };
}
export interface SketchProjection {
  construction?: DraftConstruction; version: 'firmament-sketch/1'; document: string; revision: string; node: string | null;
  profiles: readonly { id: string; name: string }[]; emptyModels: readonly { id: string; name: string }[];
  openingReferences: readonly { id: string; name: string }[];
  frame: { name: string; origin: readonly number[]; x: readonly number[]; y: readonly number[]; normal: readonly number[] } | null;
  loops: readonly { name: string; isOuter: boolean; curves: readonly SketchCurve[] }[];
  shapes: readonly SketchShape[]; diagnostics: readonly { severity: string; code: string; message: string; span: { start: number; length: number } | null }[];
  validationLevel: 'ProfileResolvedNotBuilt' | 'Unresolved';
}
export interface SketchEditRequest { kind: 'dimensions' | 'create' | 'opening' | 'construction' | 'constructionDimensions'; construction?: DraftOperation; node?: string | null; shape?: string; changes?: readonly { control: string; value: number }[]; x?: number; y?: number; reference?: string; anchor?: string; diameter?: number; width?: number; height?: number }

export interface LanguageHover { readonly document: string; readonly revision: string; readonly start: number; readonly length: number; readonly title: string; readonly description: string }
export interface LanguageFormat { readonly document: string; readonly revision: string; readonly text: string; readonly changed: boolean }
export interface SemanticFieldSchema { readonly id: string; readonly name: string; readonly kind: string; readonly unit: 'None' | 'Length' | 'Angle'; readonly required: boolean; readonly default: string | null; readonly choices: readonly string[]; readonly variants?: readonly string[]; readonly description: string | null; readonly sourceEditable: boolean }
export interface SemanticOutputSchema { readonly id: string; readonly name: string; readonly kind: string; readonly sourceAddressable: boolean; readonly sourceRole: string | null }
export interface SemanticConstructSchema { readonly id: string; readonly name: string; readonly context: string | null; readonly entry: string | null; readonly description: string | null; readonly compatibilityAlias: string | null; readonly fields: readonly SemanticFieldSchema[]; readonly outputs: readonly SemanticOutputSchema[] }
export interface SemanticSchema { readonly version: 'firmament-semantic-schema/1'; readonly projectionVersion: 'firmament-field-projection/1'; readonly constructs: readonly SemanticConstructSchema[] }
export interface ProjectedValue { readonly valueKind: string; readonly text: string; readonly number?: number | null; readonly components?: readonly number[] | null; readonly unit?: string | null }
export interface FieldProjection { readonly fieldId: string; readonly name: string; readonly kind: string; readonly unit: string; readonly effectiveValue: ProjectedValue | null; readonly authoredValue: string | null; readonly origin: 'Authored' | 'Defaulted' | 'Derived' | 'Inherited' | 'Unavailable'; readonly declaration: SourceReference | null; readonly source: SourceReference | null; readonly editable: boolean; readonly readOnlyReason: string | null }
export interface ConstructProjection { readonly version: 'firmament-field-projection/1'; readonly constructId: string; readonly semanticId: string; readonly featureApplicationIdentity?: string | null; readonly sourceRevision: string; readonly buildRevision: number; readonly source: SourceReference | null; readonly fields: readonly FieldProjection[]; readonly outputs: readonly SemanticOutputSchema[] }
export interface FieldRewrite { readonly source: string; readonly sourceRevision: string; readonly replaced: SourceReference; readonly replacement: string }
export interface SelectorCandidate { readonly selector: string; readonly kind: string; readonly semanticKey: string | null; readonly outputRole: string | null; readonly source: SourceReference; readonly qualification: SourceAddressability; readonly buildRevision: number }
export interface AetherisOptions { readonly worker?: boolean; readonly wasmUrl?: string | URL; readonly diagnostics?: (diagnostics: readonly Diagnostic[]) => void }
export class AetherisError extends Error { readonly code: string; readonly details?: string }
export class Aetheris {
  static create(options?: AetherisOptions): Promise<Aetheris>;
  readonly runtimeInfo: RuntimeInfo;
  readonly workerTiming: WorkerTiming | null;
  readonly language: { patterns(source: string, options: LanguageOptions): Promise<PatternProjection>; sketch(source: string, node: string | null, options: LanguageOptions): Promise<SketchProjection>; sketchEdit(source: string, edit: SketchEditRequest, options: LanguageOptions): Promise<LanguageEdit>; complete(source: string, offset: number, options: LanguageOptions): Promise<LanguageCompletion>; analyze(source: string, options: LanguageOptions): Promise<LanguageAnalysis>; outline(source: string, options: LanguageOptions): Promise<LanguageOutline>; edit(source: string, operations: readonly LanguageEditOperation[], options: LanguageOptions): Promise<LanguageEdit>; palette(): Promise<LanguagePalette>; hover(source: string, offset: number, options: LanguageOptions): Promise<LanguageHover | null>; definition(source: string, offset: number, options: LanguageOptions): Promise<SourceReference | null>; format(source: string, options: LanguageOptions): Promise<LanguageFormat>; schema(): Promise<SemanticSchema> };
  info(): Promise<RuntimeInfo>;
  capabilities(): Promise<RuntimeCapabilities>;
  compile(source: string, options?: CompileOptions): Promise<{ model: ModelSession | null; diagnostics: readonly Diagnostic[] }>;
  dispose(): Promise<void>;
  terminate(): void;
}
export interface MotionClip { readonly name: string; readonly durationSeconds: number; readonly repeat: 'Once' | 'Loop'; readonly tracks: readonly { readonly name: string; readonly mateStableId: string; readonly jointName: string; readonly valueDimension: 'Angle' | 'Length' }[] }
export interface MotionPose { readonly revision: number; readonly motion: string; readonly timeSeconds: number; readonly state: Readonly<Record<string, number>>; readonly transforms: readonly { readonly id: string; readonly matrix: readonly number[] | Float64Array }[] }
export class ModelSession {
  readonly measurementAvailable?: boolean;
  readonly exportFormats: readonly ExportFormatOption[];
  /** One format returns its file; multiple formats return one ZIP. Rejected exports produce no files. */
  exportArtifacts(formats: readonly ExportFormat[], options?: OperationOptions): Promise<readonly ExportFile[]>;
  readonly motions: readonly MotionClip[];
  sampleMotion(motion: string, timeSeconds: number, options?: OperationOptions & { readonly preserveEndpoint?: boolean }): Promise<MotionPose>;
  measure(request: MeasurementRequest, options?: OperationOptions): Promise<MeasurementResult>;
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
export type ExportFormat = 'step' | 'glb' | 'usda' | 'stl' | 'obj' | 'pdf' | 'svg' | 'pptx';
export interface ExportFormatOption { readonly format: ExportFormat; readonly label: string; readonly description: string; readonly available: boolean; readonly reason?: string | null }
export interface ExportFile { readonly fileName: string; readonly mediaType: string; readonly bytes: Uint8Array }

export type MeasurementMode = 'points' | 'planes' | 'axes';
export interface MeasurementPick { occurrenceId: string | null; faceId: number | null; rayOrigin: number[]; rayDirection: number[] }
export interface MeasurementRequest { mode: MeasurementMode; first: MeasurementPick; second?: MeasurementPick; allowApproximate: boolean }
export interface MeasurementReference { occurrenceId: string; faceId: number; surfaceKind: string; point: number[]; quality: string; diameterMm?: number | null }
export interface MeasurementResult {
  mode: MeasurementMode; first: MeasurementReference; second?: MeasurementReference | null;
  distanceMm: number | null; deltaMm: number[] | null; witnessA: number[] | null; witnessB: number[] | null;
  angleDegrees: number | null; quality: string; explanation: string;
}

export interface DraftElement { id: string; name: string; kind: string; parent: string | null; span: { start: number; length: number }; fields: readonly { name: string; value: string; number: number | null; unit: string | null; editable: boolean }[] }
export interface DraftTrace { reference: string; from?: string; to?: string; alias?: string; reverse?: boolean; connect?: boolean; wholeLoop?: boolean }
export interface DraftOperation { action: 'layout' | 'point' | 'line' | 'circle' | 'path' | 'step' | 'trace' | 'extrude' | 'movePoint' | 'dimension' | 'rectangle' | 'shape' | 'parameter'; width?: number; height?: number; reference?: string; insetX?: number; insetY?: number; sharedInset?: boolean; targetProfile?: string; field?: string; value?: number; parent?: string; name?: string; kind?: string; plane?: string; x?: number; y?: number; length?: number; radius?: number; turn?: number; from?: string; to?: string; trace?: readonly DraftTrace[] }
export interface DraftSpan { id: string; owner: string; reference: string; from: string; to: string; curve: SketchCurve }
export interface DraftConstruction { shapes?: readonly SketchShape[]; spans?: readonly DraftSpan[]; frames: readonly { owner: string; frame: NonNullable<SketchProjection['frame']> }[]; elements: readonly DraftElement[]; points: readonly { name: string; x: number; y: number }[]; guides: readonly SketchCurve[]; diagnostics: readonly SketchProjection['diagnostics'][number][]; validationLevel: 'ConstructionResolvedNotTraced' | 'Unresolved' }
