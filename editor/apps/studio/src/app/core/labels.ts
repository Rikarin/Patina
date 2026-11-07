/** What a person calls the words the engine uses. */
const Channels: Readonly<Record<string, string>> = {
  baseColor: 'Base Color',
  roughness: 'Roughness',
  metalness: 'Metallic',
  normal: 'Normal',
  height: 'Height',
  emissive: 'Emissive',
  opacity: 'Opacity',
  ambientOcclusion: 'Ambient Occlusion'
};

const Blends: Readonly<Record<string, string>> = {
  Copy: 'Normal',
  LinearDodge: 'Add',
  HardLight: 'Hard Light',
  SoftLight: 'Soft Light',
  ColorDodge: 'Color Dodge',
  ColorBurn: 'Color Burn',
  LinearBurn: 'Linear Burn'
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
