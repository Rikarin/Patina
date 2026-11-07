import { NgIcon } from '@ng-icons/core';
import { XuiIconImports } from '@xui/icon';

/** What a template needs for `<ng-icon xui size="sm" name="..." />`. A component provides the icons it names. */
export const IconImports = [NgIcon, XuiIconImports] as const;
