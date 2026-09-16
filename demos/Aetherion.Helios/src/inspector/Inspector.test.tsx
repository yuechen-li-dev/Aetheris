import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Inspector } from './Inspector';
import { fakeModel, plate, width } from '../test/fakes';

describe('semantic inspector', () => {
  it('shows identity and introspected property', () => { render(<Inspector model={fakeModel()} entityId={plate.id} diagnostics={[]} onApply={async () => true} />); expect(screen.getByText(plate.id)).toBeVisible(); expect(screen.getByLabelText('Width')).toHaveValue('50'); });
  it('commits a unit-aware value only on Apply', async () => { const apply = vi.fn(async () => true); render(<Inspector model={fakeModel()} entityId={plate.id} diagnostics={[]} onApply={apply} />); fireEvent.change(screen.getByLabelText('Width'), { target: { value: '80' } }); expect(apply).not.toHaveBeenCalled(); fireEvent.click(screen.getByText('Apply')); await waitFor(() => expect(apply).toHaveBeenCalledWith(width, { value: 80, unit: 'mm' })); });
  it('keeps invalid draft visibly correctable', async () => { render(<Inspector model={fakeModel()} entityId={plate.id} diagnostics={[]} onApply={async () => false} />); fireEvent.change(screen.getByLabelText('Width'), { target: { value: '-8' } }); fireEvent.click(screen.getByText('Apply')); expect(await screen.findByText(/last valid geometry remains/i)).toBeVisible(); expect(screen.getByLabelText('Width')).toHaveValue('-8'); });
});
