import { evaluateCurve, formatCurves, parseCurves, straightCurves } from './curve';
import { computeHistogram, isolateChannel, peakOf } from './histogram';
import { blackToWhite, colourAt, cssOfRamp, formatRamp, parseRamp, roundPosition } from './ramp';

describe('curves', () => {
  it('says nothing for a straight curve, and reads nothing as the identity', () => {
    expect(formatCurves(straightCurves)).toBe('');
    expect(parseCurves('')).toBe(straightCurves);
  });

  it('round-trips the channels it was given and leaves the others straight', () => {
    const text = 'curve:r=0 1,1 0;a=0 0,0.5 0.8,1 1';
    const curves = parseCurves(text);
    expect(curves?.g).toEqual(straightCurves.g);
    expect(curves ? formatCurves(curves) : null).toBe(text);
  });

  it('refuses text that is not its own, such as a path to a file', () => {
    expect(parseCurves('curves/rust.vxcurve')).toBeNull();
    expect(parseCurves('curve:r=0')).toBeNull();
    expect(parseCurves('curve:x=0 0,1 1')).toBeNull();
  });

  it('draws what the bake computes: straight through two keys, flat at a hump', () => {
    const keys = [
      { x: 0, y: 0 },
      { x: 0.5, y: 1 },
      { x: 1, y: 0 }
    ];
    expect(evaluateCurve(straightCurves.r, 0.25)).toBeCloseTo(0.25);
    expect(evaluateCurve(keys, 0.5)).toBe(1);
    expect(evaluateCurve(keys, 0.49)).toBeLessThan(1);
    expect(evaluateCurve(keys, -1)).toBe(0);
  });
});

describe('ramps', () => {
  it('reads nothing as black to white, and round-trips in order', () => {
    expect(parseRamp('')).toBe(blackToWhite);
    const stops = parseRamp('ramp:1 ffffffff,0 ff0000ff');
    expect(stops?.map(stop => stop.position)).toEqual([0, 1]);
    expect(stops ? formatRamp(stops) : null).toBe('ramp:0 ff0000ff,1 ffffffff');
  });

  it('refuses text that is not its own', () => {
    expect(parseRamp('ramps/rust.png')).toBeNull();
    expect(parseRamp('ramp:0 red')).toBeNull();
  });

  it('mixes between neighbours, and holds past the ends', () => {
    expect(colourAt(blackToWhite, 0.5)).toBe('808080FF');
    const inset = [{ position: 0.25, colour: '102030FF' }];
    expect(colourAt(inset, 0)).toBe('102030FF');
    expect(colourAt(inset, 1)).toBe('102030FF');
  });

  // A hand's raw position came back from the Server rounded, and that change wrote over what was being typed in the box.
  it('keeps a position as the file does, so the echo of an edit changes nothing', () => {
    const position = roundPosition(0.26418);
    const stops = parseRamp(formatRamp([{ position, colour: 'A0522DFF' }]));

    expect(position).toBe(0.264);
    expect(stops?.[0]?.position).toBe(position);
  });

  it('shows as a CSS gradient', () => {
    expect(cssOfRamp(blackToWhite)).toBe('linear-gradient(to right, #000000FF 0%, #FFFFFFFF 100%)');
  });
});

describe('histograms', () => {
  const pixels = new Uint8ClampedArray([255, 0, 0, 255, 255, 255, 0, 10]);

  it('counts each level once per pixel', () => {
    const histogram = computeHistogram(pixels);
    expect(histogram.r[255]).toBe(2);
    expect(histogram.g[0]).toBe(1);
    expect(histogram.b[0]).toBe(2);
    expect(peakOf(histogram)).toBe(2);
  });

  it('never divides by nothing', () => {
    expect(peakOf(computeHistogram(new Uint8ClampedArray(0)))).toBe(1);
  });

  it('shows one channel as opaque grey, and leaves the whole picture alone', () => {
    expect([...isolateChannel(pixels, 'a')]).toEqual([255, 255, 255, 255, 10, 10, 10, 255]);
    expect(isolateChannel(pixels, 'rgba')).toBe(pixels);
  });
});
