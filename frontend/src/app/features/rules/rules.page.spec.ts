import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IdentityService } from '../../core/identity.service';
import { demoIdentityInterceptor } from '../../core/demo-identity.interceptor';
import { RulesPage } from './rules.page';

describe('RulesPage', () => {
  let fixture: ComponentFixture<RulesPage>;
  let httpTesting: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [RulesPage], providers: [provideHttpClient(withInterceptors([demoIdentityInterceptor])), provideHttpClientTesting()] }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
    const identity = TestBed.inject(IdentityService);
    identity.identities.set([{ id: 'demo-user', displayName: 'Demo User', role: 'User' }]);
    identity.select('demo-user');
    fixture = TestBed.createComponent(RulesPage);
    fixture.detectChanges();
    httpTesting.expectOne('/api/alert-rules?page=1&pageSize=10').flush({ items: [], page: 1, pageSize: 10, totalCount: 0 });
    fixture.detectChanges();
  });
  afterEach(() => httpTesting.verify());
  it('renders the empty rules state', () => { expect((fixture.nativeElement as HTMLElement).textContent).toContain('No alert rules yet'); });
  it('rejects a missing rule name and duplicate keywords', () => {
    const page = fixture.componentInstance as unknown as { openCreate: () => void; saveRule: () => void; form: { controls: { name: { setValue: (value: string) => void; invalid: boolean }; keywords: { setValue: (value: string) => void; invalid: boolean } } } };
    page.openCreate(); page.form.controls.name.setValue(''); page.form.controls.keywords.setValue('Flood\nflood'); page.saveRule();
    fixture.detectChanges();
    expect(page.form.controls.name.invalid).toBe(true);
    expect(page.form.controls.keywords.invalid).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Enter a name.');
  });
});