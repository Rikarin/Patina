# ADR 0034: A 3D mouse, read as plain HID

**Status:** accepted, 2026-11-13.

## What is wrong

Artists who model and paint keep a 3D mouse in the other hand, and the viewport could not read one (#163). 3Dconnexion makes
nearly all of them. Its SDK, 3DxWare's navigation library, comes under 3Dconnexion's own licence, which forbids passing it on
outside that licence's terms and is not one GPL-3.0 code can be shipped with. The SDK also drives the camera itself, through a
navigation model the application describes to it, which would put a second way of moving the camera beside the viewport's.

## Decision

1. **The studio reads the device's HID reports itself, through WebHID.** A 3D mouse is a USB or Bluetooth HID multi-axis
   controller: report 1 is the cap's push (and, on devices since about 2011, its turn after it), report 2 the turn on older ones,
   report 3 the buttons, each axis a little-endian 16-bit count of about ±350. That is the device's own protocol, read the same way by Linux's
   open spacenavd driver, and no vendor code is linked or shipped.
2. **The desktop app grants 3Dconnexion's devices and nothing else.** `setDevicePermissionHandler` lets the studio's own origin
   read a HID device whose vendor is 3Dconnexion (`0x256f`), or Logitech (`0x046d`) with one of the older 3D mice's product IDs.
   The studio never asks for a device, so no picker shows and no other device can be read.
3. **The cap moves the camera as a drag does.** It goes through `orbit`, `pan` and `zoom` in `orbit.ts`, scaled by how long each
   frame was, with the pointer's turn speed from Preferences applied. Twisting spins the model, tipping it forward tips the model,
   sliding slides it, and pushing away zooms out. The first button frames the model, as F does.

## Consequences

- With 3Dconnexion's driver running too, both read the same reports, so nothing has to be turned off. A device the driver holds
  alone on some system cannot be opened; then the viewport simply does not move with it.
- Outside the desktop app a browser would have to ask for the device; the studio does not, so a web build reads none.
- The axes' signs follow the HID reports' and are not yet checked on every device. The cap's buttons beyond the first, its own
  speed and its axes' directions are not settings yet.
