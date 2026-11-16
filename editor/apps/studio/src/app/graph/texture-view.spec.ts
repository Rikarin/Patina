import { physicalText, precisionOf, proportionsOf, storedText } from './texture-view';

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

describe('the 2D View in the graph physical proportions (#184)', () => {
  it('draws a square map of a wall twice as wide as it is tall, its width kept for the zoom readout', () => {
    expect(proportionsOf({ width: 1024, height: 1024 }, { width: 200, height: 100 })).toEqual({
      width: 1024,
      height: 512
    });
  });

  it('draws a picture in its own pixels without a physical size', () => {
    expect(proportionsOf({ width: 512, height: 256 }, null)).toEqual({ width: 512, height: 256 });
  });

  it('says a physical size in the largest unit that keeps it at one or more', () => {
    expect(physicalText({ width: 200, height: 50 })).toBe('2.0 m × 50 cm');
  });
});
