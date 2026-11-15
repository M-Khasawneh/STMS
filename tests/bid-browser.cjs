// Use ONLY the --bid-phase3-browser-fixture host. Its marker is verified before any mutation.
// Reuses an existing Playwright installation; installs no packages or browsers.
const {chromium} = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs');
const path = require('path');
(async () => {
  const [base, tender, output] = process.argv.slice(2);
  if (!base || !tender || !output) throw Error('Usage: node tests/bid-browser.cjs fixture-url tender-id output-directory');
  const browser = await chromium.launch({headless: true, channel: 'msedge'});
  const errors = []; let checks = 0; let page;
  const ok = (condition, message) => { if (!condition) throw Error(message); checks++; console.log('PASS: ' + message); };
  try {
    const context = await browser.newContext({extraHTTPHeaders: {'X-Bid-Test-Actor': 'supplier'}});
    page = await context.newPage();
    page.on('pageerror', e => errors.push(e.message));
    const marker = await page.goto(base + '/Bids');
    if (marker.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing to run against a non-fixture application');
    fs.mkdirSync(output, {recursive: true});
    for (const width of [1440, 768, 390, 320]) {
      await page.setViewportSize({width, height: 1000});
      const result = await page.goto(`${base}/Bids/Create?tenderId=${tender}`);
      ok(result.status() === 200, `create page renders ${width}px`);
      await page.waitForTimeout(700);
      ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `no page overflow ${width}px`);
      ok(await page.locator('[data-bid-line]').count() === 2, 'both required items present');
      if (width === 1440 || width === 390) await page.screenshot({path: path.join(output, `create-${width}.png`), fullPage: true});
    }
    const menu = page.locator('.menu-toggle');
    await menu.click(); ok(await menu.getAttribute('aria-expanded') === 'true', 'mobile navigation opens');
    await page.keyboard.press('Escape'); ok(await menu.getAttribute('aria-expanded') === 'false', 'Escape closes mobile navigation');
    await page.locator('[data-save-draft]').click();
    ok(await page.locator('input[name="Form.DeliveryTimeframe"]').evaluate(e => !e.validity.valid), 'required form feedback remains active');
    await page.locator('[name="Form.DeliveryTimeframe"]').fill('Two weeks');
    await page.locator('[name="Form.PaymentTerms"]').fill('30 days');
    await page.locator('[name="Form.TechnicalDetails"]').fill('All requested specifications are included.');
    await page.locator('[name="Form.OfferValidUntil"]').fill('2026-12-01');
    for (const field of await page.locator('[data-unit-price]').all()) await field.fill('1.234567');
    ok(await page.locator('[data-bid-total]').textContent() === '4.62962625', 'live item totals use precise decimal arithmetic');
    await page.locator('[data-add-charge]').click();
    await page.locator('[name="Form.Charges[0].CostType"]').fill('Tax');
    await page.locator('[name="Form.Charges[0].Amount"]').fill('0.1');
    ok(await page.locator('[data-bid-total]').textContent() === '4.72962625', 'additional cost included exactly');
    await page.locator('[data-add-charge]').click();
    await page.locator('[data-remove-charge]').last().click();
    ok(await page.locator('[data-charge]').count() === 1, 'dynamic costs add, remove and reindex');
    await Promise.all([page.waitForURL(/\/Bids\/Edit\//), page.locator('[data-save-draft]').click()]);
    const editUrl = page.url(); const id = editUrl.split('/').pop();
    ok(await page.locator('[name="Form.Charges[0].CostType"]').inputValue() === 'Tax', 'dynamic cost binds after real MVC save');
    let upload = page.locator('form[action*="/Upload/"]');
    await upload.locator('input[type=file]').setInputFiles({name: 'bad.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('invalid')});
    ok(await upload.locator('input[type=file]').evaluate(e => !e.validity.valid), 'extension feedback shown before upload');
    await upload.locator('input[type=file]').setInputFiles({name: 'big.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(5 * 1024 * 1024 + 1)});
    ok(await upload.locator('input[type=file]').evaluate(e => !e.validity.valid), '5 MB browser size check');
    const pdf = {name: 'proposal.pdf', mimeType: 'application/pdf', buffer: await (await context.request.get(base+'/UiPagePreview/Pdf')).body()};
    await upload.locator('input[type=file]').setInputFiles(pdf);
    await page.locator('[name="Form.DeliveryTimeframe"]').fill('Three weeks');
    await upload.getByRole('button', {name: 'Upload document'}).click();
    ok((await page.locator('[data-bid-form-status]').textContent()).includes('Save your offer changes first'), 'unsaved commercial changes block document navigation');
    await Promise.all([page.waitForResponse(r => r.request().method() === 'POST' && r.url().includes('/Bids/Edit/')), page.locator('[data-save-draft]').click()]);
    await page.waitForLoadState('networkidle');
    upload = page.locator('form[action*="/Upload/"]');
    await upload.locator('input[type=file]').setInputFiles(pdf);
    await Promise.all([page.waitForResponse(r => r.request().method() === 'POST' && r.url().includes('/Bids/Upload/')), upload.getByRole('button', {name: 'Upload document'}).click()]);
    await page.waitForLoadState('networkidle');
    ok(await page.getByRole('heading', {name: 'proposal.pdf'}).count() === 1, 'private document upload integrated');
    for (const width of [1440, 768, 390, 320]) {
      await page.setViewportSize({width, height: 1000});
      await page.waitForTimeout(450); // Let the existing responsive sidebar breakpoint transition settle.
      ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `document editor fits ${width}px`);
      if (width === 1440 || width === 390) await page.screenshot({path: path.join(output, `edit-${width}.png`), fullPage: true});
    }
    const [document] = await Promise.all([page.waitForEvent('download',{timeout:60000}),page.getByRole('link', {name: 'Download watermarked copy'}).click()]);
    ok(document.suggestedFilename() === 'proposal-watermarked.pdf' && !(await document.failure()), 'secure file downloads as attachment');
    await page.locator('[data-leave-editor]').click();
    const confirmation = page.frameLocator('dialog.workspace-popup iframe');
    await confirmation.locator('[data-bid-submit]').waitFor();
    ok(await confirmation.locator('[data-bid-submit]').count() === 1, 'real document verification enables popup confirmation');
    await confirmation.locator('#confirm-offer').check();
    await Promise.all([page.waitForURL(/\/Bids\/Details\//), confirmation.locator('[data-bid-submit] button').click()]);
    ok((await page.locator('.bid-status').textContent()).trim() === 'Submitted', 'final submission renders immutable details');
    ok(await page.getByRole('link', {name: 'Edit draft & documents'}).count() === 0, 'no editing controls on submitted offer');
    const summaryEvent = page.waitForEvent('download');
    await page.getByRole('link', {name: 'Download my summary'}).click();
    const summary = await summaryEvent;
    ok(summary.suggestedFilename().endsWith('.txt') && fs.readFileSync(await summary.path(), 'utf8').includes('4.72962625 JOD'), 'personal text summary matches server total');
    await page.emulateMedia({reducedMotion: 'reduce'});
    await page.goto(`${base}/Bids/Details/${id}`);
    ok(await page.evaluate(() => !document.getAnimations().some(a => a.playState === 'running')), 'reduced motion stops page animations');
    await page.goto(base + '/Bids');
    ok(await page.getByRole('link', {name: 'Edit draft', exact: true}).count() === 0, 'My Offers only exposes available submitted actions');
    ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'My Offers fits mobile');
    ok(errors.length === 0, 'no browser JavaScript errors');
    fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({checks, errors}, null, 2));
    console.log(`PASS: ${checks} Phase 3 browser checks`);
  } catch (error) {
    console.error('Browser errors:', errors);
    if (page) await page.screenshot({path: path.join(output, 'failure.png'), fullPage: true}).catch(() => {});
    throw error;
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exit(1); });
