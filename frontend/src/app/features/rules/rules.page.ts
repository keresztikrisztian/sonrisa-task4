import { CommonModule } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AlertRule, AlertRuleInput } from '../../core/api.models';
import { ApiService, describeApiError } from '../../core/api.service';
import { IdentityService } from '../../core/identity.service';

@Component({ selector: 'app-rules-page', imports: [CommonModule, ReactiveFormsModule], templateUrl: './rules.page.html', styleUrl: './rules.page.scss' })
export class RulesPage {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  protected readonly identity = inject(IdentityService);
  protected readonly rules = signal<AlertRule[]>([]);
  protected readonly page = signal(1);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  protected readonly editorOpen = signal(false);
  protected readonly editing = signal<AlertRule | null>(null);
  protected readonly busyRuleId = signal<number | null>(null);
  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120), (control: { value: string }) => control.value.trim() ? null : { required: true }]],
    category: ['', Validators.maxLength(100)],
    keywords: ['', (control: { value: string }) => this.validateKeywords(control.value)],
    email: [true], slack: [false], isEnabled: [true],
  });

  constructor() {
    effect(() => {
      if (this.identity.current()?.role === 'User') { this.page.set(1); untracked(() => this.loadRules()); }
      else { this.rules.set([]); this.totalCount.set(0); }
    });
  }

  protected loadRules(): void {
    if (this.identity.current()?.role !== 'User') return;
    this.loading.set(true); this.error.set('');
    this.api.getRules(this.page()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => { this.rules.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: (error: unknown) => { this.error.set(describeApiError(error)); this.loading.set(false); },
    });
  }
  protected openCreate(): void { this.editing.set(null); this.form.reset({ name: '', category: '', keywords: '', email: true, slack: false, isEnabled: true }); this.error.set(''); this.editorOpen.set(true); }
  protected openEdit(rule: AlertRule): void {
    this.editing.set(rule);
    this.form.reset({ name: rule.name, category: rule.category ?? '', keywords: rule.keywords.join('\n'), email: rule.channels.includes('Email'), slack: rule.channels.includes('Slack'), isEnabled: rule.isEnabled });
    this.error.set(''); this.editorOpen.set(true);
  }
  protected saveRule(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const input: AlertRuleInput = { name: value.name.trim(), category: value.category.trim() || null, keywords: value.keywords.split('\n').map((item) => item.trim()).filter(Boolean), channels: [value.email ? 'Email' : '', value.slack ? 'Slack' : ''].filter(Boolean), isEnabled: value.isEnabled };
    this.saving.set(true); this.error.set('');
    const editing = this.editing();
    const request = editing ? this.api.updateRule(editing.id, input) : this.api.createRule(input);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.saving.set(false); this.editorOpen.set(false); this.loadRules(); },
      error: (error: unknown) => { this.error.set(describeApiError(error)); this.saving.set(false); },
    });
  }
  protected toggleRule(rule: AlertRule): void {
    this.busyRuleId.set(rule.id); this.error.set('');
    this.api.updateRule(rule.id, { name: rule.name, category: rule.category, keywords: rule.keywords, channels: rule.channels, isEnabled: !rule.isEnabled }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busyRuleId.set(null); this.loadRules(); },
      error: (error: unknown) => { this.error.set(describeApiError(error)); this.busyRuleId.set(null); },
    });
  }
  protected deleteRule(rule: AlertRule): void {
    if (!confirm(`Delete the alert rule "${rule.name}"?`)) return;
    this.busyRuleId.set(rule.id); this.error.set('');
    this.api.deleteRule(rule.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busyRuleId.set(null); this.loadRules(); },
      error: (error: unknown) => { this.error.set(describeApiError(error)); this.busyRuleId.set(null); },
    });
  }
  protected changePage(nextPage: number): void { if (nextPage >= 1 && nextPage <= this.pageCount) { this.page.set(nextPage); this.loadRules(); } }
  protected get pageCount(): number { return Math.max(1, Math.ceil(this.totalCount() / 10)); }
  protected hasError(controlName: string, errorName: string): boolean { const control = this.form.get(controlName); return !!control?.touched && control.hasError(errorName); }
  private validateKeywords(value: string): { invalidKeywords: boolean } | null {
    const keywords = value.split('\n').map((item) => item.trim()).filter(Boolean);
    if (keywords.some((item) => item.length > 100)) return { invalidKeywords: true };
    const normalized = keywords.map((item) => item.toLocaleLowerCase());
    return new Set(normalized).size === normalized.length ? null : { invalidKeywords: true };
  }
}