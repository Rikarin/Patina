import { parseFrame } from './frame-transport';
import { DefaultCamera, orbit, zoom } from './orbit';

function frame(width: number, height: number, magic = 0x464e5450): ArrayBuffer {
  const message = new ArrayBuffer(32 + width * height * 4);
  const header = new DataView(message);
  header.setUint32(0, magic, true);
  header.setUint32(4, 7, true);
  header.setUint32(8, width, true);
  header.setUint32(12, height, true);
  header.setUint32(16, 3, true);
  header.setUint32(20, 2500, true);
  return message;
}

describe('parseFrame', () => {
  it('reads the header the Server writes', () => {
    const parsed = parseFrame(frame(2, 2));

    expect(parsed).toMatchObject({ sequence: 7, width: 2, height: 2, input: 3, renderMs: 2.5 });
    expect(parsed?.pixels.length).toBe(16);
  });

  it('refuses a message that is not a frame', () => {
    expect(parseFrame(new ArrayBuffer(8))).toBeNull();
    expect(parseFrame(frame(2, 2, 0x12345678))).toBeNull();
  });

  it('refuses a frame shorter than its size says', () => {
    expect(parseFrame(frame(2, 2).slice(0, 40))).toBeNull();
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
