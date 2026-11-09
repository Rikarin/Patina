import {
  matAutoFixOff,
  matBrush,
  matColorize,
  matContentCopy,
  matFormatColorFill,
  matGesture,
  matHorizontalRule,
  matPolyline,
  matWallpaper
} from '@ng-icons/material-icons/baseline';
import type { FillMode, PaintTool } from './paint-store';

/**
 * The tools, by the command that chooses each, in the order the strip and Properties show them. The command table names
 * them and says their keys (core/command-table.ts), so a label or a tooltip here would be a second copy.
 */
export const PaintTools: readonly { id: PaintTool; command: string; icon: string }[] = [
  { id: 'brush', command: 'brushTool', icon: 'matBrush' },
  { id: 'eraser', command: 'eraserTool', icon: 'matAutoFixOff' },
  { id: 'smudge', command: 'smudgeTool', icon: 'matGesture' },
  { id: 'clone', command: 'cloneTool', icon: 'matContentCopy' },
  { id: 'projection', command: 'projectionTool', icon: 'matWallpaper' },
  { id: 'fill', command: 'fillTool', icon: 'matFormatColorFill' },
  { id: 'picker', command: 'pickerTool', icon: 'matColorize' },
  { id: 'line', command: 'lineTool', icon: 'matHorizontalRule' },
  { id: 'path', command: 'pathTool', icon: 'matPolyline' }
];

/** The icons of {@link PaintTools}, for a component's `provideIcons`. */
export const PaintToolIcons = {
  matAutoFixOff,
  matBrush,
  matColorize,
  matContentCopy,
  matFormatColorFill,
  matGesture,
  matHorizontalRule,
  matPolyline,
  matWallpaper
};

/** What a click of the fill tool can take, in the Server's words until its vocabulary gives the labels. */
export const FillModes: readonly FillMode[] = ['triangle', 'polygon', 'part', 'shell'];
