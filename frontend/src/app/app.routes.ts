import { Routes } from '@angular/router';
import { AdminPage } from './features/admin/admin.page';
import { RulesPage } from './features/rules/rules.page';

export const routes: Routes = [
	{ path: '', pathMatch: 'full', redirectTo: 'rules' },
	{ path: 'rules', component: RulesPage, title: 'My alert rules | Sonrisa' },
	{ path: 'admin', component: AdminPage, title: 'Administration | Sonrisa' },
	{ path: '**', redirectTo: 'rules' },
];
