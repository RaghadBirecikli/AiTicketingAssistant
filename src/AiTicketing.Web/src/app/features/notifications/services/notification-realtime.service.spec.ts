import { TestBed } from '@angular/core/testing';
import { computed, signal } from '@angular/core';
import { HubConnectionState } from '@microsoft/signalr';
import {
  NOTIFICATION_HUB_CONNECTION_FACTORY,
  NOTIFICATION_RECEIVED_EVENT,
  NOTIFICATION_RECONNECT_DELAYS,
  NotificationHubConnection,
  NotificationRealtimeService
} from './notification-realtime.service';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthStorageService } from '../../../core/auth/auth-storage.service';
import { NotificationStateService } from './notification-state.service';
import { CurrentUser } from '../../../core/auth/auth.models';
import { UserRole } from '../../../core/models/role.model';
import { NotificationResponse } from '../models/notification.models';

const liveNotification: NotificationResponse = {
  id: 'notification-id',
  userId: 'user-id',
  title: 'Ticket assigned',
  message: 'Ticket assigned to you.',
  type: 'TicketAssigned',
  ticketId: '11111111-1111-4111-8111-111111111111',
  isRead: false,
  createdAtUtc: '2026-06-04T12:00:00+00:00',
  readAtUtc: null
};

class FakeConnection implements NotificationHubConnection {
  state = HubConnectionState.Disconnected;
  startSpy = jasmine.createSpy('start').and.callFake(async () => {
    this.state = HubConnectionState.Connected;
  });
  stopSpy = jasmine.createSpy('stop').and.callFake(async () => {
    this.state = HubConnectionState.Disconnected;
  });
  handlers = new Map<string, (payload: unknown) => void>();
  reconnectingCallback: (() => void) | null = null;
  reconnectedCallback: (() => void) | null = null;
  closeCallback: (() => void) | null = null;

  start(): Promise<void> {
    return this.startSpy();
  }

  stop(): Promise<void> {
    return this.stopSpy();
  }

  on(methodName: string, callback: (payload: unknown) => void): void {
    this.handlers.set(methodName, callback);
  }

  onreconnecting(callback: () => void): void {
    this.reconnectingCallback = callback;
  }

  onreconnected(callback: () => void): void {
    this.reconnectedCallback = callback;
  }

  onclose(callback: () => void): void {
    this.closeCallback = callback;
  }
}

class AuthServiceStub {
  currentUser = signal<CurrentUser | null>({
    id: 'user-id',
    email: 'user@example.com',
    displayName: 'User',
    roles: ['Admin']
  });
  currentRole = signal<UserRole | null>('Admin');
  isAuthenticated = computed(() => this.currentUser() !== null);
}

class AuthStorageStub {
  token: string | null = 'token-1';
  getToken = jasmine.createSpy('getToken').and.callFake(() => this.token);
}

describe('NotificationRealtimeService', () => {
  let auth: AuthServiceStub;
  let storage: AuthStorageStub;
  let state: jasmine.SpyObj<NotificationStateService>;
  let connections: FakeConnection[];
  let factoryCalls: Array<{ hubUrl: string; accessTokenFactory: () => string }>;
  let service: NotificationRealtimeService;

  beforeEach(() => {
    auth = new AuthServiceStub();
    storage = new AuthStorageStub();
    state = jasmine.createSpyObj<NotificationStateService>('NotificationStateService', ['receiveRealtimeNotification']);
    connections = [];
    factoryCalls = [];

    TestBed.configureTestingModule({
      providers: [
        NotificationRealtimeService,
        { provide: AuthService, useValue: auth },
        { provide: AuthStorageService, useValue: storage },
        { provide: NotificationStateService, useValue: state },
        {
          provide: NOTIFICATION_HUB_CONNECTION_FACTORY,
          useValue: (hubUrl: string, accessTokenFactory: () => string) => {
            factoryCalls.push({ hubUrl, accessTokenFactory });
            const connection = new FakeConnection();
            connections.push(connection);
            return connection;
          }
        }
      ]
    });
  });

  async function createService(): Promise<void> {
    service = TestBed.inject(NotificationRealtimeService);
    TestBed.flushEffects();
    await Promise.resolve();
  }

  it('uses the correct hub URL and reads tokens through AuthStorageService', async () => {
    await createService();

    expect(factoryCalls[0].hubUrl).toBe('https://localhost:7194/hubs/notifications');
    expect(factoryCalls[0].accessTokenFactory()).toBe('token-1');
    expect(storage.getToken).toHaveBeenCalled();
  });

  it('does not start a connection for anonymous users', async () => {
    auth.currentUser.set(null);
    await createService();

    expect(connections.length).toBe(0);
  });

  it('starts after authenticated restoration and uses one connection per session', async () => {
    await createService();
    await service.start();

    expect(connections.length).toBe(1);
    expect(connections[0].startSpy).toHaveBeenCalledTimes(1);
    expect(service.connectionState()).toBe('connected');
  });

  it('starts after a later login and stops on logout or invalid auth', async () => {
    auth.currentUser.set(null);
    await createService();
    expect(connections.length).toBe(0);

    auth.currentUser.set({
      id: 'user-2',
      email: 'user2@example.com',
      displayName: 'User 2',
      roles: ['Customer']
    });
    TestBed.flushEffects();
    await Promise.resolve();

    expect(connections.length).toBe(1);

    auth.currentUser.set(null);
    TestBed.flushEffects();
    await Promise.resolve();

    expect(connections[0].stopSpy).toHaveBeenCalled();
    expect(service.connectionState()).toBe('disconnected');
  });

  it('listens to the exact NotificationReceived event and forwards valid payloads to state', async () => {
    await createService();

    expect(connections[0].handlers.has(NOTIFICATION_RECEIVED_EVENT)).toBeTrue();

    connections[0].handlers.get(NOTIFICATION_RECEIVED_EVENT)?.(liveNotification);

    expect(state.receiveRealtimeNotification).toHaveBeenCalledWith(liveNotification);
  });

  it('tracks reconnecting, reconnected, and closed states safely', async () => {
    await createService();

    connections[0].reconnectingCallback?.();
    expect(service.connectionState()).toBe('reconnecting');

    connections[0].reconnectedCallback?.();
    expect(service.connectionState()).toBe('connected');

    connections[0].closeCallback?.();
    expect(service.connectionState()).toBe('disconnected');
  });

  it('connection start failure does not throw or expose raw errors', async () => {
    TestBed.overrideProvider(NOTIFICATION_HUB_CONNECTION_FACTORY, {
      useValue: (hubUrl: string, accessTokenFactory: () => string) => {
        factoryCalls.push({ hubUrl, accessTokenFactory });
        const connection = new FakeConnection();
        connection.startSpy.and.rejectWith(new Error('token websocket raw failure'));
        connections.push(connection);
        return connection;
      }
    });

    await createService();

    expect(service.connectionState()).toBe('disconnected');
  });

  it('logout during connection startup does not leave a live connection', async () => {
    let resolveStart!: () => void;
    const startGate = new Promise<void>(resolve => {
      resolveStart = resolve;
    });
    TestBed.overrideProvider(NOTIFICATION_HUB_CONNECTION_FACTORY, {
      useValue: (hubUrl: string, accessTokenFactory: () => string) => {
        factoryCalls.push({ hubUrl, accessTokenFactory });
        const connection = new FakeConnection();
        connection.startSpy.and.returnValue(startGate);
        connections.push(connection);
        return connection;
      }
    });

    await createService();
    auth.currentUser.set(null);
    TestBed.flushEffects();
    await Promise.resolve();
    resolveStart();
    await Promise.resolve();

    expect(connections[0].stopSpy).toHaveBeenCalled();
    expect(service.connectionState()).toBe('disconnected');
  });

  it('automatic reconnect schedule is bounded and documented in code', () => {
    expect(NOTIFICATION_RECONNECT_DELAYS).toEqual([0, 2000, 5000, 10000, 30000]);
  });
});
