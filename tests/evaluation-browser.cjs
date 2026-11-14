// Real compiled MVC views with a marked presentation-only fixture. No SQL writes.
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
        const context = await browser.newContext({ extraHTTPHeaders: { 'X-Bid-Test-Actor': 'manager' } });
        const page = await context.newPage();
        page.on('pageerror', error => errors.push(error.message));
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const action of ['Details', 'Confirm']) {
                const response = await page.goto(base + '/Evaluations/' + action + '/0b83d4af-c784-4da1-a818-a2c59240578d');
                if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
                await page.waitForTimeout(300);
                check(response.status() === 200, 'Evaluation page renders');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'No page overflow at ' + width);
                if (width === 1440 || width === 390) await page.screenshot({ path: path.join(output, action.toLowerCase() + '-' + width + '.png'), fullPage: true });
                if (action === 'Details') {
                    const score = page.locator('main input[type=number]').first();
                    await score.fill('101');
                    check(!(await score.evaluate(input => input.checkValidity())), 'Score maximum accessible feedback');
                    await score.fill('80');
                    await score.focus();
                    check(await score.evaluate(input => document.activeElement === input), 'Keyboard focus works');
                } else {
                    check(await page.locator('main input[type=checkbox]').evaluate(input => input.required), 'Explicit final confirmation required');
                }
            }
        }
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'officer' });
        await page.goto(base + '/Evaluations/Details/0b83d4af-c784-4da1-a818-a2c59240578d');
        check(await page.locator('main input[type=number]').count() === 0, 'Officer screen is read-only');
        check((await page.locator('tfoot').innerText()).includes('80'), 'Completed weighted result renders');
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Completed result table fits mobile page');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.reload();
        check(await page.evaluate(() => !document.getAnimations().some(animation => animation.playState === 'running')), 'Reduced motion');
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' evaluation browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
