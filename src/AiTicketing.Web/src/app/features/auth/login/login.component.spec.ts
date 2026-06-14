import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import { CurrentUser } from '../../../core/auth/auth.models';
import { LoginComponent } from './login.component';

class AuthServiceStub {
  currentRole = jasmine.createSpy('currentRole').and.returnValue('Admin');
  login = jasmine.createSpy('login').and.returnValue(of({
    id: 'admin-id',
    email: 'admin@example.com',
    displayName: 'Admin User',
    roles: ['Admin']
  } satisfies CurrentUser));
}

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let auth: AuthServiceStub;

  beforeEach(async () => {
    auth = new AuthServiceStub();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders accessible login fields', () => {
    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelector('h1')?.textContent).toContain('Sign in');
    expect(element.querySelector('label[for="email"]')?.textContent).toContain('Email');
    expect(element.querySelector('label[for="password"]')?.textContent).toContain('Password');
  });

  it('required fields validate before submit', () => {
    component.submit();
    fixture.detectChanges();

    const element: HTMLElement = fixture.nativeElement;
    expect(auth.login).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Email is required.');
    expect(element.textContent).toContain('Password is required.');
  });

  it('submits valid credentials and routes to the role home', () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    component.form.setValue({ email: 'admin@example.com', password: 'password' });
    component.submit();

    expect(auth.login).toHaveBeenCalledWith({ email: 'admin@example.com', password: 'password' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin');
  });

  it('displays a safe error for invalid login', () => {
    auth.login.and.returnValue(throwError(() => new ApiError(400, 'validation', 'Backend detail')));
    component.form.setValue({ email: 'bad@example.com', password: 'wrong' });

    component.submit();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('The email or password is incorrect.');
    expect(fixture.nativeElement.textContent).not.toContain('Backend detail');
  });
});
