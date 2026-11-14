// Marked memory fixture only: no production contact submissions or SQL writes.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');
(async () => {
    const [base, output] = process.argv.slice(2);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0; const errors = [];
    const check = (ok, message) => { if (!ok) throw Error(message); checks++; };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ extraHTTPHeaders: { 'X-Bid-Test-Actor': 'anonymous' } });
        const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
        const routes = ['/', '/Home/About', '/Home/Contact', '/Account/Login', '/Suppliers/Register'];
        for (const width of [1440, 1024, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            let baseline;
            for (const route of routes) {
                const response = await page.goto(base + route);
                if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture server');
                await page.waitForTimeout(900);
                check(response.status() === 200, route + ' renders at ' + width);
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), route + ' fits ' + width);
                check(await page.locator('#home-navigation > a').count() === 6, 'All required navigation destinations');
                check(JSON.stringify((await page.locator('#home-navigation > a').allTextContents()).slice(0, 4).map(x => x.trim())) === JSON.stringify(['Home', 'How It Works', 'About', 'Contact']), 'Public navigation order');
                if (route === '/Home/About') {
                    check(await page.locator('.public-purpose-art, .purpose-mark, .about-page img').count() === 0, 'About content contains no decorative logo');
                    check(await page.locator('.public-lifecycle-list > li').count() === 6, 'All six sourcing stages retained');
                }
                const shell = await page.locator('.public-topbar').evaluate(header => {
                    const style = getComputedStyle(header), login = getComputedStyle(header.querySelector('.public-login-link'));
                    return [Math.round(header.getBoundingClientRect().height), style.backgroundColor, style.borderRadius, login.backgroundColor, login.color, login.fontSize];
                });
                baseline ??= shell;
                check(JSON.stringify(shell) === JSON.stringify(baseline), 'Navbar is identical on ' + route + ' at ' + width);
                check(await page.locator('.public-footer').count() === 1, 'Shared modern footer');
                check(await page.locator('a[href="/Home/Privacy"]').count() === (route === '/' ? 1 : 0), 'Privacy remains homepage-only');
                if (width <= 1024) {
                    const menu = page.locator('.home-menu-toggle');
                    await menu.click();
                    check(await menu.getAttribute('aria-expanded') === 'true' && await page.locator('.public-login-link').isVisible(), 'Mobile login stays visible in open menu');
                    check(await page.locator('.public-login-link').evaluate(link => getComputedStyle(link).color === 'rgb(255, 255, 255)'), 'Mobile login contrast');
                    await page.keyboard.press('Escape');
                    check(await menu.getAttribute('aria-expanded') === 'false' && await menu.evaluate(button => document.activeElement === button), 'Escape closes menu and restores focus');
                }
                if (route === '/Account/Login') {
                    check(await page.locator('.login-story').count() === 0, 'Removed login side section stays removed');
                    await page.getByLabel('Password', { exact: true }).fill('visual-only');
                    await page.getByRole('button', { name: 'Show', exact: true }).click();
                    check(await page.locator('#Password').getAttribute('type') === 'text', 'Accessible password reveal');
                    await page.getByRole('button', { name: 'Hide', exact: true }).click();
                    check(await page.locator('#Password').getAttribute('type') === 'password', 'Password can be concealed');
                }
                if ((width === 1440 || width === 390) && ['/', '/Home/About', '/Home/Contact', '/Account/Login'].includes(route)) await page.screenshot({ path: path.join(output, (route === '/' ? 'home' : route.split('/').at(-1)) + '-' + width + '.png'), fullPage: true });
            }
        }
        await page.goto(base + '/Home/Contact');
        await page.locator('[name=SenderName]').fill('Fixture visitor');
        await page.locator('[name=Email]').fill('visitor@example.test');
        await page.locator('[name=Subject]').fill('Fixture contact enquiry');
        await page.locator('[name=Message]').fill('A memory-only enquiry for the administrator.');
        await Promise.all([page.waitForNavigation(), page.getByRole('button', { name: 'Send message' }).click()]);
        check((await page.locator('.alert-success').innerText()).includes('sent to the STMS administrator'), 'Real MVC contact POST shows delivery confirmation');
        check(await page.locator('[name=Message]').inputValue() === '', 'Successful PRG clears message and prevents refresh resubmission');
        await page.emulateMedia({ reducedMotion: 'reduce' });
        await page.goto(base + '/Account/Login');
        check(await page.locator('.public-topbar').evaluate(element => getComputedStyle(element).transitionDuration === '0s'), 'Navbar respects reduced motion');
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' public navbar/footer/login/contact browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
