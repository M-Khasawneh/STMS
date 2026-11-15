// Real Razor views and authorized report categories on the memory-only report fixture.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');
(async () => {
    const [base, output] = process.argv.slice(2);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0;
    const errors = [];
    const check = (ok, message) => { if (!ok) throw Error(message); checks++; };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ reducedMotion: 'reduce' });
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        async function visit(route) {
            const response = await page.goto(base + route);
            check(response.headers()['x-stms-bid-fixture'] === 'memory-only', 'Marked memory host');
            check(response.status() === 200, 'Authorized report renders: ' + route);
        }
        for (const actor of ['admin', 'requester', 'supplier', 'officer', 'manager', 'evaluator', 'top']) {
            await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': actor });
            await visit('/Reports');
            const categories = await page.locator('.report-tabs a').evaluateAll(links => links.map(link => link.getAttribute('href')));
            check(categories.length > 0, 'Existing categories for ' + actor);
            for (const width of [1440, 390]) {
                await page.setViewportSize({ width, height: 1000 });
                for (const route of categories) {
                    await visit(route);
                    check(await page.locator('.report-watermark').count() === 1, 'One print mark per report, not per section');
                    check(await page.locator('.report-watermark:visible').count() === 0, 'No screen watermark: ' + actor + route);
                    check(await page.locator('.report-sheet .report-watermark').count() === 0, 'Tables have no repeated overlay');
                    check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Responsive report');
                }
            }
            await page.emulateMedia({ media: 'print' });
            check(await page.locator('.report-watermark:visible').count() === 1, 'Printing retains one mark for ' + actor);
            check((await page.locator('.report-watermark').innerText()).includes(actor), 'Print identity belongs to current actor');
            await page.emulateMedia({ media: 'screen' });
            const printRoute = categories[0].replace('/Reports?', '/Reports/Print?');
            await visit(printRoute);
            check(await page.locator('.report-watermark:visible').count() === 1, 'Dedicated print preview preserves document protection');
        }
        const draft = '8bff4fe2-e45e-455d-a8ca-8f283c07f2bc';
        const submitted = '92dcc730-ebdd-48db-a5cc-5e1c9ee191ac';
        for (const [actor, route] of [
            ['evaluator', '/UnifiedReports/Details/' + draft], ['evaluator', '/UnifiedReports/Submit/' + draft],
            ['evaluator', '/UnifiedReports/Details/' + submitted],
            ['manager', '/UiPagePreview/Page?name=UnifiedReports%2FReview']
        ]) {
            await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': actor });
            await visit(route);
            check(await page.locator('.report-watermark:visible, .report-print-identification:visible').count() === 0, 'Review/editor screens have no watermark or identity banner');
            await page.emulateMedia({ media: 'print' });
            check(await page.locator('.report-watermark:visible').count() === 1, 'Unified print retains one mark');
            await page.emulateMedia({ media: 'screen' });
        }
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'admin' });
        await visit('/Reports');
        await page.screenshot({ path: path.join(output, 'reports-clean-mobile.png'), fullPage: true });
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' report screen/print watermark checks across seven roles.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
