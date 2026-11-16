import type { MeshCameraFacts, ViewportCameraState } from '@patina/api-client';

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

/** The lens range the Server takes for the field of view. */
const Lenses = { min: 0.1, max: 2.6 } as const;

/** Where a file's camera puts the orbit camera, and the lens it sees through. */
export interface FileView {
  camera: ViewportCameraState;
  fieldOfView: number;
  orthographic: boolean;
}

type Triple = readonly [number, number, number];

function triple(values: readonly number[]): Triple {
  return [values[0] ?? 0, values[1] ?? 0, values[2] ?? 0];
}

function dot(a: Triple, b: Triple): number {
  return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}

/** Half the angle across the picture's narrower side, as the Server's orbit camera measures a framing. */
function narrower(fieldOfView: number, aspect: number): number {
  const half = fieldOfView / 2;
  return Math.min(half, Math.atan(aspect * Math.tan(half)));
}

/**
 * The orbit camera that looks through a mesh file's camera (#144, ADR 0065). It looks the way the file's camera looks and
 * circles the point on its line of sight nearest the model's centre, so it stands where the file's camera stands and the
 * model stays the thing it turns round. The file's vertical angle becomes the lens; an orthographic camera keeps the
 * pane's lens and sees as tall a slab as the file's. A file camera's roll is not kept, since the orbit's up is the
 * world's, and the pane's shape, not the file's, says how wide the picture is. A model behind the camera is circled at
 * its own distance ahead.
 *
 * @param file The file's camera, in the units of the box.
 * @param minimum The least corner of the box round the model.
 * @param maximum The greatest corner.
 * @param aspect The pane's width over its height.
 * @param lens The field of view the viewport has now, which an orthographic camera keeps.
 */
export function throughCamera(
  file: MeshCameraFacts,
  minimum: readonly number[],
  maximum: readonly number[],
  aspect: number,
  lens: number
): FileView {
  const [low, high] = [triple(minimum), triple(maximum)];
  const centre: Triple = [(low[0] + high[0]) / 2, (low[1] + high[1]) / 2, (low[2] + high[2]) / 2];
  const radius = Math.max(Math.hypot(high[0] - low[0], high[1] - low[1], high[2] - low[2]) / 2, 1e-4);
  const eye = triple(file.position);
  const forward = triple(file.forward);
  const toCentre: Triple = [centre[0] - eye[0], centre[1] - eye[1], centre[2] - eye[2]];
  const along = dot(toCentre, forward);
  const reach = along > 1e-4 * radius ? along : Math.max(Math.hypot(...toCentre), radius);
  const target = forward.map((lane, axis) => (eye[axis] ?? 0) + lane * reach);
  const orthographic = file.orthographicHeight > 0;
  const fieldOfView = orthographic ? lens : Math.min(Lenses.max, Math.max(Lenses.min, file.fieldOfView));
  const seen = orthographic ? file.orthographicHeight / 2 / Math.tan(fieldOfView / 2) : reach;
  const distance = (seen * Math.sin(narrower(fieldOfView, aspect))) / radius;
  return {
    camera: {
      yaw: Math.atan2(-forward[0], -forward[2]),
      pitch: Math.asin(Math.max(-1, Math.min(1, -forward[1]))),
      distance: Math.max(MinDistance, Math.min(MaxDistance, distance)),
      panX: ((target[0] ?? 0) - centre[0]) / radius,
      panY: ((target[1] ?? 0) - centre[1]) / radius,
      panZ: ((target[2] ?? 0) - centre[2]) / radius
    },
    fieldOfView,
    orthographic
  };
}
