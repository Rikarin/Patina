import type { components } from '../generated/schema';

export type SystemInfo = components['schemas']['SystemInfo'];
export type WorkerStatus = components['schemas']['WorkerStatus'];
export type LibraryInfo = components['schemas']['LibraryInfo'];
export type ProjectSummary = components['schemas']['ProjectSummary'];
export type ProjectPage = components['schemas']['ProjectPage'];
export type ProjectCreateRequest = components['schemas']['ProjectCreateRequest'];
export type AssetSummary = components['schemas']['AssetSummary'];
export type AssetPage = components['schemas']['AssetPage'];
export type AssetImpact = components['schemas']['AssetImpact'];
export type ImpactedProject = components['schemas']['ImpactedProject'];
export type TagCount = components['schemas']['TagCount'];
export type TagPage = components['schemas']['TagPage'];
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
