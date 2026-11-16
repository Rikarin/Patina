import { Injectable, inject } from '@angular/core';
import { unwrap } from '@patina/api-client';
import { LibraryStore } from './library-store';

/**
 * Project templates (#144): a project's settings kept under a name in the library, which New Project starts from. The
 * Server keeps them as files; this only asks.
 */
@Injectable({ providedIn: 'root' })
export class ProjectTemplates {
  private readonly library = inject(LibraryStore);

  /** The templates the library keeps, by name, or none when it cannot be asked. */
  async names(): Promise<string[]> {
    try {
      const client = await this.library.api();
      return unwrap(await client.GET('/api/v1/library/project-templates')).names;
    } catch {
      return [];
    }
  }

  /** Keeps a project's settings as a template of this name, replacing one of the same name. */
  async save(project: string, name: string): Promise<string[]> {
    const client = await this.library.api();
    return unwrap(await client.POST('/api/v1/library/project-templates', { body: { project, name } })).names;
  }
}
