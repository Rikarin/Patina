import type { components } from '../generated/schema';

export type SystemInfo = components['schemas']['SystemInfo'];
export type WorkerStatus = components['schemas']['WorkerStatus'];
export type ViewportState = components['schemas']['ViewportState'];
export type ViewportCameraState = components['schemas']['ViewportCameraState'];

/** What a client sends over `/ws/viewport`. WebSockets are outside the OpenAPI document, so this is written by hand. */
export interface ViewportInput {
  camera?: ViewportCameraState;
  width?: number;
  height?: number;
  input?: number;
  interacting?: boolean;
}
