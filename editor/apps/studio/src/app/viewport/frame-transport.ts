import type { Connection, ViewportInput } from '@patina/api-client';

export interface FrameHeader {
  sequence: number;
  width: number;
  height: number;
  /** The newest input the frame includes. */
  input: number;
  renderMs: number;
  /** The newest revision of the stack the viewport wears that this frame shows, or 0. */
  painted: number;
}

/** One frame: RGBA, tightly packed, top row first. `pixels` is valid until the next frame arrives. */
export interface ViewportFrame extends FrameHeader {
  pixels: Uint8Array;
}

/** The binary header in front of a frame's pixels (ViewportSession.HeaderBytes). */
export const HeaderBytes = 32;
const Magic = 0x464e5450; // "PTNF", little-endian
const ReconnectMs = 500;

/** Decodes a frame header, or returns null for bytes that are not one. */
export function parseHeader(bytes: ArrayBuffer): FrameHeader | null {
  if (bytes.byteLength < HeaderBytes) {
    return null;
  }

  const header = new DataView(bytes, 0, HeaderBytes);
  if (header.getUint32(0, true) !== Magic) {
    return null;
  }

  return {
    sequence: header.getUint32(4, true),
    width: header.getUint32(8, true),
    height: header.getUint32(12, true),
    input: header.getUint32(16, true),
    renderMs: header.getUint32(20, true) / 1000,
    painted: header.getUint32(24, true)
  };
}

/**
 * How frames reach the pane and input reaches the Server. One implementation today (ADR 0003); Electron's
 * `sharedTexture` or a shared-memory path would be a second, and the pane would not change.
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

/** Fills `[from, to)` of a buffer from a byte stream; a BYOB read hands the buffer back, so the result is the live one. */
async function readInto(
  reader: ReadableStreamBYOBReader,
  buffer: ArrayBuffer,
  from: number,
  to: number
): Promise<ArrayBuffer | null> {
  let live = buffer;
  let at = from;
  while (at < to) {
    const { value, done } = await reader.read(new Uint8Array(live, at, to - at));
    if (done) {
      return null;
    }

    live = value.buffer;
    at += value.byteLength;
  }

  return live;
}

/**
 * Frames from the long `GET /api/v1/viewport/frames` response, read into two buffers in turn, so a frame stays whole
 * while the next one arrives; input over `/ws/viewport`. A browser reads a response body about four times faster than
 * WebSocket messages at this size, which is the difference between 56 fps and 10 at 1440p (ADR 0003).
 */
export class StreamFrameTransport implements FrameTransport {
  private socket: WebSocket | null = null;
  private readonly abort = new AbortController();
  private closed = false;
  private retry: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly connection: Connection,
    private readonly onFrame: (frame: ViewportFrame) => void,
    private readonly onOpen: () => void
  ) {
    this.connect();
    void this.receive();
  }

  send(input: ViewportInput): void {
    if (this.socket?.readyState === WebSocket.OPEN) {
      this.socket.send(JSON.stringify(input));
    }
  }

  close(): void {
    this.closed = true;
    this.abort.abort();
    if (this.retry !== null) {
      clearTimeout(this.retry);
    }

    this.socket?.close();
  }

  private connect(): void {
    const base = this.connection.baseUrl.replace(/^http/, 'ws');
    const token = this.connection.token ? `&access_token=${encodeURIComponent(this.connection.token)}` : '';
    const socket = new WebSocket(`${base}/ws/viewport?frames=false${token}`);
    socket.onopen = () => this.onOpen();
    socket.onclose = () => {
      if (!this.closed) {
        this.retry = setTimeout(() => this.connect(), ReconnectMs);
      }
    };
    this.socket = socket;
  }

  private get wanted(): boolean {
    return !this.closed;
  }

  private async receive(): Promise<void> {
    while (this.wanted) {
      try {
        await this.stream();
      } catch {
        // The Server went away, or is restarting: the loop tries again.
      }

      await new Promise(done => setTimeout(done, ReconnectMs));
    }
  }

  private async stream(): Promise<void> {
    const headers: Record<string, string> = this.connection.token
      ? { Authorization: `Bearer ${this.connection.token}` }
      : {};
    const response = await fetch(`${this.connection.baseUrl}/api/v1/viewport/frames`, {
      headers,
      signal: this.abort.signal
    });
    const reader = response.body?.getReader({ mode: 'byob' });
    if (!response.ok || !reader) {
      throw new Error(`The frame stream answered ${response.status}.`);
    }

    let head: ArrayBuffer | null = new ArrayBuffer(HeaderBytes);
    let [next, spare] = [new ArrayBuffer(0), new ArrayBuffer(0)];
    for (;;) {
      head = await readInto(reader, head, 0, HeaderBytes);
      const header = head && parseHeader(head);
      if (!head || !header) {
        return;
      }

      const bytes = header.width * header.height * 4;
      const filled = await readInto(reader, next.byteLength === bytes ? next : new ArrayBuffer(bytes), 0, bytes);
      if (!filled) {
        return;
      }

      next = spare;
      spare = filled;
      this.onFrame({ ...header, pixels: new Uint8Array(filled) });
    }
  }
}
