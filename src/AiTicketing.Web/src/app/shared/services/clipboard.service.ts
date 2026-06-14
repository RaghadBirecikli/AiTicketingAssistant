import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ClipboardService {
  async copyText(text: string): Promise<void> {
    await navigator.clipboard.writeText(text);
  }
}
