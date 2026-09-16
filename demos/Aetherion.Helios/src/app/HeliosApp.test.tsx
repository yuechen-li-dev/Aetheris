import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeModel, ok } from '../test/fakes';

const { runtimeMock } = vi.hoisted(() => ({ runtimeMock: { info: { packageVersion: '2.0.0-preview.3' }, initialize: vi.fn(), open: vi.fn(), setSource: vi.fn(), setProperty: vi.fn(), rebuild: vi.fn(), exportSTEP: vi.fn(), dispose: vi.fn(), session: vi.fn() } }));
vi.mock('../sdk/CadRuntime', () => ({ WebSdkCadRuntime: class { constructor() { return runtimeMock; } } }));
vi.mock('../viewport/Viewport', () => ({ Viewport: ({ onSelect }: { onSelect(id: string, face: string): void }) => <button data-testid="viewport" onClick={() => onSelect('box:plate', 'face:top')}>Viewport geometry</button> }));

import { HeliosApp } from './HeliosApp';
const model = fakeModel();

describe('Helios shell', () => {
  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); vi.stubGlobal('URL', { ...URL, createObjectURL: vi.fn(() => 'blob:helios-test'), revokeObjectURL: vi.fn() }); vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined); runtimeMock.open.mockResolvedValue({ model, diagnostics: [] }); runtimeMock.setSource.mockResolvedValue(ok()); runtimeMock.setProperty.mockResolvedValue(ok()); runtimeMock.rebuild.mockResolvedValue(ok()); runtimeMock.exportSTEP.mockResolvedValue(new Uint8Array([1])); runtimeMock.session.mockReturnValue(model); });
  it('initializes with the bracket through the runtime boundary', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); expect(screen.getByText('HELIOS')).toBeVisible(); expect(screen.getAllByText('WebBracket').length).toBeGreaterThan(0); });
  it('ships Mars as the default theme', async () => { render(<HeliosApp />); await waitFor(() => expect(document.documentElement.dataset.theme).toBe('mars')); expect(screen.getByLabelText('Theme')).toHaveValue('mars'); });
  it('switches and persists Sirius', async () => { render(<HeliosApp />); fireEvent.change(screen.getByLabelText('Theme'), { target: { value: 'sirius' } }); await waitFor(() => expect(localStorage.getItem('helios-theme')).toBe('sirius')); });
  it('synchronizes viewport selection to tree and inspector', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); fireEvent.click(screen.getByTestId('viewport')); expect(screen.getByText('Plate', { selector: '.entity-hero strong' })).toBeVisible(); expect(screen.getByText(/face:top/)).toBeVisible(); });
  it('synchronizes tree selection to the inspector', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); fireEvent.click(screen.getByText('CenterMount')); expect(screen.getByText('hole:center')).toBeVisible(); });
  it('sends source replacement through the public session', async () => { render(<HeliosApp />); const source = await screen.findByLabelText('Firmament source'); fireEvent.change(source, { target: { value: 'Model Changed {}' } }); fireEvent.click(screen.getByText('↻ Rebuild')); await waitFor(() => expect(runtimeMock.setSource).toHaveBeenCalledWith('Model Changed {}', 'editable-bracket.firmament')); });
  it('opens the assembly fixture', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); fireEvent.click(screen.getByText('ASSEMBLY')); await waitFor(() => expect(runtimeMock.open).toHaveBeenLastCalledWith(expect.stringContaining('Assembly WebBlockPair'), 'shared-block-assembly.firmament')); });
  it('authors a canonical Hole in source rather than geometry', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); fireEvent.click(screen.getByRole('button', { name: /Hole/ })); expect((screen.getByLabelText('Firmament source') as HTMLTextAreaElement).value).toContain('Hole<Shaft> HeliosHole1'); expect(runtimeMock.setSource).not.toHaveBeenCalled(); });
  it('keeps sheet metal explicitly unavailable', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); expect(screen.getByText('SHEET METAL')).toBeDisabled(); });
  it('exports STEP bytes through the public runtime boundary', async () => { render(<HeliosApp />); await waitFor(() => expect(runtimeMock.open).toHaveBeenCalled()); fireEvent.click(screen.getByRole('button', { name: /Export STEP/ })); await waitFor(() => expect(runtimeMock.exportSTEP).toHaveBeenCalledOnce()); expect(URL.createObjectURL).toHaveBeenCalled(); });
});
