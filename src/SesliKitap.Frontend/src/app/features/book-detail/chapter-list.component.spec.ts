import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ChapterSummary } from '../../core/models/chapter.model';
import { ChapterListComponent } from './chapter-list.component';

describe('ChapterListComponent', () => {
  let component: ChapterListComponent;
  let fixture: ComponentFixture<ChapterListComponent>;

  const sampleChapters: ChapterSummary[] = [
    {
      id: 'ch-1',
      siraNo: 1,
      baslik: 'Bölüm 1',
      kelimeSayisi: 1000,
      karakterSayisi: 6000,
      tahminiSureSn: 420,
      dahilMi: true,
      otomatikHaricNedeni: null,
      metinSurumu: 1,
      sesDurumu: 'Yok',
      sesSurumu: 0,
      sureSn: null,
      baslangicSayfasi: 1,
      bitisSayfasi: 10,
      hataMesaji: null
    },
    {
      id: 'ch-2',
      siraNo: 2,
      baslik: 'Bölüm 2',
      kelimeSayisi: 1200,
      karakterSayisi: 7200,
      tahminiSureSn: 500,
      dahilMi: true,
      otomatikHaricNedeni: null,
      metinSurumu: 1,
      sesDurumu: 'Yok',
      sesSurumu: 0,
      sureSn: null,
      baslangicSayfasi: 11,
      bitisSayfasi: 22,
      hataMesaji: null
    },
    {
      id: 'ch-3',
      siraNo: 3,
      baslik: 'Bölüm 3',
      kelimeSayisi: 900,
      karakterSayisi: 5400,
      tahminiSureSn: 380,
      dahilMi: true,
      otomatikHaricNedeni: null,
      metinSurumu: 1,
      sesDurumu: 'Yok',
      sesSurumu: 0,
      sureSn: null,
      baslangicSayfasi: 23,
      bitisSayfasi: 30,
      hataMesaji: null
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChapterListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(ChapterListComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('bookId', 'book-1');
    fixture.componentRef.setInput('chapters', sampleChapters);
    fixture.detectChanges();
  });

  it('ardışık iki bölüm seçildiğinde canMergeSelected true döner', () => {
    component.toggleSelect('ch-1');
    component.toggleSelect('ch-2');
    expect(component.selectedCount()).toBe(2);
    expect(component.canMergeSelected()).toBeTrue();
  });

  it('ardışık olmayan bölümler seçildiğinde canMergeSelected false döner', () => {
    component.toggleSelect('ch-1');
    component.toggleSelect('ch-3');
    expect(component.selectedCount()).toBe(2);
    expect(component.canMergeSelected()).toBeFalse();
  });
});
