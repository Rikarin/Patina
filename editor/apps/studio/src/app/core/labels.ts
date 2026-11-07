/** What a person calls the words the engine uses. */
const Channels: Readonly<Record<string, string>> = {
  baseColor: 'Base Color',
  roughness: 'Roughness',
  metalness: 'Metallic',
  normal: 'Normal',
  height: 'Height',
  emissive: 'Emissive',
  opacity: 'Opacity',
  occlusion: 'Ambient Occlusion',
  mask: 'Mask'
};

const Blends: Readonly<Record<string, string>> = {
  Copy: 'Normal',
  Multiply: 'Multiply',
  Screen: 'Screen',
  Overlay: 'Overlay',
  Add: 'Add',
  Subtract: 'Subtract',
  Darken: 'Darken',
  Lighten: 'Lighten',
  Divide: 'Divide',
  HardLight: 'Hard Light',
  SoftLight: 'Soft Light',
  Difference: 'Difference',
  Exclusion: 'Exclusion',
  ColourDodge: 'Color Dodge',
  ColourBurn: 'Color Burn',
  SignedAdd: 'Signed Add'
};

const JobStates: Readonly<Record<string, string>> = {
  queued: 'Waiting',
  running: 'Working',
  done: 'Done',
  failed: 'Failed',
  cancelled: 'Cancelled'
};

const ImageFormats: Readonly<Record<string, string>> = {
  png8: 'PNG 8-bit',
  png16: 'PNG 16-bit',
  exr16: 'EXR 16-bit float',
  exr32: 'EXR 32-bit float'
};

export function channelName(usage: string): string {
  return Channels[usage] ?? usage;
}

export function blendName(blend: string): string {
  return Blends[blend] ?? blend;
}

export function jobStateName(state: string): string {
  return JobStates[state] ?? state;
}

export function imageFormatName(format: string): string {
  return ImageFormats[format] ?? format;
}
