import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'clock',
  standalone: true
})
export class ClockPipe implements PipeTransform {
  transform(seconds: number | null | undefined): string {
    if (seconds === null || seconds === undefined || Number.isNaN(seconds) || seconds < 0) {
      return '00:00';
    }

    const total = Math.floor(seconds);
    const h = Math.floor(total / 3600);
    const m = Math.floor((total % 3600) / 60);
    const s = total % 60;

    const mm = String(m).padStart(2, '0');
    const ss = String(s).padStart(2, '0');

    if (h > 0) {
      return `${h}:${mm}:${ss}`;
    }
    return `${mm}:${ss}`;
  }
}
