import { Injectable, inject } from '@angular/core';
import { Commands } from '../core/commands';
import { PaintStore, type PaintTool } from './paint-store';

const RadiusStep = 1.25;

const Tools: Readonly<Record<string, PaintTool>> = {
  brushTool: 'brush',
  eraserTool: 'eraser',
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
      this.paint.radius.set(Math.max(1, Math.min(256, Math.round(radius))));
    };
    this.commands.handle('smallerBrush', { enabled: () => this.paint.radius() > 1 && open(), run: () => resize(1 / RadiusStep) });
    this.commands.handle('largerBrush', { enabled: () => this.paint.radius() < 256 && open(), run: () => resize(RadiusStep) });
    this.commands.handle('pickIdColour', {
      enabled: () => open() && this.paint.layer() !== null,
      run: () => this.paint.picking.set(this.paint.layer())
    });
  }
}
