import { Injectable, inject } from '@angular/core';
import { Commands } from '../core/commands';
import { desktop } from '../core/desktop';
import { SystemStatus } from '../core/system-status';
import { GraphViews } from '../graph/graph-views';
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
      () =>
        inFront(() => {
          project.copying.set(false);
          project.savingAs.set(project.active() !== null);
        })
    );
    handle(
      'saveCopy',
      () => project.active() !== null || arriving(),
      () =>
        inFront(() => {
          project.copying.set(true);
          project.savingAs.set(project.active() !== null);
        })
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
}
