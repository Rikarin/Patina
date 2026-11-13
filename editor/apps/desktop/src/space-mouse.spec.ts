import { describe, expect, it } from 'vitest';
import { isSpaceMouse } from './space-mouse';

describe('isSpaceMouse', () => {
  it('takes 3Dconnexion’s devices, old and new, and nothing else of Logitech’s', () => {
    expect(isSpaceMouse({ vendorId: 0x256f, productId: 0xc635 })).toBe(true);
    expect(isSpaceMouse({ vendorId: 0x046d, productId: 0xc626 })).toBe(true);
    expect(isSpaceMouse({ vendorId: 0x046d, productId: 0xc52b })).toBe(false);
    expect(isSpaceMouse({ vendorId: 0x054c, productId: 0x0ce6 })).toBe(false);
  });
});
