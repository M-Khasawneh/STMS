const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');
const { settled } = require('./browser-waits.cjs');
(async () => {
    const [base, output] = process.argv.slice(2);
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0; const errors = [];
    const check = (ok, message) => { if (!ok) throw Error(message); checks++; };
    try {
        fs.mkdirSync(output, { recursive: true });
        const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, extraHTTPHeaders: { 'X-Bid-Test-Actor': 'anonymous' } });
        const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
        for (const route of ['/', '/Home/About', '/Home/Contact', '/Account/Login', '/Suppliers/Register']) {
            const response = await page.goto(base + route);
            if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-fixture host');
            await settled(page);
            check(response.status() === 200, 'Public route renders ' + route);
            check(await page.locator('.sticky-footer-surface').evaluate(element => getComputedStyle(element).position) === (route === '/' ? 'fixed' : 'relative'), 'Sticky reveal is homepage-only');
            check(await page.locator('.sticky-footer-surface').evaluate(element => (() => {
                const style = getComputedStyle(element);
                const stops = [...style.backgroundImage.matchAll(/rgba?\(([^)]+)\)/g)].map(m => m[1].split(',').slice(0,3).map(Number));
                if (!stops.length) stops.push(style.backgroundColor.match(/[\d.]+/g)?.map(Number) || []);
                return stops.length > 0 && stops.every(([r,g,b]) => b > g && g > r && Math.max(r,g,b) < 90);
            })()), 'Footer uses dark navy surface');
            check(await page.locator('.public-footer a[href="#"]').count() === 0, 'No reference placeholder destinations');
            await page.evaluate(() => {
                const footer = document.querySelector('[data-sticky-footer]');
                scrollTo({top: footer.getBoundingClientRect().top + scrollY - innerHeight + footer.clientHeight * .4, behavior:'instant'});
            });
            await settled(page);
            const first = await page.locator('.sticky-footer-surface').boundingBox();
            await page.evaluate(() => scrollBy({top:60,behavior:'instant'})); await settled(page);
            const next = await page.locator('.sticky-footer-surface').boundingBox();
            if (route === '/') check(Math.abs(first.y - next.y) < 2, 'Homepage surface stays fixed while the page reveals it');
            await page.evaluate(() => scrollTo({top:document.documentElement.scrollHeight,behavior:'instant'}));
            await page.waitForFunction(() => {
                const e = document.querySelector('.sticky-footer-brand'), r = e.getBoundingClientRect();
                return !!document.elementFromPoint(r.x+r.width/2,r.y+r.height/2)?.closest('[data-sticky-footer]');
            });
            await settled(page);
            check(await page.locator('.sticky-footer-brand').evaluate(link => {
                const r = link.getBoundingClientRect(); return !!document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2)?.closest('[data-sticky-footer]');
            }), 'Fully revealed footer links receive pointer input');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'No desktop overflow');
            if (route === '/') await page.locator('.public-footer').screenshot({ path: path.join(output, 'footer-desktop.png') });
        }
        await page.goto(base + '/');
        await page.locator('main a').last().focus();
        await page.keyboard.press('Tab');
        await page.waitForFunction(() => {
            const r = document.activeElement.getBoundingClientRect();
            return r.top >= 0 && r.bottom <= innerHeight;
        });
        await settled(page);
        check(await page.locator('.public-footer').evaluate(footer => footer.contains(document.activeElement) && footer.classList.contains('footer-keyboard-flow')), 'Keyboard can enter clipped footer safely');
        check(await page.evaluate(() => { const r = document.activeElement.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight; }), 'Focused footer link is brought into view');
        await page.emulateMedia({ reducedMotion: 'reduce' }); await settled(page);
        check(await page.locator('.sticky-footer-surface').evaluate(element => getComputedStyle(element).position === 'relative'), 'Reduced motion restores natural flow');
        check(await page.locator('.public-footer').evaluate(element => element.getAnimations({ subtree: true }).every(animation => animation.playState !== 'running')), 'Reduced motion cancels footer animation');
        await page.emulateMedia({ reducedMotion: 'no-preference' });
        for (const size of [{ width: 1440, height: 500 }, { width: 768, height: 900 }, { width: 390, height: 844 }, { width: 320, height: 700 }]) {
            await page.setViewportSize(size); await page.goto(base + '/'); await settled(page);
            check(await page.locator('.sticky-footer-surface').evaluate(element => getComputedStyle(element).position === 'relative'), 'Small/short viewport keeps accessible flow');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Responsive footer fits ' + size.width);
            await page.locator('.public-footer a').last().scrollIntoViewIfNeeded();
            check(await page.locator('.public-footer a').last().isVisible(), 'Last footer link is reachable');
            if (size.width === 390) await page.locator('.public-footer').screenshot({ path: path.join(output, 'footer-mobile.png') });
        }
        const nojs = await browser.newContext({ javaScriptEnabled: false, viewport: { width: 1440, height: 1000 }, extraHTTPHeaders: { 'X-Bid-Test-Actor': 'anonymous' } });
        const plain = await nojs.newPage(); await plain.goto(base + '/');
        check(await plain.locator('.sticky-footer-surface').evaluate(element => getComputedStyle(element).position === 'relative'), 'No-JavaScript footer remains in normal document flow');
        check(await plain.locator('.public-footer a[href="/Home/Contact"]').count() > 0, 'No-JavaScript Contact destination remains functional');
        check(errors.length === 0, 'Zero JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' native sticky footer scroll/accessibility checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
