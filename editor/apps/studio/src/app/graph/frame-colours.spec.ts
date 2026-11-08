import { FrameColours, frameColourName, frameShown } from './frame-colours';

describe('frame colours', () => {
  it('offers plain colours a file can hold and any client can draw (#225)', () => {
    expect(FrameColours).toHaveLength(8);
    for (const colour of FrameColours) {
      expect(colour).toMatch(/^#[0-9a-f]{6}$/u);
    }
  });

  it('shows a colour of the ramp as its chart slot, which follows the theme, and any other as itself', () => {
    expect(frameShown('#1baf7a', 0)).toBe('var(--color-chart-3)');
    expect(frameShown('#1BAF7A', 0)).toBe('var(--color-chart-3)');
    expect(frameShown('#123456', 0)).toBe('#123456');
    expect(frameShown('', 9)).toBe('var(--color-chart-2)');
  });

  it('names Auto, the ramp by number, and another colour by its value', () => {
    expect(frameColourName('')).toBe('Auto');
    expect(frameColourName('#eb6834')).toBe('Colour 2');
    expect(frameColourName('#123456')).toBe('#123456');
  });
});
