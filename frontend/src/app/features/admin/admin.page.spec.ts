import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { demoIdentityInterceptor } from '../../core/demo-identity.interceptor';
import { IdentityService } from '../../core/identity.service';
import { AdminPage } from './admin.page';

describe('AdminPage', () => {
  let fixture: ComponentFixture<AdminPage>;
  let httpTesting: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [AdminPage], providers: [provideHttpClient(withInterceptors([demoIdentityInterceptor])), provideHttpClientTesting()] }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
    const identity = TestBed.inject(IdentityService);
    identity.identities.set([{ id: 'demo-admin', displayName: 'Demo Administrator', role: 'Admin' }]);
    identity.select('demo-admin');
    fixture = TestBed.createComponent(AdminPage);
    fixture.detectChanges();
    httpTesting.expectOne('/api/events?page=1&pageSize=10').flush({ items: [], page: 1, pageSize: 10, totalCount: 0 });
    httpTesting.expectOne('/api/notification-deliveries?page=1&pageSize=10').flush({ items: [
      { id: 1, eventId: 4, alertRuleId: 2, channel: 'Email', status: 'Recorded', attemptCount: 1 },
      { id: 2, eventId: 4, alertRuleId: 3, channel: 'Slack', status: 'Sent', attemptCount: 1 },
      { id: 3, eventId: 5, alertRuleId: 3, channel: 'Email', status: 'Failed', attemptCount: 3 },
    ], page: 1, pageSize: 10, totalCount: 3 });
    fixture.detectChanges();
  });
  afterEach(() => httpTesting.verify());
  it('distinguishes recorded, live sent, and failed delivery outcomes', () => {
    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('Recorded');
    expect(page.textContent).toContain('Sent');
    expect(page.textContent).toContain('Failed');
    expect(page.querySelector('.delivery-recorded')).not.toBeNull();
    expect(page.querySelector('.delivery-sent')).not.toBeNull();
    expect(page.querySelector('.delivery-failed')).not.toBeNull();
  });
  it('allows an empty event description as the API does', () => {
    const page = fixture.componentInstance as unknown as { form: { controls: { description: { valid: boolean } } } };
    expect(page.form.controls.description.valid).toBe(true);
  });
});