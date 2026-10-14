import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AlertRule, AlertRuleInput, Delivery, EventDetail, EventInput, EventItem, EventSubmission, PageResult } from './api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  getRules(page: number): Observable<PageResult<AlertRule>> { return this.http.get<PageResult<AlertRule>>('/api/alert-rules', { params: { page, pageSize: 10 } }); }
  createRule(input: AlertRuleInput): Observable<AlertRule> { return this.http.post<AlertRule>('/api/alert-rules', input); }
  updateRule(id: number, input: AlertRuleInput): Observable<AlertRule> { return this.http.put<AlertRule>(`/api/alert-rules/${id}`, input); }
  deleteRule(id: number): Observable<void> { return this.http.delete<void>(`/api/alert-rules/${id}`); }
  getEvents(page: number): Observable<PageResult<EventItem>> { return this.http.get<PageResult<EventItem>>('/api/events', { params: { page, pageSize: 10 } }); }
  getEvent(id: number): Observable<EventDetail> { return this.http.get<EventDetail>(`/api/events/${id}`); }
  submitEvent(input: EventInput): Observable<EventSubmission> { return this.http.post<EventSubmission>('/api/events', input); }
  getDeliveries(status: string, page: number): Observable<PageResult<Delivery>> {
    let params = new HttpParams().set('page', page).set('pageSize', 10);
    if (status) params = params.set('status', status);
    return this.http.get<PageResult<Delivery>>('/api/notification-deliveries', { params });
  }
}

export function describeApiError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const payload = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
    const validationMessages = Object.values(payload?.errors ?? {}).flat();
    if (validationMessages.length) return validationMessages.join(' ');
    if (payload?.detail) return payload.detail;
    if (payload?.title) return payload.title;
    if (error.status === 0) return 'The API could not be reached. Check that the backend is running and try again.';
    return `The request failed (${error.status}). Please try again.`;
  }
  return 'Something went wrong. Please try again.';
}