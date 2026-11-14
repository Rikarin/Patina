import { Injectable, Injector, inject } from '@angular/core';
import { unwrap } from '@patina/api-client';
import { Activity } from '../core/activity';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { messageOf } from '../core/messages';
import { SystemStatus } from '../core/system-status';
import { GraphViews } from '../graph/graph-views';
import { LibraryStore } from './library-store';
import { ProjectStore } from './project-store';

/**
 * The File commands about the document in front. A new or reopened project's tab shows before it is in front, while
 * the viewport lets go of what was (`DELETE /viewport/mesh`), and a command sent in that moment would find nothing
 * open. So each is on while a tab is on its way, and waits for it before acting.
 */
@Injectable({ providedIn: 'root' })
export class ProjectCommands {
  private readonly commands = inject(Commands);
  private readonly project = inject(ProjectStore);
  private readonly status = inject(SystemStatus);
  private readonly graphs = inject(GraphViews);
  private readonly library = inject(LibraryStore);
  /** Asked for when there is something to report, so that installing the commands starts nothing. */
  private readonly injector = inject(Injector);
  private installed = false;

  install(): void {
    if (this.installed) {
      return;
    }

    this.installed = true;
    const project = this.project;
    const arriving = () => project.arriving() !== null;
    const inFront = async (then: () => unknown) => {
      await project.inFront();
      return then();
    };
    const handle = (id: string, enabled: () => boolean, run: () => unknown) =>
      this.commands.handle(id, { enabled, run });
    const bridge = desktop();

    handle(
      'import',
      () => bridge !== undefined && (project.current() !== null || arriving()),
      async () => {
        const path = await bridge?.openFile(this.status.pictures(), this.status.meshes());
        return path ? project.importFiles([path]) : undefined;
      }
    );
    handle(
      'importMaterials',
      () => bridge !== undefined && (project.current() !== null || arriving()),
      async () => {
        const path = await bridge?.openMaterial(this.status.materials());
        return path ? project.importMaterials(path) : undefined;
      }
    );
    handle(
      'importPackage',
      () => bridge !== undefined,
      async () => {
        const path = await bridge?.openPackage();
        return path ? this.importPackage(path) : undefined;
      }
    );
    handle(
      'reloadMesh',
      () => project.mesh() !== null || arriving(),
      () => inFront(() => (project.mesh() ? project.reloadMesh() : undefined))
    );
    handle(
      'projectSettings',
      () => project.current() !== null || arriving(),
      () => inFront(() => project.settingsOpen.set(project.current() !== null))
    );
    handle(
      'save',
      () => project.active() !== null || arriving(),
      () => {
        // Leaving a box commits what it holds, so that this saves what the user can see.
        const focused = globalThis.document.activeElement;
        if (focused instanceof HTMLElement && focused.matches('input, textarea')) {
          focused.blur();
        }

        // A graph beside the one in front that was touched last is the one Save means (#40).
        const front = this.graphs.front();
        if (front !== this.graphs.main && front.document() !== null) {
          return front.save();
        }

        return inFront(() => (project.active() ? project.saveActive() : undefined));
      }
    );
    handle(
      'saveAs',
      () => project.active() !== null || arriving(),
      () => inFront(() => project.savingAs.set(project.active() !== null))
    );
    handle(
      'saveAll',
      () => project.unsavedTabs().length > 0,
      () => project.saveAll()
    );
    handle(
      'closeDocument',
      () => project.active() !== null || arriving(),
      () =>
        inFront(() => {
          const path = project.active()?.path;
          return path ? project.closeTab(path) : undefined;
        })
    );
    handle(
      'closeProject',
      () => (project.active()?.project ?? null) !== null || arriving(),
      () => project.closeProject()
    );
  }

  /**
   * Brings a zipped package into the library's packages (#185). The Server reads all of it before writing any of it,
   * and says why when it will not; every project's shelf offers it once it is in.
   */
  private async importPackage(path: string): Promise<void> {
    try {
      const client = await this.library.api();
      const imported = unwrap(await client.POST('/api/v1/library/packages/import', { body: { source: path } }));
      const name = imported.manifest.name;
      this.injector.get(Activity).report({
        tone: 'done',
        source: 'Packages',
        text: imported.replaced
          ? `${name} replaced the package of its name, and every project’s shelf offers it.`
          : `${name} is in the library’s packages, and every project’s shelf offers it.`
      });
    } catch (failure) {
      this.injector.get(Activity).report({ tone: 'error', source: 'Packages', text: messageOf(failure) });
    }
  }
}
