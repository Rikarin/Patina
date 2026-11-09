import { Injectable, inject } from '@angular/core';
import { Commands } from '../core/commands';
import { PaintStore, type PaintTool } from './paint-store';

const RadiusStep = 1.25;

/** The largest size the slider and `[`/`]` reach, which is short of the Server's 4096. */
export const MaximumRadius = 1024;

const Tools: Readonly<Record<string, PaintTool>> = {
  brushTool: 'brush',
  eraserTool: 'eraser',
  smudgeTool: 'smudge',
  cloneTool: 'clone',
  projectionTool: 'projection',
  fillTool: 'fill',
  pickerTool: 'picker',
  lineTool: 'line',
  pathTool: 'path'
};

/** The painting commands that are the store's: the tool, the brush's size and picking a mask colour. */
@Injectable({ providedIn: 'root' })
export class PaintCommands {
  private readonly commands = inject(Commands);
  private readonly paint = inject(PaintStore);
  private installed = false;

  install(): void {
    if (this.installed) {
      return;
    }

    this.installed = true;
    const open = () => this.paint.state() !== null;
    for (const [id, tool] of Object.entries(Tools)) {
      this.commands.handle(id, {
        enabled: open,
        checked: () => open() && this.paint.tool() === tool,
        run: () => this.paint.tool.set(tool)
      });
    }

    const resize = (by: number) => {
      const radius = this.paint.radius() * by;
      this.paint.radius.set(Math.max(1, Math.min(MaximumRadius, Math.round(radius))));
    };
    this.commands.handle('smallerBrush', { enabled: () => this.paint.radius() > 1 && open(), run: () => resize(1 / RadiusStep) });
    this.commands.handle('largerBrush', { enabled: () => this.paint.radius() < MaximumRadius && open(), run: () => resize(RadiusStep) });
    this.commands.handle('findLayer', {
      enabled: () => open() && (this.paint.state()?.layers.length ?? 0) > 0,
      checked: () => this.paint.finding(),
      run: () => this.paint.finding.set(!this.paint.finding())
    });
    this.commands.handle('setCloneSource', {
      enabled: open,
      checked: () => this.paint.settingCloneSource(),
      run: () => {
        this.paint.tool.set('clone');
        this.paint.settingCloneSource.set(!this.paint.settingCloneSource());
      }
    });
    this.commands.handle('toggleStencil', {
      enabled: () => open() && this.paint.screenPicture() !== null,
      checked: () => this.paint.stencil(),
      run: () => this.paint.stencil.set(!this.paint.stencil())
    });
    this.commands.handle('pickIdColour', {
      enabled: () => open() && this.paint.layer() !== null,
      run: () => this.paint.picking.set(this.paint.layer())
    });
  }
}
