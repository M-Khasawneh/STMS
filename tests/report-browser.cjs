// Rendered MVC report pages on the marked database-free presentation fixture.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs');
const path = require('path');

(async () => {
    const [base, output] = process.argv.slice(2);
    if (!base || !output) throw Error('Usage: fixture-url external-output-directory');
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0;
    const errors = [];
    const check = (valid, message) => { if (!valid) throw Error(message); checks++; };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ extraHTTPHeaders: { 'X-Bid-Test-Actor': 'evaluator' } });
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        const routes = {
            draft: '/UnifiedReports/Details/8bff4fe2-e45e-455d-a8ca-8f283c07f2bc',
            confirm: '/UnifiedReports/Submit/8bff4fe2-e45e-455d-a8ca-8f283c07f2bc',
            submitted: '/UnifiedReports/Details/92dcc730-ebdd-48db-a5cc-5e1c9ee191ac'
        };
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const [name, route] of Object.entries(routes)) {
                const response = await page.goto(base + route);
                if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
                await page.waitForTimeout(300);
                check(response.status() === 200, name + ' renders');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), name + ' fits at ' + width);
                if (name === 'draft') {
                    check((await page.locator('main').innerText()).includes('Fixture excluded supplier'), 'Excluded offer is documented');
                    await page.getByLabel('Substantiated recommendation', { exact: true }).focus();
                    check(await page.getByLabel('Substantiated recommendation', { exact: true }).evaluate(input => document.activeElement === input), 'Keyboard focus on report form');
                    if (width <= 390) {
                        check(await page.getByRole('region', { name: 'Unified supplier comparison' }).evaluate(element => element.scrollWidth > element.clientWidth), 'Comparison scrolls inside its accessible region');
                        check((await page.locator('.report-comparison th').first().boundingBox()).width >= 160, 'Supplier column remains readable');
                    }
                }
                if (name === 'submitted') check(await page.locator('main textarea').count() === 0, 'Submitted report is read-only');
                if (name === 'confirm') check(await page.locator('main input[type=checkbox]').evaluate(input => input.required), 'Explicit submission confirmation');
                if (width === 1440 || width === 390) await page.screenshot({ path: path.join(output, name + '-' + width + '.png'), fullPage: true });
            }
        }
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'manager' });
        await page.goto(base + routes.draft);
        check(await page.locator('main textarea').count() === 0, 'Manager cannot edit report');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.reload();
        check(await page.evaluate(() => !document.getAnimations().some(animation => animation.playState === 'running')), 'Reduced motion');
        check(errors.length === 0, 'Zero JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' unified report browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
