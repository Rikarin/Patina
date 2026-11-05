/** The row a key moves to in a list of `total` rows, or null when the key is not for moving. */
export function nextIndex(key: string, index: number | null, total: number, rowsPerPage: number): number | null {
  if (total === 0) {
    return null;
  }

  const last = total - 1;
  const current = index ?? -1;
  switch (key) {
    case 'ArrowDown':
      return Math.min(current + 1, last);
    case 'ArrowUp':
      return Math.max(index === null ? 0 : current - 1, 0);
    case 'PageDown':
      return Math.min(current + rowsPerPage, last);
    case 'PageUp':
      return Math.max(current - rowsPerPage, 0);
    case 'Home':
      return 0;
    case 'End':
      return last;
    default:
      return null;
  }
}

/** "1 project", "2,048 projects". */
export function counted(count: number, noun: string): string {
  return `${count.toLocaleString('en')} ${noun}${count === 1 ? '' : 's'}`;
}
