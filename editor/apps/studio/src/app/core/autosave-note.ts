/** What autosave said about a document, as the Activity panel keeps it (#192). */
export interface AutosaveNote {
  readonly tone: 'done' | 'warning';
  readonly text: string;
  /** The event it came from, so that two alike are still two. */
  readonly sequence: number;
}

/** What an autosave event says about a document, by its name. */
export function autosaveNote(
  name: string,
  event: { kind: string; label?: string | null; sequence: number }
): AutosaveNote {
  return event.kind === 'autosaved'
    ? { tone: 'done', text: `Autosave kept the unsaved edits to ${name}.`, sequence: event.sequence }
    : {
        tone: 'warning',
        text: `Autosave could not keep the edits to ${name}${event.label ? `: ${event.label}` : '.'}`,
        sequence: event.sequence
      };
}
