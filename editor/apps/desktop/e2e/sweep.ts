import { sweepScratch } from './projects';

/** Once before the specs run: what earlier runs could not remove (#294). */
export default function sweep(): void {
  sweepScratch();
}
