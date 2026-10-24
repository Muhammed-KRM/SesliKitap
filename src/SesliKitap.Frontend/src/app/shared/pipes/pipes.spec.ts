import { ClockPipe } from './clock.pipe';
import { DurationPipe } from './duration.pipe';
import { FileSizePipe } from './file-size.pipe';

describe('Paylaşılan Pipe Testleri', () => {
  it('DurationPipe saniyeyi Türkçe saat/dakika biçimine çevirir', () => {
    const pipe = new DurationPipe();
    expect(pipe.transform(0)).toBe('0 dk');
    expect(pipe.transform(45)).toBe('45 sn');
    expect(pipe.transform(3600 + 720)).toBe('1 sa 12 dk');
  });

  it('ClockPipe saniyeyi mm:ss ve hh:mm:ss biçimine çevirir', () => {
    const pipe = new ClockPipe();
    expect(pipe.transform(0)).toBe('00:00');
    expect(pipe.transform(125)).toBe('02:05');
    expect(pipe.transform(3725)).toBe('1:02:05');
  });

  it('FileSizePipe bayt değerini Türkçe virgüllü MB/KB biçimine çevirir', () => {
    const pipe = new FileSizePipe();
    expect(pipe.transform(0)).toBe('0 B');
    expect(pipe.transform(1024)).toBe('1,0 KB');
    expect(pipe.transform(1572864)).toBe('1,5 MB');
  });
});
