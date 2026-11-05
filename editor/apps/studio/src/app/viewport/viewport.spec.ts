import { parseHeader } from './frame-transport';
import { DefaultCamera, orbit, zoom } from './orbit';

function header(width: number, height: number, magic = 0x464e5450): ArrayBuffer {
  const bytes = new ArrayBuffer(32);
  const view = new DataView(bytes);
  view.setUint32(0, magic, true);
  view.setUint32(4, 7, true);
  view.setUint32(8, width, true);
  view.setUint32(12, height, true);
  view.setUint32(16, 3, true);
  view.setUint32(20, 2500, true);
  return bytes;
}

describe('parseHeader', () => {
  it('reads the header the Server writes', () => {
    expect(parseHeader(header(2, 4))).toEqual({ sequence: 7, width: 2, height: 4, input: 3, renderMs: 2.5 });
  });

  it('refuses bytes that are not a frame header', () => {
    expect(parseHeader(new ArrayBuffer(8))).toBeNull();
    expect(parseHeader(header(2, 2, 0x12345678))).toBeNull();
  });
});

describe('orbit', () => {
  it('turns with the drag and stops short of the poles', () => {
    const right = orbit(DefaultCamera, 100, 0);
    expect(right.yaw).toBeLessThan(DefaultCamera.yaw);
    expect(right.pitch).toBe(DefaultCamera.pitch);

    expect(orbit(DefaultCamera, 0, 100_000).pitch).toBeLessThan(Math.PI / 2);
    expect(orbit(DefaultCamera, 0, -100_000).pitch).toBeGreaterThan(-Math.PI / 2);
  });

  it('zooms by a share of the distance, inside its limits', () => {
    expect(zoom(DefaultCamera, 100).distance).toBeGreaterThan(DefaultCamera.distance);
    expect(zoom(DefaultCamera, 100_000).distance).toBeLessThanOrEqual(12);
    expect(zoom(DefaultCamera, -100_000).distance).toBeGreaterThan(1);
  });
});
