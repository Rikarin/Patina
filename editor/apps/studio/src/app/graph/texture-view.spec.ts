import { precisionOf, storedText } from './texture-view';

describe('the 2D View readout at the picture precision (#184)', () => {
  it('shows a stored texel to five places, unclamped, where the preview rounds to a byte', () => {
    const text = storedText({
      node: 2,
      port: 'Out',
      x: 12,
      y: 40,
      width: 512,
      height: 512,
      format: 'Rgba16Float',
      values: [0.123456, -0.5, 1.75, 1]
    });

    expect(text).toBe('12, 40  0.12346 -0.50000 1.75000 1.00000');
  });

  it('names what each format holds of a lane', () => {
    expect(precisionOf('R8')).toBe('8 bits');
    expect(precisionOf('Rgba8')).toBe('8 bits');
    expect(precisionOf('R16Float')).toBe('16-bit float');
    expect(precisionOf('Rgba32Float')).toBe('32-bit float');
  });
});
