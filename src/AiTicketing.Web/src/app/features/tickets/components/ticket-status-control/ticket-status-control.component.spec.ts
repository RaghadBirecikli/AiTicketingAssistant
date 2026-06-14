import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TicketStatusControlComponent } from './ticket-status-control.component';

describe('TicketStatusControlComponent', () => {
  let fixture: ComponentFixture<TicketStatusControlComponent>;
  let component: TicketStatusControlComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketStatusControlComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketStatusControlComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('currentStatus', 'Open');
    fixture.detectChanges();
  });

  it('keeps actual backend status values while rendering readable labels', () => {
    const options = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLOptionElement>('option'));

    expect(options.map(option => option.value)).toEqual(['Open', 'InProgress', 'WaitingForCustomer', 'Resolved', 'Closed']);
    expect(options.map(option => option.textContent?.trim())).toEqual(['Open', 'In Progress', 'Waiting for Customer', 'Resolved', 'Closed']);
  });

  it('requires confirmation before closing and cancel sends no request', () => {
    spyOn(component.changeStatus, 'emit');
    component.form.controls.status.setValue('Closed');

    component.submit();
    expect(component.confirmationPending()).toBeTrue();
    expect(component.changeStatus.emit).not.toHaveBeenCalled();

    component.cancelConfirmation();
    expect(component.confirmationPending()).toBeFalse();
  });

  it('emits after closing is confirmed', () => {
    spyOn(component.changeStatus, 'emit');
    component.form.controls.status.setValue('Closed');

    component.submit();
    component.submit();

    expect(component.changeStatus.emit).toHaveBeenCalledWith('Closed');
  });

  it('prevents duplicate status submission while submitting', () => {
    spyOn(component.changeStatus, 'emit');
    fixture.componentRef.setInput('isSubmitting', true);
    fixture.detectChanges();

    component.submit();

    expect(component.changeStatus.emit).not.toHaveBeenCalled();
  });
});
