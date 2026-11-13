/**
 * The steps of a quit, from asking about unsaved edits to the process's end, each a line on standard output with the
 * time since the quit began. A quit that stalls then says where it stopped: the e2e tests that wait for the app to end
 * print these lines when they give up (#236).
 */
export class QuitTrace {
  static readonly Prefix = 'Patina quit:';
  private started: number | null = null;

  constructor(
    private readonly write: (line: string) => void = line => process.stdout.write(`${line}\n`),
    private readonly now: () => number = () => performance.now()
  ) {}

  step(what: string): void {
    this.started ??= this.now();
    this.write(`${QuitTrace.Prefix} ${what} (+${Math.round(this.now() - this.started)} ms)`);
  }
}
