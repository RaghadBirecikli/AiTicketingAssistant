import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TicketAssignmentControlComponent } from './ticket-assignment-control.component';

describe('TicketAssignmentControlComponent', () => {
  let fixture: ComponentFixture<TicketAssignmentControlComponent>;
  let component: TicketAssignmentControlComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TicketAssignmentControlComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TicketAssignmentControlComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('agents', [
      { id: 'agent-id', email: 'agent@example.com', displayName: 'Support Agent' }
    ]);
    fixture.componentRef.setInput('currentAssignedToUserId', 'agent-id');
    fixture.detectChanges();
  });

  it('selects the current assigned Agent', () => {
    expect(component.form.controls.assignedToUserId.value).toBe('agent-id');
  });

  it('emits reassignment details', () => {
    spyOn(component.assignTicket, 'emit');

    component.submit();

    expect(component.assignTicket.emit).toHaveBeenCalledWith({
      assignedToUserId: 'agent-id',
      assignedToDisplayName: 'Support Agent'
    });
  });

  it('prevents duplicate assignment submission while submitting', () => {
    spyOn(component.assignTicket, 'emit');
    fixture.componentRef.setInput('isSubmitting', true);
    fixture.detectChanges();

    component.submit();

    expect(component.assignTicket.emit).not.toHaveBeenCalled();
  });

  it('handles an empty agent list safely', () => {
    fixture.componentRef.setInput('agents', []);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No Agents are available');
    expect((fixture.nativeElement as HTMLElement).querySelector('button')?.hasAttribute('disabled')).toBeTrue();
  });
});
