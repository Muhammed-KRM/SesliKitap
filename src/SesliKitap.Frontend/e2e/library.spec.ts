import { expect, test } from '@playwright/test';

const API_KEY =
  process.env['ADMIN_API_KEY'] ?? 'e2e-test-admin-api-key-minimum-32-chars-long-12345';

test.describe('Kütüphane ve Erişim Anahtarı Akışı (F-01, F-04, F-75)', () => {
  test('Erişim anahtarı girişi, kitap yükleme ve inceleme bekliyor durumuna geçiş', async ({
    page
  }) => {
    await page.goto('/erisim');
    await expect(page.locator('h1')).toContainText('Erişim Anahtarı');

    await page.fill('input[type="password"]', API_KEY);
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL(/\/$/);
    await expect(page.locator('h1')).toContainText('Kütüphanem');

    // Yeni TXT kitap yükleme
    await page.click('button:has-text("Kitap Yükle")');
    const sampleTxt = [
      'BÖLÜM 1: Başlangıç',
      '',
      'Güneşli bir sabah Boğaz kıyısında yürüyüş yaparken martıların sesini dinliyordu.',
      'Uzaktan geçen vapurun düdüğü sabah sessizliğini bozdu.',
      '',
      'BÖLÜM 2: Yolculuk',
      '',
      'İkinci bölümde kahramanımız Anadolu yakasından Avrupa yakasına geçti.'
    ].join('\n');

    const fileInput = page.locator('input[type="file"]');
    await fileInput.setInputFiles({
      name: 'e2e-ornek-kitap.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from(sampleTxt, 'utf-8')
    });

    await page.click('button:has-text("Yükle ve İşle")');

    // Kitap detay sayfasına yönlendirme ve işlenme tamamlanması
    await expect(page).toHaveURL(/\/kitap\//, { timeout: 15000 });
    await expect(page.locator('h1')).toContainText('e2e-ornek-kitap', { timeout: 15000 });
  });
});
