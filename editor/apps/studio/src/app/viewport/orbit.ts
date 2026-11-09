import type { ViewportCameraState } from '@patina/api-client';

const RadiansPerPixel = 0.008;
const ZoomPerWheelPixel = 0.001;
/** Distances are in framings: one is the whole model just inside the pane, whatever its shape. */
const MinDistance = 0.02;
/** A pan is in bounding radii and a distance in framings, about three radii at the opening lens. */
const PanPerPixel = 0.0035;
const MaxDistance = 6;
/** The distance the Server opens at and F goes back to, with a little room round the model. */
export const Framed = 1.05;
/** Straight down or up. The camera's up is the way it moves as it tips, so it has one even at the pole. */
const Pole = Math.PI / 2;

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
  distance: Framed,
  panX: 0,
  panY: 0,
  panZ: 0
};

/** The views a key or a button snaps to: yaw and pitch, with the model's front facing +z. */
export const AxisViews = {
  front: { yaw: 0, pitch: 0 },
  back: { yaw: Math.PI, pitch: 0 },
  right: { yaw: Math.PI / 2, pitch: 0 },
  left: { yaw: -Math.PI / 2, pitch: 0 },
  top: { yaw: 0, pitch: Pole },
  bottom: { yaw: 0, pitch: -Pole }
} as const;

export type AxisView = keyof typeof AxisViews;

/** Looks along an axis at the whole model, as Blender's number pad does. */
export function snapped(camera: ViewportCameraState, view: AxisView): ViewportCameraState {
  return { ...framed(camera), ...AxisViews[view] };
}

/**
 * A drag turns the model under the pointer: right spins it right, down tips it towards the viewer. The pitch is free over
 * the poles, as the Server's camera is (#137): past straight down the view turns over, so a sideways drag spins the other
 * way round the up axis to keep the model moving with the pointer. The pitch stays within a turn either side.
 */
export function orbit(camera: ViewportCameraState, dx: number, dy: number): ViewportCameraState {
  const upright = Math.cos(camera.pitch) >= 0 ? 1 : -1;
  return {
    ...camera,
    yaw: camera.yaw - dx * RadiansPerPixel * upright,
    pitch: wrapped(camera.pitch + dy * RadiansPerPixel)
  };
}

/** An angle brought within half a turn of zero. */
function wrapped(angle: number): number {
  return angle - 2 * Math.PI * Math.round(angle / (2 * Math.PI));
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

/** The whole model in the pane, looked at from where the camera is: the distance and the pan go back, the angle stays. */
export function framed(camera: ViewportCameraState = DefaultCamera): ViewportCameraState {
  return { ...camera, distance: Framed, panX: 0, panY: 0, panZ: 0 };
}

/** What a key on the number pad snaps to, as in Blender: 1 front, 3 right, 7 top, and Ctrl for the opposite side. */
export function padView(code: string, opposite: boolean): AxisView | undefined {
  switch (code) {
    case 'Numpad1':
      return opposite ? 'back' : 'front';
    case 'Numpad3':
      return opposite ? 'left' : 'right';
    case 'Numpad7':
      return opposite ? 'bottom' : 'top';
    default:
      return undefined;
  }
}
