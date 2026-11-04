import type { Connection, ViewportInput } from '@patina/api-client';

/** One frame: RGBA, tightly packed, top row first. `pixels` is valid until the next frame. */
export interface ViewportFrame {
  sequence: number;
  width: number;
  height: number;
  /** The newest input the frame includes. */
  input: number;
  renderMs: number;
  pixels: Uint8Array;
}

/** The binary header in front of a frame's pixels (ViewportSession.HeaderBytes). */
const HeaderBytes = 32;
const Magic = 0x464e5450; // "PTNF", little-endian
const ReconnectMs = 500;

/** Decodes one binary message, or returns null for one that is not a frame of a size it claims. */
export function parseFrame(message: ArrayBuffer): ViewportFrame | null {
  if (message.byteLength < HeaderBytes) {
    return null;
  }

  const header = new DataView(message, 0, HeaderBytes);
  const width = header.getUint32(8, true);
  const height = header.getUint32(12, true);
  if (header.getUint32(0, true) !== Magic || message.byteLength !== HeaderBytes + width * height * 4) {
    return null;
  }

  return {
    sequence: header.getUint32(4, true),
    width,
    height,
    input: header.getUint32(16, true),
    renderMs: header.getUint32(20, true) / 1000,
    pixels: new Uint8Array(message, HeaderBytes)
  };
}

/**
 * How frames reach the pane and input reaches the Server. WebSocket is the one implementation (ADR 0003); Electron's
 * `sharedTexture` would be a second behind this interface, and the pane would not change.
 */
export interface FrameTransport {
  /** Sends input; dropped while disconnected, since the next input carries the whole camera anyway. */
  send(input: ViewportInput): void;
  close(): void;
}

export type FrameTransportFactory = (
  connection: Connection,
  onFrame: (frame: ViewportFrame) => void,
  onOpen: () => void
) => FrameTransport;

/** Binary frames over `/ws/viewport`, reconnecting for as long as it is wanted. */
export class WebSocketFrameTransport implements FrameTransport {
  private socket: WebSocket | null = null;
  private closed = false;
  private retry: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly connection: Connection,
    private readonly onFrame: (frame: ViewportFrame) => void,
    private readonly onOpen: () => void
  ) {
    this.connect();
  }

  send(input: ViewportInput): void {
    if (this.socket?.readyState === WebSocket.OPEN) {
      this.socket.send(JSON.stringify(input));
    }
  }

  close(): void {
    this.closed = true;
    if (this.retry !== null) {
      clearTimeout(this.retry);
    }

    this.socket?.close();
  }

  private connect(): void {
    const base = this.connection.baseUrl.replace(/^http/, 'ws');
    const token = this.connection.token ? `?access_token=${encodeURIComponent(this.connection.token)}` : '';
    const socket = new WebSocket(`${base}/ws/viewport${token}`);
    socket.binaryType = 'arraybuffer';
    socket.onopen = () => this.onOpen();
    socket.onmessage = event => {
      const frame = event.data instanceof ArrayBuffer ? parseFrame(event.data) : null;
      if (frame) {
        this.onFrame(frame);
      }
    };
    socket.onclose = () => {
      if (!this.closed) {
        this.retry = setTimeout(() => this.connect(), ReconnectMs);
      }
    };
    this.socket = socket;
  }
}
