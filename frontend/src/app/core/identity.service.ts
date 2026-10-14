import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { DemoIdentity } from './api.models';
import { describeApiError } from './api.service';

const identityStorageKey = 'sonrisa-demo-identity';

@Injectable({ providedIn: 'root' })
export class IdentityService {
  private readonly http = inject(HttpClient);
  readonly identities = signal<DemoIdentity[]>([]);
  readonly current = signal<DemoIdentity | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');

  loadIdentities(): void {
    this.loading.set(true); this.error.set('');
    this.http.get<DemoIdentity[]>('/api/demo-identities').pipe(
      catchError((error: unknown) => { this.error.set(describeApiError(error)); this.loading.set(false); return of(null); }),
    ).subscribe((identities) => {
      if (!identities) return;
      this.identities.set(identities);
      const storedId = localStorage.getItem(identityStorageKey);
      this.setCurrent(identities.find((item) => item.id === storedId) ?? identities[0] ?? null);
      this.loading.set(false);
    });
  }

  select(id: string): void {
    const selected = this.identities().find((item) => item.id === id) ?? null;
    if (selected) this.setCurrent(selected);
  }

  private setCurrent(identity: DemoIdentity | null): void {
    this.current.set(identity);
    if (identity) localStorage.setItem(identityStorageKey, identity.id);
    else localStorage.removeItem(identityStorageKey);
  }
}