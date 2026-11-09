import type { AssetSummary, ProjectSummary } from '@patina/api-client';
import { assetTarget, projectTarget, thumbnailSource } from './library-actions';

const asset: AssetSummary = {
  id: '0190a000-0000-7000-8000-000000000001',
  kind: 'material',
  name: 'Oak',
  path: 'projects/Props/crate/oak.vxtexgraph',
  tags: ['wood'],
  revision: null,
  usageCount: 2
};

const project: ProjectSummary = {
  id: '0190a000-0000-7000-8000-000000000002',
  name: 'Crate',
  path: 'Props/crate',
  tags: [],
  referenceCount: 0
};

describe('LibraryTarget', () => {
  it('finds a material’s project in the folder its file is in, however deep', () => {
    expect(assetTarget(asset)).toEqual({
      kind: 'document',
      project: 'Props/crate',
      id: asset.id,
      name: 'Oak',
      path: asset.path,
      tags: ['wood'],
      documentKind: 'material'
    });
  });

  it('asks for a project’s picture by its folder below the library, and a document’s by its file', () => {
    expect(thumbnailSource(projectTarget(project))).toBe('projects/Props/crate');
    expect(thumbnailSource(assetTarget(asset))).toBe('projects/Props/crate/oak.vxtexgraph');
  });
});
