import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { App } from './app/app';
import { routes } from './app/app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { sessionInterceptor } from './app/auth';

bootstrapApplication(App, {
  providers: [provideRouter(routes), provideHttpClient(withInterceptors([sessionInterceptor]))],
}).catch((error: unknown) => console.error(error));
