// Only targets the marked memory fixture. No SQL or production account mutations.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs');
const path = require('path');

(async () => {
    const [base, bidId, tenderId, output] = process.argv.slice(2);
    if (!base || !bidId || !tenderId || !output) {
        throw Error('Usage: fixture-url bid-id tender-id external-output-directory');
    }
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let count = 0;
    const errors = [];
    const check = (valid, message) => {
        if (!valid) throw Error(message);
        count++;
        console.log('PASS ' + message);
    };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ extraHTTPHeaders: { 'X-Bid-Test-Actor': 'officer' } });
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        const first = await page.goto(base + '/ReviewBids');
        if (first.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
        const routes = {
            list: '/ReviewBids',
            details: '/ReviewBids/Details/' + bidId,
            compare: '/ReviewBids/Compare?tenderId=' + tenderId,
            history: '/ReviewBids/History/' + bidId
        };
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const [name, route] of Object.entries(routes)) {
                const response = await page.goto(base + route);
                await page.waitForTimeout(500);
                check(response.status() === 200, name + ' renders at ' + width);
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), name + ' fits at ' + width);
                check(await page.locator('main form').count() === 0, name + ' remains read-only');
                if (name === 'compare') {
                    check((await page.locator('main').textContent()).includes('Temporary supplier company'), 'comparison identifies supplier');
                    if (width === 1440 || width === 390) {
                        await page.screenshot({ path: path.join(output, 'comparison-' + width + '.png'), fullPage: true });
                    }
                }
            }
        }
        await page.goto(base + routes.details);
        const [downloaded] = await Promise.all([page.waitForEvent('download',{timeout:60000}),page.getByRole('link', { name: /Download watermarked copy/ }).click()]);
        check(!(await downloaded.failure()), 'authorized private download');
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'supplier' });
        await page.goto(base + '/Bids/Details/' + bidId);
        await page.getByRole('link', { name: 'Activity history' }).click();
        check((await page.locator('main').innerText()).includes('Offer submitted'), 'supplier sees recorded activity');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.goto(base + '/Bids/History/' + bidId);
        check(await page.evaluate(() => !document.getAnimations().some(animation => animation.playState === 'running')), 'reduced motion');
        check(errors.length === 0, 'zero JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks: count, errors }, null, 2));
        console.log('PASS ' + count + ' Phase 4 browser checks');
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exit(1); });
