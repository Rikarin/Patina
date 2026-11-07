import type { ViewportCameraState } from '@patina/api-client';

const RadiansPerPixel = 0.008;
const ZoomPerWheelPixel = 0.001;
const MaxPitch = 1.5;
const MinDistance = 0.05;
const PanPerPixel = 0.0012;
const MaxDistance = 12;

const PixelsPerKey = 24;
const WheelPixelsPerKey = 120;
const ArrowSteps: Readonly<Record<string, readonly [number, number]>> = {
  ArrowLeft: [-1, 0],
  ArrowRight: [1, 0],
  ArrowUp: [0, -1],
  ArrowDown: [0, 1]
};

export const DefaultCamera: ViewportCameraState = {
  yaw: 0.6,
  pitch: 0.35,
  distance: 2.2,
  panX: 0,
  panY: 0,
  panZ: 0,
  exposure: 1,
  lightRotation: 0
};

/** A drag turns the model under the pointer: right spins it right, down tips it towards the viewer. */
export function orbit(camera: ViewportCameraState, dx: number, dy: number): ViewportCameraState {
  const pitch = Math.max(-MaxPitch, Math.min(MaxPitch, camera.pitch + dy * RadiansPerPixel));
  return { ...camera, yaw: camera.yaw - dx * RadiansPerPixel, pitch };
}

/** The wheel moves the camera by a share of its distance, so zooming feels the same near and far. */
export function zoom(camera: ViewportCameraState, wheelPixels: number): ViewportCameraState {
  const distance = camera.distance * Math.exp(wheelPixels * ZoomPerWheelPixel);
  return { ...camera, distance: Math.max(MinDistance, Math.min(MaxDistance, distance)) };
}

/** A drag slides the model with the pointer, in the plane the camera faces, at any distance. */
export function pan(camera: ViewportCameraState, dx: number, dy: number): ViewportCameraState {
  const step = PanPerPixel * camera.distance;
  const [sinYaw, cosYaw, sinPitch, cosPitch] = [
    Math.sin(camera.yaw),
    Math.cos(camera.yaw),
    Math.sin(camera.pitch),
    Math.cos(camera.pitch)
  ];
  return {
    ...camera,
    panX: camera.panX - step * (dx * cosYaw - dy * sinPitch * sinYaw),
    panY: camera.panY + step * dy * cosPitch,
    panZ: camera.panZ + step * (dx * sinYaw + dy * sinPitch * cosYaw)
  };
}

/** What one press of a key does to the camera, or nothing for a key that does not move it. Shift pans the arrows. */
export function nudge(camera: ViewportCameraState, key: string, shift: boolean): ViewportCameraState | undefined {
  const arrow = ArrowSteps[key];
  if (arrow) {
    return (shift ? pan : orbit)(camera, arrow[0] * PixelsPerKey, arrow[1] * PixelsPerKey);
  }

  switch (key) {
    case '+':
    case '=':
      return zoom(camera, -WheelPixelsPerKey);
    case '-':
      return zoom(camera, WheelPixelsPerKey);
    default:
      return undefined;
  }
}

/** Back to the whole model, where the view started. */
export function framed(): ViewportCameraState {
  return DefaultCamera;
}
