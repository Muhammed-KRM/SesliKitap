import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'relativeTime',
  standalone: true
})
export class RelativeTimePipe implements PipeTransform {
  private readonly rtf = new Intl.RelativeTimeFormat('tr', { numeric: 'auto' });

  transform(isoDate: string | Date | null | undefined): string {
    if (!isoDate) {
      return '—';
    }
    const time = typeof isoDate === 'string' ? Date.parse(isoDate) : isoDate.getTime();
    if (Number.isNaN(time)) {
      return '—';
    }

    const diffSec = Math.round((time - Date.now()) / 1000);
    const absSec = Math.abs(diffSec);

    if (absSec < 60) {
      return 'az önce';
    }
    if (absSec < 3600) {
      return this.rtf.format(Math.round(diffSec / 60), 'minute');
    }
    if (absSec < 86400) {
      return this.rtf.format(Math.round(diffSec / 3600), 'hour');
    }
    return this.rtf.format(Math.round(diffSec / 86400), 'day');
  }
}
