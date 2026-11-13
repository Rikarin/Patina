import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import type { ViewportCameraState } from '@patina/api-client';
import { orbit, pan, zoom } from './orbit';

/**
 * What a 3D mouse's cap is doing (#163), each axis -1 to 1: pushed right, away and down, and tipped forward, rolled
 * right and twisted clockwise seen from above. HID's own axes, as the device reports them.
 */
export interface SpaceMotion {
  readonly move: readonly [number, number, number];
  readonly turn: readonly [number, number, number];
}

export const Still: SpaceMotion = { move: [0, 0, 0], turn: [0, 0, 0] };

/** How far a cap goes, in the counts a report gives: about 350 either way. */
const FullTravel = 350;
/** A cap at rest is never quite still: less than this share of its travel is nothing. */
const DeadZone = 0.04;
/** What a cap at full travel does in a second: in a drag's pixels for a turn and a slide, and a wheel's for a zoom. */
const TurnPerSecond = 320;
const SlidePerSecond = 420;
const ZoomPerSecond = 900;
/** The longest a frame counts for, so a stall does not throw the camera across the room. */
const LongestFrame = 0.05;

/** HID's usage page and usage for a multi-axis controller, which a 3D mouse's motion is. */
const GenericDesktop = 0x01;
const MultiAxisController = 0x08;

/** The WebHID that the studio reads a 3D mouse through: only the part it uses, since TypeScript's DOM has none. */
interface HidReportEvent extends Event {
  readonly reportId: number;
  readonly data: DataView;
}

interface HidDevice extends EventTarget {
  readonly opened: boolean;
  readonly productName: string;
  readonly collections: readonly { usagePage?: number; usage?: number }[];
  open(): Promise<void>;
  close(): Promise<void>;
}

interface Hid extends EventTarget {
  getDevices(): Promise<HidDevice[]>;
}

/** One axis of a report, as a share of the cap's travel, with the dead zone taken out. */
function axis(data: DataView, at: number): number {
  if (data.byteLength < at + 2) {
    return 0;
  }

  const share = Math.max(-1, Math.min(1, data.getInt16(at, true) / FullTravel));
  return Math.abs(share) < DeadZone ? 0 : share;
}

/**
 * What a report says the cap is doing, from what it was doing. Report 1 is the push, and on newer devices the turn after
 * it in the same report; report 2 is the turn on older ones; report 3, the buttons, moves nothing.
 */
export function readReport(reportId: number, data: DataView, held: SpaceMotion): SpaceMotion {
  switch (reportId) {
    case 1:
      return {
        move: [axis(data, 0), axis(data, 2), axis(data, 4)],
        turn: data.byteLength >= 12 ? [axis(data, 6), axis(data, 8), axis(data, 10)] : held.turn
      };
    case 2:
      return { ...held, turn: [axis(data, 0), axis(data, 2), axis(data, 4)] };
    default:
      return held;
  }
}

/** Which buttons a button report holds down, as bits from the first. */
export function readButtons(reportId: number, data: DataView): number | null {
  return reportId === 3 && data.byteLength >= 1
    ? data.getUint8(0) | (data.byteLength >= 2 ? data.getUint8(1) << 8 : 0)
    : null;
}

/** Whether the cap is doing anything at all. */
export function moving(motion: SpaceMotion): boolean {
  return [...motion.move, ...motion.turn].some(value => value !== 0);
}

/**
 * The camera after a cap has been held for some seconds, as the model in hand: twisting it spins the model and tipping
 * it forward tips the model the same way, sliding it sideways or up and down slides the model, and pushing it away
 * moves the model away. The pointer's own settings for turning apply to the cap as well.
 */
export function spaceStep(
  camera: ViewportCameraState,
  motion: SpaceMotion,
  seconds: number,
  turnScale = 1
): ViewportCameraState {
  const time = Math.min(seconds, LongestFrame);
  const [right, away, down] = motion.move;
  const [forward, , twist] = motion.turn;
  const turn = TurnPerSecond * time * turnScale;
  const slide = SlidePerSecond * time;
  let next = orbit(camera, twist * turn, forward * turn);
  next = pan(next, right * slide, down * slide);
  return zoom(next, away * ZoomPerSecond * time);
}

/** Who moves with the cap: each frame it is held, once when it is let go, and on each button pressed. */
export interface SpaceListener {
  move(motion: SpaceMotion, seconds: number): void;
  still(): void;
  press(button: number): void;
}

/**
 * A 3D mouse, read as raw HID reports (#163). The desktop app lets the studio read 3Dconnexion's devices without asking,
 * so they are found when the studio starts and when one is plugged in. Elsewhere, or with none, this does nothing. The
 * viewport listens, and turns its camera each frame while the cap is held; the first button frames the model.
 */
@Injectable({ providedIn: 'root' })
export class SpaceMouse {
  /** The device's name while one is read, or null. */
  readonly device = signal<string | null>(null);
  private motion: SpaceMotion = Still;
  private buttons = 0;
  private listener: SpaceListener | null = null;
  private frame = 0;
  private last = 0;
  private readonly opened = new Set<HidDevice>();
  private readonly report = (event: Event) => this.read(event as HidReportEvent);
  private readonly plugged = () => void this.find();

  constructor() {
    const hid = (globalThis.navigator as { hid?: Hid } | undefined)?.hid;
    if (!hid) {
      return;
    }

    hid.addEventListener('connect', this.plugged);
    hid.addEventListener('disconnect', this.plugged);
    void this.find();
    inject(DestroyRef).onDestroy(() => {
      hid.removeEventListener('connect', this.plugged);
      hid.removeEventListener('disconnect', this.plugged);
      cancelAnimationFrame(this.frame);
      for (const device of this.opened) {
        device.removeEventListener('inputreport', this.report);
        void device.close().catch(() => undefined);
      }
    });
  }

  /** Says who moves with the cap, until `destroyRef` ends: one listener, the viewport. */
  listen(listener: SpaceListener, destroyRef: DestroyRef): void {
    this.listener = listener;
    destroyRef.onDestroy(() => {
      if (this.listener === listener) {
        this.listener = null;
      }
    });
  }

  private async find(): Promise<void> {
    const hid = (globalThis.navigator as { hid?: Hid } | undefined)?.hid;
    let devices: HidDevice[];
    try {
      devices = (await hid?.getDevices()) ?? [];
    } catch {
      return; // no permission to read any device is no 3D mouse
    }

    for (const device of this.opened) {
      if (!devices.includes(device)) {
        this.opened.delete(device);
      }
    }

    for (const device of devices) {
      const multiAxis = device.collections.some(
        found => found.usagePage === GenericDesktop && found.usage === MultiAxisController
      );
      if (!multiAxis || this.opened.has(device)) {
        continue;
      }

      try {
        if (!device.opened) {
          await device.open();
        }

        device.addEventListener('inputreport', this.report);
        this.opened.add(device);
      } catch {
        // A device another program holds alone cannot be read; the rest still can.
      }
    }

    this.device.set([...this.opened][0]?.productName ?? null);
  }

  private read(event: HidReportEvent): void {
    const buttons = readButtons(event.reportId, event.data);
    if (buttons !== null) {
      const pressed = buttons & ~this.buttons;
      this.buttons = buttons;
      for (let button = 0; button < 16; button++) {
        if (pressed & (1 << button)) {
          this.listener?.press(button);
        }
      }

      return;
    }

    this.motion = readReport(event.reportId, event.data, this.motion);
    if (moving(this.motion) && this.frame === 0) {
      this.last = performance.now();
      this.frame = requestAnimationFrame(now => this.tick(now));
    }
  }

  /** Moves the camera once a frame while the cap is held, by how long the frame was. */
  private tick(now: number): void {
    this.frame = 0;
    if (!moving(this.motion)) {
      this.listener?.still();
      return;
    }

    this.listener?.move(this.motion, (now - this.last) / 1000);
    this.last = now;
    this.frame = requestAnimationFrame(next => this.tick(next));
  }
}
