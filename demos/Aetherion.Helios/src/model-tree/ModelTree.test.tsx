import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ModelTree } from './ModelTree';
import { hole, plate, root } from '../test/fakes';

const tree = { rootId: root.id, nodes: [root, plate, hole] };
describe('model tree', () => {
  it('renders the SDK semantic hierarchy', () => { render(<ModelTree tree={tree} selectedId={null} onSelect={() => {}} />); expect(screen.getByText('WebBracket')).toBeVisible(); expect(screen.getByText('CenterMount')).toBeVisible(); });
  it('routes selection by semantic id', () => { const select = vi.fn(); render(<ModelTree tree={tree} selectedId={null} onSelect={select} />); fireEvent.click(screen.getByText('Plate')); expect(select).toHaveBeenCalledWith(plate.id); });
  it('derives broad semantic kind options from the public tree', () => {
    render(<ModelTree tree={tree} selectedId={null} onSelect={() => {}} />);
    expect(screen.getByLabelText('Filter model by kind')).toHaveTextContent('AllBoxHoleModel');
  });
  it('filters by semantic kind and keeps the matching ancestor path', () => {
    render(<ModelTree tree={tree} selectedId={null} onSelect={() => {}} />);
    fireEvent.change(screen.getByLabelText('Filter model by kind'), { target: { value: 'Hole' } });
    expect(screen.getByText('WebBracket')).toBeVisible();
    expect(screen.getByText('CenterMount')).toBeVisible();
    expect(screen.queryByText('Plate')).not.toBeInTheDocument();
  });
  it('composes semantic kind and text filters and restores all kinds', () => {
    render(<ModelTree tree={tree} selectedId={null} onSelect={() => {}} />);
    fireEvent.change(screen.getByLabelText('Filter model by kind'), { target: { value: 'Hole' } });
    fireEvent.change(screen.getByLabelText('Filter model'), { target: { value: 'plate' } });
    expect(screen.queryByText('CenterMount')).not.toBeInTheDocument();
    expect(screen.queryByText('Plate')).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Filter model by kind'), { target: { value: '' } });
    expect(screen.getByText('Plate')).toBeVisible();
  });
});
