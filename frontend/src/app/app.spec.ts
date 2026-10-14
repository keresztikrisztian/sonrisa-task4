import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiService } from './core/api.service';
import { demoIdentityInterceptor } from './core/demo-identity.interceptor';
import { IdentityService } from './core/identity.service';

describe('Demo identity behavior', () => {
  let httpTesting: HttpTestingController;
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([demoIdentityInterceptor])), provideHttpClientTesting()],
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpTesting.verify());
  it('restores the saved identity and leaves the public identity lookup unmodified', () => {
    localStorage.setItem('sonrisa-demo-identity', 'demo-admin');
    const identity = TestBed.inject(IdentityService);
    identity.loadIdentities();
    const request = httpTesting.expectOne('/api/demo-identities');
    expect(request.request.headers.has('X-Demo-Identity')).toBe(false);
    request.flush([{ id: 'demo-user', displayName: 'Demo User', role: 'User' }, { id: 'demo-admin', displayName: 'Demo Administrator', role: 'Admin' }]);
    expect(identity.current()?.id).toBe('demo-admin');
    expect(localStorage.getItem('sonrisa-demo-identity')).toBe('demo-admin');
  });
  it('adds the selected identity to API requests', () => {
    const identity = TestBed.inject(IdentityService);
    identity.identities.set([{ id: 'demo-user', displayName: 'Demo User', role: 'User' }]);
    identity.select('demo-user');
    TestBed.inject(ApiService).getRules(1).subscribe();
    const request = httpTesting.expectOne('/api/alert-rules?page=1&pageSize=10');
    expect(request.request.headers.get('X-Demo-Identity')).toBe('demo-user');
    request.flush({ items: [], page: 1, pageSize: 10, totalCount: 0 });
  });
});
