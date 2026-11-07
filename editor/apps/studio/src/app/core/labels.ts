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

export function channelName(usage: string): string {
  return Channels[usage] ?? usage;
}

export function blendName(blend: string): string {
  return Blends[blend] ?? blend;
}
