import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'duration',
  standalone: true
})
export class DurationPipe implements PipeTransform {
  transform(seconds: number | null | undefined): string {
    if (seconds === null || seconds === undefined || Number.isNaN(seconds) || seconds <= 0) {
      return '0 dk';
    }

    const totalSec = Math.round(seconds);
    if (totalSec < 60) {
      return `${totalSec} sn`;
    }

    const hours = Math.floor(totalSec / 3600);
    const minutes = Math.round((totalSec % 3600) / 60);

    if (hours === 0) {
      return `${minutes} dk`;
    }
    if (minutes === 0) {
      return `${hours} sa`;
    }
    return `${hours} sa ${minutes} dk`;
  }
}
