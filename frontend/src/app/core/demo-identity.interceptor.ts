import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { IdentityService } from './identity.service';

export const demoIdentityInterceptor: HttpInterceptorFn = (request, next) => {
  const identity = inject(IdentityService).current();
  if (!identity || !request.url.startsWith('/api/')) return next(request);
  return next(request.clone({ setHeaders: { 'X-Demo-Identity': identity.id } }));
};