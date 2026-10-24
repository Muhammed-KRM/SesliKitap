import { Injectable } from '@angular/core';

export interface MediaSessionMeta {
  title: string;
  artist: string;
  album: string;
  artworkUrl: string;
}

export interface MediaSessionController {
  play(): Promise<void> | void;
  pause(): void;
  skip(deltaSec: number): void;
  prevChapter(): Promise<void> | void;
  nextChapter(): Promise<void> | void;
  seek(sec: number): void;
}

@Injectable({ providedIn: 'root' })
export class MediaSessionService {
  private get isSupported(): boolean {
    return typeof navigator !== 'undefined' && 'mediaSession' in navigator;
  }

  update(meta: MediaSessionMeta): void {
    if (!this.isSupported || typeof MediaMetadata === 'undefined') {
      return;
    }

    try {
      navigator.mediaSession.metadata = new MediaMetadata({
        title: meta.title,
        artist: meta.artist,
        album: meta.album,
        artwork: meta.artworkUrl
          ? [{ src: meta.artworkUrl, sizes: '512x512', type: 'image/jpeg' }]
          : []
      });
    } catch {
      // ignore unsupported metadata fields
    }
  }

  setPositionState(duration: number, position: number, playbackRate: number): void {
    if (!this.isSupported || typeof navigator.mediaSession.setPositionState !== 'function') {
      return;
    }

    if (!Number.isFinite(duration) || duration <= 0) {
      return;
    }

    try {
      navigator.mediaSession.setPositionState({
        duration: Math.max(0, duration),
        position: Math.min(Math.max(0, position), duration),
        playbackRate: Math.max(0.5, playbackRate)
      });
    } catch {
      // ignore position state errors
    }
  }

  bindHandlers(player: MediaSessionController): void {
    if (!this.isSupported) {
      return;
    }

    const ms = navigator.mediaSession;
    const safeBind = (action: MediaSessionAction, handler: MediaSessionActionHandler) => {
      try {
        ms.setActionHandler(action, handler);
      } catch {
        // unsupported action in current browser
      }
    };

    safeBind('play', () => {
      void player.play();
    });
    safeBind('pause', () => {
      player.pause();
    });
    safeBind('seekbackward', (details) => {
      player.skip(-(details.seekOffset ?? 15));
    });
    safeBind('seekforward', (details) => {
      player.skip(details.seekOffset ?? 30);
    });
    safeBind('previoustrack', () => {
      void player.prevChapter();
    });
    safeBind('nexttrack', () => {
      void player.nextChapter();
    });
    safeBind('seekto', (details) => {
      if (details.seekTime != null) {
        player.seek(details.seekTime);
      }
    });
  }
}
