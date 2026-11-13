/**
 * Which HID devices the studio may read: 3Dconnexion's 3D mice (#163), and nothing else. The studio reads them through
 * WebHID, as raw reports, so no vendor SDK or driver is linked: 3Dconnexion's SDK is not under a licence that can ship
 * with GPL code, and a 3D mouse's reports are plain HID. With the vendor's own driver running, both read the same
 * reports.
 */

/** 3Dconnexion's own vendor ID, which every device it has made since 2011 carries. */
const ThreeDconnexion = 0x256f;
/** Logitech's, which 3Dconnexion's older devices carry. */
const Logitech = 0x046d;
/** The older devices' product IDs under Logitech's vendor ID, from the CadMan to the SpaceMouse Pro. */
const LogitechSpaceMice = new Set([
  0xc603, 0xc605, 0xc606, 0xc608, 0xc621, 0xc623, 0xc625, 0xc626, 0xc627, 0xc628, 0xc629, 0xc62b
]);

/** Whether a HID device is a 3D mouse the studio may read. */
export function isSpaceMouse(device: { vendorId: number; productId: number }): boolean {
  return (
    device.vendorId === ThreeDconnexion || (device.vendorId === Logitech && LogitechSpaceMice.has(device.productId))
  );
}
