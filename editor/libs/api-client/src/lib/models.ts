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
export type GraphState = components['schemas']['GraphState'];
export type GraphNodeState = components['schemas']['GraphNodeState'];
export type GraphEdgeState = components['schemas']['GraphEdgeState'];
export type GraphInterfacePort = components['schemas']['GraphInterfacePort'];
export type GraphNodeType = components['schemas']['GraphNodeType'];
export type GraphPortDefinition = components['schemas']['GraphPortDefinition'];
export type GraphSettingDefinition = components['schemas']['GraphSettingDefinition'];
export type GraphGroupState = components['schemas']['GraphGroupState'];
export type GraphCommentState = components['schemas']['GraphCommentState'];
export type GraphCompound = components['schemas']['GraphCompound'];
export type GraphDiagnostic = components['schemas']['GraphDiagnostic'];
export type GraphPreview = components['schemas']['GraphPreview'];
export type GraphHistoryState = components['schemas']['GraphHistoryState'];
export type GraphEvaluation = components['schemas']['GraphEvaluation'];
export type GraphDelta = components['schemas']['GraphDelta'];
export type GraphCommand = components['schemas']['GraphCommand'];
export type GraphEditResult = components['schemas']['GraphEditResult'];

/**
 * One event of a graph session. Its schema comes from `/graphs/{id}/events`, and `/ws/graphs/{id}?since=` sends the same
 * events, a text message each, as they happen. The WebSocket is outside the OpenAPI document.
 */
export type GraphEvent = components['schemas']['GraphEvent'];

export type BakeRequest = components['schemas']['BakeRequest'];
export type BakeResult = components['schemas']['BakeResult'];
export type BakedMapFile = components['schemas']['BakedMapFile'];
export type JobPage = components['schemas']['JobPage'];
export type JobState = components['schemas']['JobState'];

/** One change to a job. `/jobs/events` has its schema, and `/ws/jobs?since=` sends the same events as they happen. */
export type JobEvent = components['schemas']['JobEvent'];

/** What a client sends over `/ws/viewport`. WebSockets are outside the OpenAPI document, so this is written by hand. */
export interface ViewportInput {
  camera?: ViewportCameraState;
  width?: number;
  height?: number;
  input?: number;
  interacting?: boolean;
}
export type StackState = components['schemas']['StackState'];
export type StackLayer = components['schemas']['StackLayer'];
export type StackPreview = components['schemas']['StackPreview'];
export type StackEvent = components['schemas']['StackEvent'];
export type StackCommandBatch = components['schemas']['StackCommandBatch'];
export type StackStamp = components['schemas']['StackStamp'];
export type StackView = components['schemas']['StackView'];
export type StackBrush = components['schemas']['StackBrush'];
export type StackMask = components['schemas']['StackMask'];
export type StackVocabulary = components['schemas']['StackVocabulary'];
export type StackCommand = StackCommandBatch['commands'][number];
export type ViewportPick = components['schemas']['ViewportPick'];
export type StackEditResult = components['schemas']['StackEditResult'];
