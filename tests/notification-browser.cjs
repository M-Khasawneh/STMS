// Marked in-memory fixture only; no SQL writes or real user inboxes.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');
(async () => {
    const [base, output] = process.argv.slice(2);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0; const errors = [];
    const check = (ok, message) => { if (!ok) throw Error(message); checks++; };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ extraHTTPHeaders: { 'X-Bid-Test-Actor': 'admin' } });
        const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
        for (const width of [1440, 1024, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            const response = await page.goto(base + '/Workspace');
            if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
            await page.waitForTimeout(700);
            check(response.status() === 200, 'Admin dashboard renders');
            check((await page.locator('[data-metric=message] [data-metric-value]').innerText()).trim() === '2', 'Dashboard message count reflects fixture data');
            check(await page.locator('[data-metric=message]').getAttribute('href') === '/Messages', 'Message KPI links to inbox');
            check(await page.locator('.sidebar a[href="/Messages"]').count() === 1, 'Admin sidebar has Messages');
            const original = page.url();
            await page.locator('.notification-dropdown > summary').click();
            check(page.url() === original, 'Bell opens in place without navigation');
            check(await page.locator('.notification-menu-list > li').count() === 3, 'Own notifications are stacked');
            const cards = await page.locator('.notification-menu-list > li').all();
            const a = await cards[0].boundingBox(), b = await cards[1].boundingBox();
            check(b.y > a.y, 'Notifications form a vertical list');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Dropdown fits ' + width);
            const popover = await page.locator('.notification-popover').boundingBox();
            check(popover.x >= 0 && popover.x + popover.width <= width, 'Popover remains inside viewport');
            if (width === 1440 || width === 390) await page.screenshot({ path: path.join(output, 'notifications-' + width + '.png'), fullPage: true });
            await page.keyboard.press('Escape');
            check(!await page.locator('.notification-dropdown').evaluate(element => element.open) && await page.locator('.notification-dropdown > summary').evaluate(element => element === document.activeElement), 'Escape closes and restores focus');
        }
        const before = page.url();
        await page.locator('.notification-dropdown > summary').click();
        await page.locator('[data-notification-read] button').first().click();
        await page.waitForFunction(() => document.querySelector('.notification-count').textContent === '2');
        check(page.url() === before && await page.locator('.notification-dropdown').evaluate(element => element.open), 'Read update stays in open dropdown without navigation');
        check(await page.locator('.notification-menu-list .is-read').count() === 1, 'Read state visibly updates');
        await page.locator('.notification-menu-list').getByRole('button', { name: 'Read message' }).first().click();
        await page.waitForURL('**/Messages/Details/*');
        check((await page.locator('.contact-message-body').innerText()).includes('Fixture visitor'), 'Admin can read full Contact message');
        await page.goto(base + '/Messages');
        check((await page.locator('main').innerText()).includes('1 unread · 2 total'), 'Message list reflects persisted memory read state');
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Message inbox fits mobile');
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'supplier' });
        await page.goto(base + '/Workspace');
        check(await page.locator('.sidebar a[href="/Messages"]').count() === 0, 'Supplier has no Admin messages navigation');
        await page.locator('.notification-dropdown > summary').click();
        check((await page.locator('.notification-menu-list').innerText()).includes('Private supplier notice') && !(await page.locator('.notification-menu-list').innerText()).includes('Fixture question'), 'Supplier sees only their own notifications');
        await page.locator('[data-notification-read] button').click();
        await page.waitForFunction(() => document.querySelector('.notification-count').hidden);
        check(await page.locator('.notification-menu-heading').innerText().then(text => text.includes('0 unread')), 'Last read clears unread badge and updates count');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' messages/dashboard/notification dropdown browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
