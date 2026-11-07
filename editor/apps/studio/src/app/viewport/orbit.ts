import type { ViewportCameraState } from '@patina/api-client';

const RadiansPerPixel = 0.008;
const ZoomPerWheelPixel = 0.001;
const MaxPitch = 1.5;
const MinDistance = 0.05;
const PanPerPixel = 0.0012;
const MaxDistance = 12;

export const DefaultCamera: ViewportCameraState = { yaw: 0.6, pitch: 0.35, distance: 2.2, panX: 0, panY: 0, panZ: 0 };

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

/** Back to the whole model, where the view started. */
export function framed(): ViewportCameraState {
  return DefaultCamera;
}
