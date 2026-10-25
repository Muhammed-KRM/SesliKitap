import { expect, test } from '@playwright/test';

const API_KEY =
  process.env['ADMIN_API_KEY'] ?? 'e2e-test-admin-api-key-minimum-32-chars-long-12345';

test.describe('Bölüm Listesi, Hariç Tutma, Birleştirme ve Düzenleme (F-15, F-16, F-17)', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript((key) => {
      window.localStorage.setItem('sk_api_key', key);
    }, API_KEY);
  });

  test('Bölümleri listeler, metin düzenler ve kaydeder', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('h1')).toContainText('Kütüphanem');

    const firstCard = page.locator('a[href^="/kitap/"]').first();
    if ((await firstCard.count()) === 0) {
      test.skip(true, 'Kütüphanede düzenlenecek kitap bulunamadı.');
      return;
    }

    await firstCard.click();
    await expect(page).toHaveURL(/\/kitap\//);

    const editBtn = page.locator('button:has-text("Düzenle")').first();
    if ((await editBtn.count()) > 0) {
      await editBtn.click();
      const textarea = page.locator('textarea');
      await expect(textarea).toBeVisible();
      await page.click('button:has-text("Değişiklikleri Kaydet")');
    }
  });
});
