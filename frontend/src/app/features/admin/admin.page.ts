import { CommonModule } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Delivery, EventDetail, EventItem } from '../../core/api.models';
import { ApiService, describeApiError } from '../../core/api.service';
import { IdentityService } from '../../core/identity.service';

@Component({ selector: 'app-admin-page', imports: [CommonModule, ReactiveFormsModule], templateUrl: './admin.page.html', styleUrl: './admin.page.scss' })
export class AdminPage {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  protected readonly identity = inject(IdentityService);
  protected readonly events = signal<EventItem[]>([]);
  protected readonly selectedEvent = signal<EventDetail | null>(null);
  protected readonly deliveries = signal<Delivery[]>([]);
  protected readonly selectedStatus = signal('');
  protected readonly eventsLoading = signal(false);
  protected readonly deliveriesLoading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal('');
  protected readonly eventsError = signal('');
  protected readonly deliveriesError = signal('');
  protected readonly notice = signal('');
  protected readonly eventPage = signal(1);
  protected readonly eventTotal = signal(0);
  protected readonly deliveryPage = signal(1);
  protected readonly deliveryTotal = signal(0);
  protected readonly form = this.fb.nonNullable.group({
    category: ['', [Validators.required, Validators.maxLength(100), (control: { value: string }) => control.value.trim() ? null : { required: true }]],
    title: ['', [Validators.required, Validators.maxLength(300), (control: { value: string }) => control.value.trim() ? null : { required: true }]],
    description: ['', Validators.maxLength(4000)],
    occurredAt: [this.localDateTime(), Validators.required],
  });

  constructor() {
    effect(() => {
      if (this.identity.current()?.role === 'Admin') { untracked(() => { this.loadEvents(); this.loadDeliveries(); }); }
      else { this.events.set([]); this.deliveries.set([]); this.selectedEvent.set(null); }
    });
  }

  protected submitEvent(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.identity.current()?.role !== 'Admin') return;
    const value = this.form.getRawValue();
    const occurredAt = new Date(value.occurredAt);
    if (Number.isNaN(occurredAt.getTime())) { this.error.set('Enter a valid event date and time.'); return; }
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.submitEvent({ ...value, occurredAt: occurredAt.toISOString() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.notice.set(result.isDuplicate ? `This event already exists as event #${result.id}.` : `Event #${result.id} was submitted.`);
        this.selectedStatus.set(''); this.deliveryPage.set(1);
        this.loadEvents(); this.loadDeliveries(); this.selectEventById(result.id);
        this.form.reset({ category: '', title: '', description: '', occurredAt: this.localDateTime() });
      },
      error: (error: unknown) => { this.error.set(describeApiError(error)); this.submitting.set(false); },
    });
  }
  protected loadEvents(): void {
    if (this.identity.current()?.role !== 'Admin') return;
    this.eventsLoading.set(true);
    this.eventsError.set('');
    this.api.getEvents(this.eventPage()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => { this.events.set(result.items); this.eventTotal.set(result.totalCount); this.eventsLoading.set(false); },
      error: (error: unknown) => { this.eventsError.set(describeApiError(error)); this.eventsLoading.set(false); },
    });
  }
  protected selectEvent(event: EventItem): void { this.selectEventById(event.id); }
  protected selectEventById(id: number): void {
    this.error.set('');
    this.api.getEvent(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (detail) => this.selectedEvent.set(detail), error: (error: unknown) => this.error.set(describeApiError(error)),
    });
  }
  protected loadDeliveries(): void {
    if (this.identity.current()?.role !== 'Admin') return;
    this.deliveriesLoading.set(true);
    this.deliveriesError.set('');
    this.api.getDeliveries(this.selectedStatus(), this.deliveryPage()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => { this.deliveries.set(result.items); this.deliveryTotal.set(result.totalCount); this.deliveriesLoading.set(false); },
      error: (error: unknown) => { this.deliveriesError.set(describeApiError(error)); this.deliveriesLoading.set(false); },
    });
  }
  protected changeStatus(event: Event): void { this.selectedStatus.set((event.target as HTMLSelectElement).value); this.deliveryPage.set(1); this.loadDeliveries(); }
  protected refresh(): void { this.loadEvents(); this.loadDeliveries(); const selected = this.selectedEvent(); if (selected) this.selectEventById(selected.id); }
  protected changeEventPage(page: number): void { if (page >= 1 && page <= this.eventPageCount) { this.eventPage.set(page); this.loadEvents(); } }
  protected changeDeliveryPage(page: number): void { if (page >= 1 && page <= this.deliveryPageCount) { this.deliveryPage.set(page); this.loadDeliveries(); } }
  protected get eventPageCount(): number { return Math.max(1, Math.ceil(this.eventTotal() / 10)); }
  protected get deliveryPageCount(): number { return Math.max(1, Math.ceil(this.deliveryTotal() / 10)); }
  protected statusClass(status: string): string { return `delivery-${status.toLowerCase()}`; }
  protected occurredAtError(): boolean { const control = this.form.controls.occurredAt; return control.touched && control.invalid; }
  private localDateTime(): string { const now = new Date(); return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 16); }
}