import { expect, test } from '@playwright/test';

const API_KEY =
  process.env['ADMIN_API_KEY'] ?? 'e2e-test-admin-api-key-minimum-32-chars-long-12345';

test.describe('Cihazlar Arası Dinleme Konumu Senkronizasyonu (F-56)', () => {
  test('İki ayrı tarayıcı bağlamı (PC ve Mobil) aynı kütüphaneye bağlanır', async ({
    browser
  }) => {
    const pcContext = await browser.newContext();
    const mobileContext = await browser.newContext();

    const pcPage = await pcContext.newPage();
    const mobilePage = await mobileContext.newPage();

    await pcPage.addInitScript((key) => {
      window.localStorage.setItem('sk_api_key', key);
      window.localStorage.setItem('sk_device_name', 'Masaüstü PC');
    }, API_KEY);

    await mobilePage.addInitScript((key) => {
      window.localStorage.setItem('sk_api_key', key);
      window.localStorage.setItem('sk_device_name', 'Cep Telefonu');
    }, API_KEY);

    await pcPage.goto('/');
    await mobilePage.goto('/');

    await expect(pcPage.locator('h1')).toContainText('Kütüphanem');
    await expect(mobilePage.locator('h1')).toContainText('Kütüphanem');

    await pcContext.close();
    await mobileContext.close();
  });
});
