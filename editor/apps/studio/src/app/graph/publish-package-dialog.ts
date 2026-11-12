import { ChangeDetectionStrategy, Component, computed, effect, inject, model, signal, untracked } from '@angular/core';
import type { PackageDependency, PackagePlan } from '@patina/api-client';
import { XuiButtonImports } from '@xui/button';
import { XuiCalloutImports } from '@xui/callout';
import { XuiDialogImports } from '@xui/dialog';
import { XuiSelectImports } from '@xui/select';
import { Activity } from '../core/activity';
import { messageOf } from '../core/messages';
import { GraphStore } from './graph-store';

/** What a package can be used as, which decides where the shelf puts it. */
const Roles = [
  { value: 'material', label: 'Material' },
  { value: 'mask', label: 'Mask' },
  { value: 'brush', label: 'Brush' },
  { value: 'filter', label: 'Filter' }
];

/** What each kind of thing a graph reads is called in the list. */
const KindNames: Readonly<Record<string, string>> = {
  compound: 'Compound',
  shipped: 'Starter compound',
  picture: 'Picture',
  drawing: 'Font or SVG',
  unknown: 'Node type'
};

/** What the dialog says of each kind, so that what comes along and what does not is plain. */
const KindHints: Readonly<Record<string, string>> = {
  compound: 'Carried in the package',
  shipped: 'Ships with Patina, so not carried',
  picture: 'Carried in the package'
};

/**
 * Publishes the open graph as a package (ADR 0027): first what it would bring, then the package in the library's
 * packages folder, where every project's shelf offers it. The Server gathers and refuses; this shows what it said.
 */
@Component({
  selector: 'ptn-publish-package',
  imports: [XuiButtonImports, XuiCalloutImports, XuiDialogImports, XuiSelectImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <xui-dialog title="Publish as Package" size="md" [(open)]="open">
      <xui-dialog-body class="flex flex-col gap-3" data-testid="publish-package">
        @if (plan(); as plan) {
          <p class="text-sm">
            <span class="font-semibold">{{ plan.name }}</span> goes to
            <span class="readout" data-testid="publish-package-path">{{ plan.path }}</span
            >{{ plan.replaces ? ', replacing the package of that name.' : '.' }}
          </p>
          <div class="flex flex-col gap-1">
            <span class="text-foreground-muted text-xs">Use as</span>
            <xui-select
              aria-label="Use as"
              data-testid="publish-package-role"
              [filterable]="false"
              [itemText]="roleName"
              [items]="roles"
              [value]="role()"
              (valueChange)="role.set($event ?? plan.role)"
            />
          </div>
          <p class="section-label">What it reads</p>
          <ul class="well flex max-h-56 flex-col overflow-auto py-1 text-xs" data-testid="publish-package-dependencies">
            @for (dependency of plan.dependencies; track dependency.kind + dependency.name) {
              <li class="flex items-baseline gap-2 px-2 py-0.5" [attr.data-kind]="dependency.kind">
                <span class="text-foreground-muted w-28 shrink-0">{{ kindName(dependency.kind) }}</span>
                <span class="min-w-0 flex-1 truncate" [title]="dependency.file ?? dependency.name">{{
                  dependency.name
                }}</span>
                @if (dependency.problem) {
                  <span class="text-error shrink-0" data-testid="publish-package-problem">{{
                    dependency.problem
                  }}</span>
                } @else {
                  <span class="text-foreground-muted shrink-0">{{ hintOf(dependency) }}</span>
                }
              </li>
            } @empty {
              <li class="text-foreground-muted px-2 py-0.5">Nothing but Patina’s own nodes.</li>
            }
          </ul>
          @for (diagnostic of plan.diagnostics; track $index) {
            <xui-callout color="error" compact>{{ diagnostic.message }}</xui-callout>
          }
          @if (store.document()?.history?.dirty) {
            <p class="text-foreground-muted text-xs">The graph is saved first, and published as saved.</p>
          }
        } @else if (!refused()) {
          <p class="text-foreground-muted text-sm">Looking at what the graph reads…</p>
        }
        @if (refused(); as reason) {
          <xui-callout color="error" compact data-testid="publish-package-refused">{{ reason }}</xui-callout>
        }
      </xui-dialog-body>
      <xui-dialog-footer>
        <button type="button" xuiButton variant="ghost" (click)="open.set(false)">Cancel</button>
        <button
          type="button"
          xuiButton
          color="primary"
          data-testid="publish-package-publish"
          [disabled]="!canPublish()"
          (click)="publish()"
        >
          Publish
        </button>
      </xui-dialog-footer>
    </xui-dialog>
  `
})
export class PublishPackageDialog {
  protected readonly store = inject(GraphStore);
  private readonly activity = inject(Activity);

  readonly open = model(false);

  protected readonly plan = signal<PackagePlan | null>(null);
  protected readonly refused = signal<string | null>(null);
  protected readonly role = signal('material');
  private readonly publishing = signal(false);
  protected readonly roles = Roles.map(role => role.value);
  protected readonly roleName = (role: string): string => Roles.find(held => held.value === role)?.label ?? role;
  protected readonly canPublish = computed(() => {
    const plan = this.plan();
    return (
      plan !== null &&
      !this.publishing() &&
      plan.diagnostics.length === 0 &&
      plan.dependencies.every(dependency => !dependency.problem)
    );
  });

  constructor() {
    effect(() => {
      if (this.open()) {
        untracked(() => void this.look());
      }
    });
  }

  protected kindName(kind: string): string {
    return KindNames[kind] ?? kind;
  }

  protected hintOf(dependency: PackageDependency): string {
    return KindHints[dependency.kind] ?? '';
  }

  private async look(): Promise<void> {
    this.plan.set(null);
    this.refused.set(null);
    try {
      // What is saved is what is published, so a plan of unsaved edits would show what they are not.
      if (this.store.document()?.history.dirty && !(await this.store.save())) {
        this.refused.set('The graph could not be saved, so it cannot be published yet.');
        return;
      }

      const plan = await this.store.packagePlan();
      this.plan.set(plan);
      this.role.set(plan.role);
    } catch (failure) {
      this.refused.set(messageOf(failure));
    }
  }

  protected async publish(): Promise<void> {
    this.publishing.set(true);
    try {
      const published = await this.store.publishPackage(this.role());
      this.activity.report({
        tone: 'done',
        source: 'Packages',
        text: `${published.manifest.name} is published to ${published.path}, and every project’s shelf offers it.`
      });
      this.open.set(false);
    } catch (failure) {
      this.refused.set(messageOf(failure));
    } finally {
      this.publishing.set(false);
    }
  }
}
