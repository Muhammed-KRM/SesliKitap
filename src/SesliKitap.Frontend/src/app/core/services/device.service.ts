import { Injectable, signal } from '@angular/core';

const DEVICE_ID_KEY = 'sk.deviceId';
const DEVICE_NAME_KEY = 'sk.deviceName';

@Injectable({ providedIn: 'root' })
export class DeviceService {
  readonly deviceId = signal<string>(this.loadOrCreateId());
  readonly deviceName = signal<string>(this.loadOrCreateName());

  setDeviceName(name: string): void {
    const trimmed = name.trim() || this.detectDefaultName();
    this.deviceName.set(trimmed);
    try {
      localStorage.setItem(DEVICE_NAME_KEY, trimmed);
    } catch {
      // ignore
    }
  }

  private loadOrCreateId(): string {
    try {
      const existing = localStorage.getItem(DEVICE_ID_KEY);
      if (existing) {
        return existing;
      }
      const created =
        typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
          ? crypto.randomUUID()
          : `dev-${Math.random().toString(36).slice(2)}-${Date.now()}`;
      localStorage.setItem(DEVICE_ID_KEY, created);
      return created;
    } catch {
      return `dev-${Date.now()}`;
    }
  }

  private loadOrCreateName(): string {
    try {
      const existing = localStorage.getItem(DEVICE_NAME_KEY);
      if (existing) {
        return existing;
      }
      const detected = this.detectDefaultName();
      localStorage.setItem(DEVICE_NAME_KEY, detected);
      return detected;
    } catch {
      return 'Web Tarayıcı';
    }
  }

  private detectDefaultName(): string {
    if (typeof navigator === 'undefined') {
      return 'Web Tarayıcı';
    }
    const ua = navigator.userAgent || '';
    let os = 'Masaüstü';
    if (/android/i.test(ua)) os = 'Android';
    else if (/iphone|ipad|ipod/i.test(ua)) os = 'iOS';
    else if (/win/i.test(ua)) os = 'Windows';
    else if (/mac/i.test(ua)) os = 'macOS';
    else if (/linux/i.test(ua)) os = 'Linux';

    let browser = 'Tarayıcı';
    if (/edg/i.test(ua)) browser = 'Edge';
    else if (/chrome|crios/i.test(ua)) browser = 'Chrome';
    else if (/firefox|fxios/i.test(ua)) browser = 'Firefox';
    else if (/safari/i.test(ua)) browser = 'Safari';

    return `${os} · ${browser}`;
  }
}
