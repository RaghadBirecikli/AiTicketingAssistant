import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiEndpoints } from '../../../core/api/api-endpoints';
import { ApiService } from '../../../core/api/api.service';
import { UserRole } from '../../../core/models/role.model';
import {
  AddInternalNoteRequest,
  AddInternalNoteResponse,
  AddTicketMessageRequest,
  AddTicketMessageResponse,
  AgentLookup,
  AssignTicketRequest,
  AssignTicketResponse,
  ChangeTicketStatusRequest,
  ChangeTicketStatusResponse,
  CreateTicketRequest,
  CreateTicketResponse,
  PagedTicketList,
  TicketDetails,
  TicketDetailsMessage,
  TicketListItem,
  TicketListQuery
} from '../models/ticket-list.models';
import { ticketListQueryToParams } from './ticket-list-query.util';

@Injectable({ providedIn: 'root' })
export class TicketListService {
  private readonly api = inject(ApiService);

  getTickets(query: TicketListQuery, role: UserRole | null): Observable<PagedTicketList> {
    return this.api.get<PagedTicketList>(ApiEndpoints.tickets, ticketListQueryToParams(query, role));
  }

  getTicketById(ticketId: string): Observable<TicketDetails> {
    return this.api.get<TicketDetails>(`${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}`);
  }

  createTicket(title: string, description: string): Observable<CreateTicketResponse> {
    const request: CreateTicketRequest = {
      title,
      description,
      customerEmail: null,
      customerName: null,
      source: 'Web'
    };

    return this.api.post<CreateTicketRequest, CreateTicketResponse>(ApiEndpoints.tickets, request);
  }

  addMessage(ticketId: string, body: string): Observable<TicketDetailsMessage> {
    const request: AddTicketMessageRequest = {
      message: body,
      isInternalNote: false,
      createdByUserId: null,
      createdByDisplayName: null
    };

    return this.api
      .post<AddTicketMessageRequest, AddTicketMessageResponse>(`${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/messages`, request)
      .pipe(map(response => ({
        id: response.message.id,
        ticketId: response.message.ticketId,
        senderUserId: response.message.createdByUserId,
        senderRole: 'Unknown',
        senderDisplayName: response.message.createdByDisplayName,
        body: response.message.message,
        isInternalNote: response.message.isInternalNote,
        createdAtUtc: response.message.createdAtUtc
      })));
  }

  addInternalNote(ticketId: string, body: string): Observable<TicketDetailsMessage> {
    return this.api
      .post<AddInternalNoteRequest, AddInternalNoteResponse>(`${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/internal-notes`, { body })
      .pipe(map(response => response.message));
  }

  assignTicket(ticketId: string, assignedToUserId: string): Observable<TicketListItem> {
    const request: AssignTicketRequest = {
      assignedToUserId
    };

    return this.api
      .patch<AssignTicketRequest, AssignTicketResponse>(`${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/assign`, request)
      .pipe(map(response => response.ticket));
  }

  changeStatus(ticketId: string, status: ChangeTicketStatusRequest['status']): Observable<TicketListItem> {
    const request: ChangeTicketStatusRequest = {
      status,
      changedByUserId: null,
      changedByDisplayName: null
    };

    return this.api
      .patch<ChangeTicketStatusRequest, ChangeTicketStatusResponse>(`${ApiEndpoints.tickets}/${encodeURIComponent(ticketId)}/status`, request)
      .pipe(map(response => response.ticket));
  }

  getAgents(): Observable<readonly AgentLookup[]> {
    return this.api.get<readonly AgentLookup[]>(ApiEndpoints.users.agents);
  }
}
