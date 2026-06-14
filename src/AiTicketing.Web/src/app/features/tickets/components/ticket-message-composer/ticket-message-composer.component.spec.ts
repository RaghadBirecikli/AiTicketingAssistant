import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TicketMessageComposerComponent, messageBodyMaxLength } from './ticket-message-composer.component';

describe('TicketMessageComposerComponent', () => {
  let fixture: ComponentFixture<TicketMessageComposerComponent>;
  let component: TicketMessageComposerComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketMessageComposerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketMessageComposerComponent);
    component = fixture.componentInstance;
    component.mode = 'public';
    fixture.detectChanges();
  });

  it('renders public message mode', () => {
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Public reply');
  });

  it('renders internal note mode with textual warning', () => {
    fixture.componentRef.setInput('mode', 'internal');
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Internal note');
    expect(text).toContain('Customers cannot see internal notes');
  });

  it('rejects empty and whitespace-only messages', () => {
    component.submit();
    expect(component.form.invalid).toBeTrue();

    component.bodyControl.setValue('   ');
    component.submit();
    expect(component.bodyControl.hasError('whitespace')).toBeTrue();
  });

  it('rejects overlong messages client-side', () => {
    component.bodyControl.setValue('x'.repeat(messageBodyMaxLength + 1));

    expect(component.bodyControl.hasError('maxlength')).toBeTrue();
  });

  it('trims body before emitting submit', () => {
    spyOn(component.sendMessage, 'emit');
    component.bodyControl.setValue('  Hello world  ');

    component.submit();

    expect(component.sendMessage.emit).toHaveBeenCalledWith({ body: 'Hello world', mode: 'public' });
  });

  it('reports and sets draft text for explicit AI insertion without submitting', () => {
    spyOn(component.sendMessage, 'emit');

    expect(component.hasDraft()).toBeFalse();

    component.setDraft('Suggested reply');

    expect(component.hasDraft()).toBeTrue();
    expect(component.bodyControl.value).toBe('Suggested reply');
    expect(component.bodyControl.dirty).toBeTrue();
    expect(component.bodyControl.touched).toBeTrue();
    expect(component.sendMessage.emit).not.toHaveBeenCalled();
  });

  it('disables submit while invalid or submitting', () => {
    let button: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(button.disabled).toBeTrue();

    component.bodyControl.setValue('Hello');
    component.isSubmitting = true;
    fixture.detectChanges();

    button = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(button.disabled).toBeTrue();
  });
});
