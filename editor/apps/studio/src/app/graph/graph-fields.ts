import type { GraphNodeType, GraphPortDefinition, GraphSettingDefinition } from '@patina/api-client';

/** A row of the Inspector: one thing a node lets an author set. */
export type GraphField =
  | {
      control: 'number';
      source: 'value';
      name: string;
      summary: string;
      fallback: readonly number[];
      whole: boolean;
      minimum: number | null;
      maximum: number | null;
    }
  | { control: 'switch'; source: 'value'; name: string; summary: string; fallback: readonly number[] }
  | { control: 'colour'; source: 'value'; name: string; summary: string; fallback: readonly number[] }
  | {
      control: 'number';
      source: 'text';
      name: string;
      summary: string;
      fallback: string;
      whole: boolean;
      minimum: number | null;
      maximum: number | null;
    }
  | { control: 'switch'; source: 'text'; name: string; summary: string; fallback: string }
  | { control: 'choice'; source: 'text'; name: string; summary: string; fallback: string; choices: readonly string[] }
  | { control: 'text'; source: 'text'; name: string; summary: string; fallback: string };

export interface GraphFieldGroup {
  name: string;
  fields: readonly GraphField[];
}

/** The inputs a wire can reach, which every output in the catalogue is: an Image. Other inputs take numbers. */
export function isWireable(port: GraphPortDefinition): boolean {
  return port.direction === 'Output' || port.kind === 'Image';
}

function fieldOfPort(port: GraphPortDefinition): GraphField | null {
  const base = { source: 'value', name: port.name, summary: port.summary, fallback: port.default } as const;
  const range = {
    minimum: port.softMinimum ?? port.minimum ?? null,
    maximum: port.softMaximum ?? port.maximum ?? null
  };
  switch (port.kind) {
    case 'Float':
      return { ...base, ...range, control: 'number', whole: false };
    case 'Int':
      return { ...base, ...range, control: 'number', whole: true };
    case 'Bool':
      return { ...base, control: 'switch' };
    case 'Float4':
      return { ...base, control: 'colour' };
    default:
      return null;
  }
}

function fieldOfSetting(setting: GraphSettingDefinition): GraphField {
  const base = { source: 'text', name: setting.name, summary: setting.summary, fallback: setting.default } as const;
  if (setting.accepted.length > 0) {
    return { ...base, control: 'choice', choices: setting.accepted };
  }

  switch (setting.kind) {
    case 'Bool':
      return { ...base, control: 'switch' };
    case 'Int':
    case 'Float':
      return {
        ...base,
        control: 'number',
        whole: setting.kind === 'Int',
        minimum: setting.minimum ?? null,
        maximum: setting.maximum ?? null
      };
    default:
      return { ...base, control: 'text' };
  }
}

/** What a node type shows in the Inspector, under its headings. Numbers come first, then settings. */
export function fieldsOf(type: GraphNodeType): readonly GraphFieldGroup[] {
  const ungrouped: GraphField[] = [];
  for (const port of type.ports) {
    const field = port.direction === 'Input' ? fieldOfPort(port) : null;
    if (field) {
      ungrouped.push(field);
    }
  }

  const groups = new Map<string, GraphField[]>([['', ungrouped]]);
  for (const setting of type.settings) {
    const fields = groups.get(setting.group) ?? [];
    fields.push(fieldOfSetting(setting));
    groups.set(setting.group, fields);
  }

  return [...groups].filter(([, fields]) => fields.length > 0).map(([name, fields]) => ({ name, fields }));
}

/**
 * The range a slider covers. A setting or a port declares what it can, and an open end is guessed from the default.
 * Either way the range widens to reach whatever is set, since the number box beside it takes anything.
 */
export function sliderRange(
  value: number,
  fallback: number,
  declared?: { minimum: number | null; maximum: number | null }
): { min: number; max: number } {
  const span = Math.max(1, Math.abs(fallback) * 4);
  const min = Math.min(declared?.minimum ?? Math.min(0, fallback < 0 ? -span : 0), value);
  const max = Math.max(declared?.maximum ?? span, value);
  return { min, max: max > min ? max : min + 1 };
}

function hexOfLane(lane: number | undefined): string {
  const scaled = Math.round(Math.min(1, Math.max(0, lane ?? 0)) * 255);
  return scaled.toString(16).padStart(2, '0');
}

/** RGBA lanes as the colour picker's `#RRGGBBAA`. The lanes are shown as stored; no transfer function is applied. */
export function hexOfLanes(lanes: readonly number[]): string {
  return `#${hexOfLane(lanes[0])}${hexOfLane(lanes[1])}${hexOfLane(lanes[2])}${hexOfLane(lanes[3] ?? 1)}`.toUpperCase();
}

/** The lanes of `#RRGGBB` or `#RRGGBBAA`, or null for anything else. */
export function lanesOfHex(hex: string): readonly number[] | null {
  const match = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})?$/i.exec(hex);
  if (!match) {
    return null;
  }

  return [match[1], match[2], match[3], match[4] ?? 'ff'].map(pair => parseInt(pair ?? '0', 16) / 255);
}
