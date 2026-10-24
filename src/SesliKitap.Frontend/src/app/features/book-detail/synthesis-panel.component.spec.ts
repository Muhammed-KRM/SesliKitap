import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { SynthesisEstimateDto } from '../../core/models/synthesis.model';
import { VoiceProfileDto } from '../../core/models/voice-profile.model';
import { MediaTokenService } from '../../core/services/media-token.service';
import { SignalrService } from '../../core/services/signalr.service';
import { SynthesisService } from '../../core/services/synthesis.service';
import { VoiceProfileService } from '../../core/services/voice-profile.service';
import { SynthesisPanelComponent } from './synthesis-panel.component';

describe('SynthesisPanelComponent', () => {
  let component: SynthesisPanelComponent;
  let fixture: ComponentFixture<SynthesisPanelComponent>;
  let synthesisSpy: jasmine.SpyObj<SynthesisService>;
  let voiceSpy: jasmine.SpyObj<VoiceProfileService>;

  const sampleVoice: VoiceProfileDto = {
    id: 'vp-1',
    ad: 'Test Anlatıcı',
    aciklama: 'Açıklama',
    cinsiyet: 'Erkek',
    durum: 'Hazir',
    varsayilanMi: true,
    referansSureSn: 15,
    referansMetin: 'Test',
    ornekVar: true,
    parametreler: { cfgValue: 2.0, inferenceTimesteps: 10, konusmaHizi: 1.0 },
    hataMesaji: null,
    olusturmaTarihi: new Date().toISOString()
  };

  const sampleEstimate: SynthesisEstimateDto = {
    bookId: 'book-1',
    voiceProfileId: 'vp-1',
    seciliBolumSayisi: 3,
    toplamKelimeSayisi: 4500,
    toplamKarakterSayisi: 27000,
    tahminiSesSureSn: 1800,
    tahminiGpuSureSn: 450,
    tahminiMaliyetUsd: 0.12,
    buAyHarcananUsd: 0.5,
    aylikButceUsd: 10,
    butceAsiliyorMu: false
  };

  beforeEach(async () => {
    synthesisSpy = jasmine.createSpyObj<SynthesisService>('SynthesisService', [
      'estimate',
      'getList',
      'start',
      'cancel'
    ]);
    voiceSpy = jasmine.createSpyObj<VoiceProfileService>('VoiceProfileService', ['getList']);

    synthesisSpy.estimate.and.returnValue(of(sampleEstimate));
    synthesisSpy.getList.and.returnValue(
      of({ kayitlar: [], toplamKayit: 0, sayfa: 1, boyut: 10, toplamSayfa: 0 })
    );
    voiceSpy.getList.and.returnValue(of([sampleVoice]));

    await TestBed.configureTestingModule({
      imports: [SynthesisPanelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SynthesisService, useValue: synthesisSpy },
        { provide: VoiceProfileService, useValue: voiceSpy },
        {
          provide: MediaTokenService,
          useValue: { sampleUrl: () => Promise.resolve('/api/voice-profiles/vp-1/sample?t=abc') }
        },
        SignalrService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SynthesisPanelComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('bookId', 'book-1');
  });

  it('ses profili ve kapsam değişiminde 400ms debounce ile tek estimate çağrısı yapar', fakeAsync(() => {
    fixture.detectChanges();
    tick(400);
    expect(synthesisSpy.estimate).toHaveBeenCalledTimes(1);

    component.onScopeChange('all');
    component.onScopeChange('missing');
    tick(200);
    expect(synthesisSpy.estimate).toHaveBeenCalledTimes(1);

    tick(250);
    expect(synthesisSpy.estimate).toHaveBeenCalledTimes(2);
    expect(component.estimate()?.toplamKelimeSayisi).toBe(4500);
  }));
});
