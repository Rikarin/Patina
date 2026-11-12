/** Where Patina's own pages are, for Help and About. Each opens in the browser, never in the app's window. */
const Repository = 'https://github.com/Rikarin/Patina';

export const Links = {
  /** A guide to the app, from a library to an export, with the keys that matter. */
  documentation: `${Repository}/blob/master/docs/03-guide.md`,
  /** A new issue, for a problem or an idea. */
  issues: `${Repository}/issues/new`,
  licence: `${Repository}/blob/master/LICENSE`,
  notices: `${Repository}/blob/master/THIRD-PARTY-NOTICES.md`
} as const;
