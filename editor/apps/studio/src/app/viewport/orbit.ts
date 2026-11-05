import type { ViewportCameraState } from '@patina/api-client';

const RadiansPerPixel = 0.008;
const ZoomPerWheelPixel = 0.001;
const MaxPitch = 1.5;
const MinDistance = 1.2;
const MaxDistance = 12;

export const DefaultCamera: ViewportCameraState = { yaw: 0.6, pitch: 0.35, distance: 2.2 };

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
