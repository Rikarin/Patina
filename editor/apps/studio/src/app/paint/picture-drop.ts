import { desktop } from '../core/desktop';
import { DragType as LibraryDrag } from '../library/library-tree';
import { isPicture } from './surface-frame';

/** Whether a drag carries something a picture slot might take: a file from this machine, or one from the library. */
export function carriesPicture(event: DragEvent): boolean {
  const types = event.dataTransfer?.types ?? [];
  return types.includes('Files') || types.includes(LibraryDrag);
}

/**
 * Lets a picture slot take a drag (#122): the drop is the slot's, not the window's, which would bring a file into the
 * project, and it copies rather than moves.
 */
export function overPictureSlot(event: DragEvent): void {
  if (!carriesPicture(event)) {
    return;
  }

  event.preventDefault();
  event.stopPropagation();
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'copy';
  }
}

/**
 * The picture a drop carries, as a path on the Server's machine: a file from this machine, or the first path from the
 * library tree. Null when it carries none, or something that is not a picture in one of the formats given. Read it in
 * the drop handler before anything awaits, since the drop's data is gone after.
 */
export function droppedPicture(event: DragEvent, formats: readonly string[] | null | undefined): string | null {
  event.preventDefault();
  event.stopPropagation();
  const file = event.dataTransfer?.files[0] ?? null;
  const source = file ? (desktop()?.pathOf(file) ?? null) : listed(event.dataTransfer?.getData(LibraryDrag) ?? '');
  return source && isPicture(source, formats) ? source : null;
}

/** The first path a drag from the library tree lists, or null. */
function listed(data: string): string | null {
  try {
    const paths: unknown = JSON.parse(data || 'null');
    return Array.isArray(paths) && typeof paths[0] === 'string' ? paths[0] : null;
  } catch {
    return null;
  }
}
