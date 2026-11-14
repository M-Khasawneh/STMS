// Runs only against the marked database-free presentation fixture.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs');
const path = require('path');

(async () => {
    const [base, output] = process.argv.slice(2);
    if (!base || !output) throw Error('Usage: fixture-url external-output-directory');
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const errors = [];
    let checks = 0;
    const check = (valid, message) => {
        if (!valid) throw Error(message);
        checks++;
    };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext();
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        for (const actor of ['officer', 'supplier']) {
            await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': actor });
            for (const width of [1440, 768, 390, 320]) {
                await page.setViewportSize({ width, height: 1000 });
                const response = await page.goto(base + '/Compliance/Details/57b4b211-7e94-4bd2-8c91-13e314e95d8f');
                if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
                await page.waitForTimeout(300);
                check(response.status() === 200, 'Renders ' + actor + ' at ' + width);
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'No overflow ' + actor + ' at ' + width);
                check(await page.locator('main h1').innerText() === 'Compliance & clarifications', 'Accessible heading');
                if (width === 1440 || width === 390) {
                    await page.screenshot({ path: path.join(output, actor + '-' + width + '.png'), fullPage: true });
                }
            }
            if (actor === 'officer') {
                await page.locator('#outcome').selectOption('NonCompliant');
                check(await page.locator('#reasons').evaluate(input => input.required), 'Reason required for non-compliance');
                await page.locator('#outcome').selectOption('Compliant');
                check(!(await page.locator('#reasons').evaluate(input => input.required)), 'Reason optional for compliant result');
            } else {
                const input = page.locator('main input[type=file]');
                await input.setInputFiles({ name: 'invalid.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('invalid') });
                check(!(await input.evaluate(element => element.validity.valid)), 'Invalid file client feedback');
                await input.setInputFiles({ name: 'evidence.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4\n%%EOF') });
                check(await input.evaluate(element => element.validity.valid), 'Allowed file client feedback');
                await page.getByRole('textbox', { name: 'Your response' }).focus();
                check(await page.getByRole('textbox', { name: 'Your response' }).evaluate(element => document.activeElement === element), 'Keyboard focus works');
            }
        }
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.reload();
        check(await page.evaluate(() => !document.getAnimations().some(animation => animation.playState === 'running')), 'Reduced motion');
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' compliance browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
