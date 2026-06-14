import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TicketCreateFormComponent } from './ticket-create-form.component';

describe('TicketCreateFormComponent', () => {
  let fixture: ComponentFixture<TicketCreateFormComponent>;
  let component: TicketCreateFormComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketCreateFormComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketCreateFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders only the supported create-ticket fields', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Title');
    expect(text).toContain('Description');
    expect(text).toContain('Ticket source: Web');
    expect(text).not.toContain('Priority');
    expect(text).not.toContain('Category');
    expect(text).not.toContain('Status');
  });

  it('requires title and description', () => {
    component.submit();

    expect(component.form.controls.title.hasError('required')).toBeTrue();
    expect(component.form.controls.description.hasError('required')).toBeTrue();
  });

  it('rejects whitespace-only title and description', () => {
    component.form.controls.title.setValue('   ');
    component.form.controls.description.setValue('\n  ');
    component.submit();

    expect(component.form.controls.title.hasError('whitespace')).toBeTrue();
    expect(component.form.controls.description.hasError('whitespace')).toBeTrue();
  });

  it('enforces backend maximum lengths', () => {
    component.form.controls.title.setValue('x'.repeat(201));
    component.form.controls.description.setValue('x'.repeat(4001));

    expect(component.form.controls.title.hasError('maxlength')).toBeTrue();
    expect(component.form.controls.description.hasError('maxlength')).toBeTrue();
  });

  it('trims text fields before submitting', () => {
    spyOn(component.createTicket, 'emit');
    component.form.controls.title.setValue('  Payment issue  ');
    component.form.controls.description.setValue('  Cannot complete payment.\n  ');

    component.submit();

    expect(component.createTicket.emit).toHaveBeenCalledWith({
      title: 'Payment issue',
      description: 'Cannot complete payment.'
    });
  });

  it('prevents duplicate submission while submitting', () => {
    spyOn(component.createTicket, 'emit');
    fixture.componentRef.setInput('isSubmitting', true);
    component.form.controls.title.setValue('Payment issue');
    component.form.controls.description.setValue('Cannot complete payment.');
    fixture.detectChanges();

    component.submit();

    expect(component.createTicket.emit).not.toHaveBeenCalled();
  });
});
