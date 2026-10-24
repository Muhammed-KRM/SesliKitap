export interface SentenceTs {
  i: number;
  p: number;
  s: number;
  e: number;
  t: string;
}

export interface ChapterTimestamps {
  version: number;
  chapterId: string;
  durationMs: number;
  sentences: SentenceTs[];
}
