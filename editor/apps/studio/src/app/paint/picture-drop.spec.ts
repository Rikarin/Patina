import { describe, expect, it, vi } from 'vitest';
import { DragType as LibraryDrag } from '../library/library-tree';
import { carriesPicture, droppedPicture, overPictureSlot } from './picture-drop';

/** A drag event carrying these types, with the library tree's list of paths when it has one. */
function drag(types: string[], listed?: string[]): DragEvent {
  const data = {
    types,
    files: [] as File[],
    dropEffect: 'none',
    getData: (type: string) => (type === LibraryDrag && listed ? JSON.stringify(listed) : '')
  };
  return { dataTransfer: data, preventDefault: vi.fn(), stopPropagation: vi.fn() } as unknown as DragEvent;
}

describe('a picture slot takes a drop (#122)', () => {
  it('takes a drag of a file or from the library, and nothing else', () => {
    expect(carriesPicture(drag(['Files']))).toBe(true);
    expect(carriesPicture(drag([LibraryDrag]))).toBe(true);
    expect(carriesPicture(drag(['text/plain']))).toBe(false);

    const over = drag([LibraryDrag]);
    overPictureSlot(over);
    expect(over.preventDefault).toHaveBeenCalled();
    expect(over.dataTransfer?.dropEffect).toBe('copy');
    const other = drag(['text/plain']);
    overPictureSlot(other);
    expect(other.preventDefault).not.toHaveBeenCalled();
  });

  it('reads the first picture the library tree lists, in the formats the Server reads', () => {
    expect(droppedPicture(drag([LibraryDrag], ['projects/crate/rust.png', 'b.png']), ['.png', '.jpg'])).toBe(
      'projects/crate/rust.png'
    );
    expect(droppedPicture(drag([LibraryDrag], ['projects/crate/crate.vxlayers']), ['.png'])).toBeNull();
    expect(droppedPicture(drag([LibraryDrag]), ['.png'])).toBeNull();
  });
});
