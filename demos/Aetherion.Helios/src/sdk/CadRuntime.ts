import { Aetheris, type Diagnostic, type ModelSession, type RuntimeInfo, type UnitValue } from '@aetheris/cad';

export interface OpenResult { model: ModelSession | null; diagnostics: readonly Diagnostic[] }

export interface CadRuntime {
  readonly info: RuntimeInfo | null;
  initialize(): Promise<RuntimeInfo>;
  open(source: string, sourceName: string): Promise<OpenResult>;
  setSource(source: string, sourceName: string): ReturnType<ModelSession['setSource']>;
  setProperty(id: string, value: UnitValue): ReturnType<ModelSession['setProperty']>;
  rebuild(): ReturnType<ModelSession['rebuild']>;
  exportSTEP(): Promise<Uint8Array>;
  dispose(): Promise<void>;
  session(): ModelSession | null;
}

export class WebSdkCadRuntime implements CadRuntime {
  private cad: Aetheris | null = null;
  private model: ModelSession | null = null;
  info: RuntimeInfo | null = null;

  async initialize() {
    this.cad ??= await Aetheris.create();
    this.info = await this.cad.info();
    return this.info;
  }

  async open(source: string, sourceName: string) {
    await this.initialize();
    await this.model?.dispose();
    const result = await this.cad!.compile(source, { sourceName });
    this.model = result.model;
    return result;
  }

  setSource(source: string, sourceName: string) { return this.requireModel().setSource(source, { sourceName }); }
  setProperty(id: string, value: UnitValue) { return this.requireModel().setProperty(id, value); }
  rebuild() { return this.requireModel().rebuild(); }
  exportSTEP() { return this.requireModel().exportSTEP(); }
  session() { return this.model; }

  async dispose() {
    await this.model?.dispose();
    await this.cad?.dispose();
    this.model = null;
    this.cad = null;
  }

  private requireModel() {
    if (!this.model) throw new Error('No Firmament model is open.');
    return this.model;
  }
}
