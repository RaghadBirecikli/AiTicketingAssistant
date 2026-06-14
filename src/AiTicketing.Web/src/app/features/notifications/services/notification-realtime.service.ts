import { InjectionToken, computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthStorageService } from '../../../core/auth/auth-storage.service';
import { NotificationStateService } from './notification-state.service';

export type NotificationRealtimeConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface NotificationHubConnection {
  readonly state: signalR.HubConnectionState;
  start(): Promise<void>;
  stop(): Promise<void>;
  on(methodName: string, callback: (payload: unknown) => void): void;
  onreconnecting(callback: () => void): void;
  onreconnected(callback: () => void): void;
  onclose(callback: () => void): void;
}

export type NotificationHubConnectionFactory = (
  hubUrl: string,
  accessTokenFactory: () => string
) => NotificationHubConnection;

export const NOTIFICATION_HUB_ROUTE = '/hubs/notifications';
export const NOTIFICATION_RECEIVED_EVENT = 'NotificationReceived';
export const NOTIFICATION_RECONNECT_DELAYS = [0, 2000, 5000, 10000, 30000] as const;

export const NOTIFICATION_HUB_CONNECTION_FACTORY = new InjectionToken<NotificationHubConnectionFactory>(
  'NOTIFICATION_HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => (hubUrl, accessTokenFactory) => new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory })
      .withAutomaticReconnect([...NOTIFICATION_RECONNECT_DELAYS])
      .build()
  }
);

@Injectable({ providedIn: 'root' })
export class NotificationRealtimeService {
  private readonly authService = inject(AuthService);
  private readonly storage = inject(AuthStorageService);
  private readonly notificationState = inject(NotificationStateService);
  private readonly connectionFactory = inject(NOTIFICATION_HUB_CONNECTION_FACTORY);

  private connection: NotificationHubConnection | null = null;
  private startSequence = 0;
  private initialized = false;

  readonly connectionState = signal<NotificationRealtimeConnectionState>('disconnected');
  readonly isConnected = computed(() => this.connectionState() === 'connected');

  constructor() {
    this.initialize();
  }

  initialize(): void {
    if (this.initialized) {
      return;
    }

    this.initialized = true;
    effect(() => {
      const user = this.authService.currentUser();
      untracked(() => {
        if (user && this.storage.getToken()) {
          void this.start();
        } else {
          void this.stop();
        }
      });
    });
  }

  async start(): Promise<void> {
    const token = this.storage.getToken();
    if (!this.authService.isAuthenticated() || !token || this.isActiveOrStarting()) {
      return;
    }

    const sequence = ++this.startSequence;
    const connection = this.createConnection();
    this.connection = connection;
    this.connectionState.set('connecting');

    try {
      await connection.start();
      if (this.startSequence !== sequence || this.connection !== connection || !this.authService.isAuthenticated()) {
        await this.safeStop(connection);
        return;
      }

      this.connectionState.set('connected');
    } catch {
      if (this.connection === connection) {
        this.connection = null;
        this.connectionState.set('disconnected');
      }
    }
  }

  async stop(): Promise<void> {
    this.startSequence++;
    const connection = this.connection;
    this.connection = null;
    this.connectionState.set('disconnected');

    if (connection) {
      await this.safeStop(connection);
    }
  }

  private createConnection(): NotificationHubConnection {
    const connection = this.connectionFactory(this.hubUrl(), () => this.storage.getToken() ?? '');
    connection.on(NOTIFICATION_RECEIVED_EVENT, payload => this.notificationState.receiveRealtimeNotification(payload));
    connection.onreconnecting(() => {
      if (this.connection === connection) {
        this.connectionState.set('reconnecting');
      }
    });
    connection.onreconnected(() => {
      if (this.connection === connection) {
        this.connectionState.set('connected');
      }
    });
    connection.onclose(() => {
      if (this.connection === connection) {
        this.connection = null;
        this.connectionState.set('disconnected');
      }
    });
    return connection;
  }

  private hubUrl(): string {
    const baseUrl = environment.apiBaseUrl.replace(/\/$/, '');
    return baseUrl ? `${baseUrl}${NOTIFICATION_HUB_ROUTE}` : NOTIFICATION_HUB_ROUTE;
  }

  private isActiveOrStarting(): boolean {
    return this.connection !== null
      || this.connectionState() === 'connecting'
      || this.connectionState() === 'connected'
      || this.connectionState() === 'reconnecting';
  }

  private async safeStop(connection: NotificationHubConnection): Promise<void> {
    try {
      await connection.stop();
    } catch {
      // SignalR disconnect failures should not affect REST notification behavior.
    }
  }
}
