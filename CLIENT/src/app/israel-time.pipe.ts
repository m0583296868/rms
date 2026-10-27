import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'israelTime',
  standalone: true
})
export class IsraelTimePipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) return '';

    const date = value instanceof Date ? value : new Date(this.asUtc(value));
    if (Number.isNaN(date.getTime())) return '';

    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: 'Asia/Jerusalem',
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23'
    }).formatToParts(date);

    const part = (type: string) => parts.find(item => item.type === type)?.value ?? '';
    return `${part('day')}/${part('month')}/${part('year')} ${part('hour')}:${part('minute')}`;
  }

  private asUtc(value: string): string {
    const text = value.trim();
    if (/[zZ]$|[+-]\d{2}:\d{2}$/.test(text)) return text;
    return `${text}Z`;
  }
}
