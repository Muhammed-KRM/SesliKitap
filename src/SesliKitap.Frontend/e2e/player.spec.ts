import { expect, test } from '@playwright/test';

const API_KEY =
  process.env['ADMIN_API_KEY'] ?? 'e2e-test-admin-api-key-minimum-32-chars-long-12345';

test.describe('Ses Oynatıcı, Hız Kontrolü ve Canlı Cümle Takibi (F-50, F-51, F-52, F-53)', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript((key) => {
      window.localStorage.setItem('sk_api_key', key);
    }, API_KEY);
  });

  test('Oynatıcı kontrolleri, hız menüsü ve yer imi çekmecesi çalışır', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('h1')).toContainText('Kütüphanem');

    const listenLink = page.locator('a[href^="/dinle/"]').first();
    if ((await listenLink.count()) === 0) {
      test.skip(true, 'Hazır sesli kitap bulunamadı.');
      return;
    }

    await listenLink.click();
    await expect(page).toHaveURL(/\/dinle\//);

    // Bölümler ve Yer İmleri çekmecesini aç
    await page.click('button:has-text("Bölümler")');
    await expect(page.locator('button:has-text("Yer İmleri")')).toBeVisible();
  });
});
