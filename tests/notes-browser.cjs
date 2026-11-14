const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs');
const path = require('path');
(async () => {
    const [base, output] = process.argv.slice(2);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0;
    const errors = [];
    const check = (valid, message) => { if (!valid) throw Error(message); checks++; };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ extraHTTPHeaders: { 'X-Bid-Test-Actor': 'admin' } });
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const name of ['ComposeRequest', 'Record']) {
                const response = await page.goto(base + '/NotesPreview/' + name);
                if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
                await page.waitForTimeout(300);
                check(response.status() === 200, 'Razor preview renders');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), name + ' fits ' + width);
                check(await page.locator('.notification-count').innerText() === '3', 'Badge matches fixture recipient notifications');
                if (name === 'ComposeRequest') {
                    await page.getByRole('button', { name: 'Add item', exact: true }).click();
                    check(await page.locator('fieldset[data-requisition-item]').count() === 2, 'Dynamic item addition preserved');
                    check(await page.locator('form[data-requisition-form]').getAttribute('action') === '/Requisitions/Create', 'Real MVC form destination preserved');
                } else {
                    check(await page.locator('.requisition-history').first().evaluate(element => getComputedStyle(element).display === 'flex'), 'Straight horizontal history');
                }
                if (width === 1440 || width === 390) await page.screenshot({ path: path.join(output, name + '-' + width + '.png'), fullPage: true });
            }
        }
        await page.goto(base + '/NotesPreview/Success');
        check((await page.locator('.feedback-toast').innerText()).includes('saved successfully'), 'Successful save feedback is visible');
        await page.evaluate(() => window.workspaceFeedback.remember('Fixture popup saved successfully.'));
        await page.goto(base + '/NotesPreview/ComposeRequest');
        check((await page.locator('.feedback-toast').innerText()).includes('popup saved'), 'Successful popup feedback survives workspace reload');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.goto(base + '/NotesPreview/Success');
        check(await page.locator('.feedback-toast').evaluate(element => getComputedStyle(element).animationName === 'none'), 'Reduced motion feedback');
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'anonymous' });
        for (const width of [1440, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            const response = await page.goto(base + '/Account/Login');
            check(response.status() === 200, 'Login renders');
            check(await page.locator('img[src*="stms-logo"]').count() === 0, 'Login logo removed');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Login fits ' + width);
            if (width !== 320) await page.screenshot({ path: path.join(output, 'login-' + width + '.png'), fullPage: true });
        }
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' Word notes browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
