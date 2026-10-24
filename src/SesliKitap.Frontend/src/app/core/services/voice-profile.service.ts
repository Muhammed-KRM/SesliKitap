import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { SesCinsiyeti } from '../models/enums';
import {
  VoiceProfileDto,
  VoiceProfileUpdateRequest
} from '../models/voice-profile.model';
import { ApiBaseService } from './api-base.service';

export interface VoiceCreatePayload {
  ad: string;
  aciklama?: string;
  cinsiyet: SesCinsiyeti;
  referansMetin?: string;
  onayVerildiMi: boolean;
  sesDosyasi: File;
}

@Injectable({ providedIn: 'root' })
export class VoiceProfileService extends ApiBaseService {
  getList(): Observable<VoiceProfileDto[]> {
    return this.http.get<VoiceProfileDto[]>(this.url('/voice-profiles'));
  }

  create(payload: VoiceCreatePayload): Observable<VoiceProfileDto> {
    const form = new FormData();
    form.append('ad', payload.ad.trim());
    if (payload.aciklama?.trim()) {
      form.append('aciklama', payload.aciklama.trim());
    }
    form.append('cinsiyet', payload.cinsiyet);
    if (payload.referansMetin?.trim()) {
      form.append('referansMetin', payload.referansMetin.trim());
    }
    form.append('onayVerildiMi', String(payload.onayVerildiMi));
    form.append('sesDosyasi', payload.sesDosyasi, payload.sesDosyasi.name);

    return this.http.post<VoiceProfileDto>(this.url('/voice-profiles'), form);
  }

  update(id: string, body: VoiceProfileUpdateRequest): Observable<VoiceProfileDto> {
    return this.http.put<VoiceProfileDto>(this.url(`/voice-profiles/${id}`), body);
  }

  setDefault(id: string): Observable<void> {
    return this.http.post<void>(this.url(`/voice-profiles/${id}/default`), {});
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(this.url(`/voice-profiles/${id}`));
  }
}
