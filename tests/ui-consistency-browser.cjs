// Compiled MVC presentation with synthetic in-memory records, never SQL or production mutations.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');
(async () => {
    const [base, output] = process.argv.slice(2);
    const screenshots = process.env.STMS_UI_SCREENSHOTS !== '0';
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const errors = [], findings = [], contrast = [], bright = [];
    let checks = 0;
    const check = (ok, issue, context) => { checks++; if (!ok) findings.push({ issue, ...context }); };
    try {
        const context = await browser.newContext({ reducedMotion: 'reduce', colorScheme: 'light' });
        const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
        // Block modifications even on this marked test host. This is a visual/interaction review only.
        await context.route('**/*', route => route.request().method() === 'POST' ? route.abort() : route.continue());
        const visit = async (actor, route, theme, width) => {
            await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': actor, 'X-Admin-Test-State': 'populated' });
            await page.setViewportSize({ width, height: 1000 });
            const response = await page.goto(base + route);
            if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing unmarked fixture');
            await page.evaluate(theme => { localStorage.setItem('stms-appearance', theme); window.dispatchEvent(new StorageEvent('storage', { key: 'stms-appearance', newValue: theme })); }, theme);
            check(response.status() === 200, 'Route renders', { actor, route, theme, width });
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'No horizontal overflow', { actor, route, theme, width });
        };
        async function audit(actor, route, theme) {
            const result = await page.evaluate(() => {
                const rgb = value => value.match(/[\d.]+/g)?.map(Number);
                const visible = e => e.getBoundingClientRect().width > 0 && e.getBoundingClientRect().height > 0 && !e.closest('[aria-hidden=true]');
                const luminance = color => color.slice(0, 3).map(v => { v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4; }).reduce((sum, v, i) => sum + v * [.2126, .7152, .0722][i], 0);
                function background(element) {
                    const chain = []; let current = element;
                    while (current) { const s = getComputedStyle(current); if (s.backgroundImage !== 'none') return null; chain.unshift(rgb(s.backgroundColor) || [0, 0, 0, 0]); current = current.parentElement; }
                    return chain.reduce((back, front) => { const a = front.length > 3 ? front[3] : 1; return front.slice(0, 3).map((v, i) => v * a + back[i] * (1 - a)); }, [255, 255, 255]);
                }
                const failures = [];
                for (const e of document.querySelectorAll('main :is(p,small,label,h1,h2,h3,a,button,span,dt,dd,td,th,time,summary),.topbar :is(button,summary),[data-dashboard-search] :is(p,summary,label,h2,button)')) {
                    if (!visible(e) || !e.textContent.trim() || e.matches(':disabled') || e.closest('svg') || !e.firstChild) continue;
                    // Inspect leaf text controls, not containers whose children have their own text styling.
                    if (e.querySelector('p,h1,h2,h3,label,button,a,strong')) continue;
                    const s = getComputedStyle(e), fg = rgb(s.color), bg = background(e);
                    if (!fg || !bg || +s.opacity < .95 || (fg.length > 3 && fg[3] < .95)) continue;
                    const a = luminance(fg), b = luminance(bg), ratio = (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
                    const large = parseFloat(s.fontSize) >= 24 || parseFloat(s.fontSize) >= 18.66 && +s.fontWeight >= 700;
                    if (ratio + .05 < (large ? 3 : 4.5)) failures.push({ tag: e.tagName, class: e.className, text: e.textContent.trim().slice(0, 55), ratio: +ratio.toFixed(2), fg: s.color, bg });
                }
                const brightPanels = [...document.querySelectorAll('main *')].filter(e => {
                    const r = e.getBoundingClientRect(), bg = rgb(getComputedStyle(e).backgroundColor);
                    return visible(e) && r.width > 150 && r.height > 36 && bg?.slice(0, 3).every(v => v > 220) && (bg.length < 4 || bg[3] > .8);
                }).map(e => e.className);
                return { failures, brightPanels };
            });
            contrast.push(...result.failures.map(item => ({ actor, route, theme, ...item })));
            if (theme === 'dark') bright.push(...result.brightPanels.map(item => ({ actor, route, class: item })));
        }
        for (const theme of ['light', 'dark']) {
            for (const actor of ['admin', 'supplier', 'requester', 'officer', 'manager', 'evaluator', 'top']) {
                for (const width of [1440, 768, 390, 320]) {
                    await visit(actor, '/Workspace', theme, width);
                    const inset = await page.locator('main').evaluate(e => getComputedStyle(e).paddingLeft);
                    check(inset === (width <= 600 ? '16px' : width <= 1100 ? '24px' : '32px'), 'Shared page inset', { actor, theme, width, inset });
                    if (width === 1440) await audit(actor, '/Workspace', theme);
                    if (screenshots && (width === 1440 || width === 390)) await page.screenshot({ path: path.join(output, `${actor}-${theme}-${width}.png`), fullPage: true });
                }
            }
            for (const [actor, route] of [
                ['requester', '/UiPagePreview/Page?name=Requisitions/Create'], ['requester', '/UiPagePreview/Page?name=Requisitions/Details'],
                ['officer', '/UiPagePreview/Page?name=Tenders/Index'], ['officer', '/UiPagePreview/Page?name=Tenders/Criteria'],
                ['requester', '/UiPagePreview/Page?name=Profile/Index'], ['admin', '/UiPagePreview/Page?name=Suppliers/Details'],
                ['officer', '/Work/Tasks'], ['officer', '/Work/Search?q=network'], ['admin', '/Reports'],
                ['anonymous', '/'], ['anonymous', '/Home/About'], ['anonymous', '/Home/Contact'], ['anonymous', '/Account/Login'], ['anonymous', '/Suppliers/Register']
            ]) {
                for (const width of [1440, 390, 320]) {
                    await visit(actor, route, theme, width);
                    if (width === 1440) await audit(actor, route, theme);
                    if (screenshots && (width === 390 || width === 1440)) await page.screenshot({ path: path.join(output, `${actor}-${route.split('?')[1]?.split('=')[1]?.replaceAll('/', '-') || route.split('/').at(-1) || 'home'}-${theme}-${width}.png`), fullPage: true });
                }
            }
            for (const width of [1440, 768, 390, 320]) {
                await visit('officer', '/Workspace', theme, width);
                await page.locator('[data-work-search-toggle]').click();
                const panel = page.locator('[data-dashboard-search]');
                const bounds = await panel.boundingBox();
                check(bounds && bounds.x >= 0 && bounds.x + bounds.width <= width + 1, 'Search stays in viewport', { theme, width, bounds });
                check(bounds?.height < 570, 'Search is compact', { theme, width, bounds });
                check(await page.locator('.dashboard-search-help').evaluate(e => !e.open), 'Voice help is initially collapsed', { theme, width });
                check(await page.locator('[data-work-query]').evaluate(e => e === document.activeElement), 'Search receives keyboard focus', { theme, width });
                if (width === 1440) await audit('officer', '/Workspace + search', theme);
                if (screenshots) await page.screenshot({ path: path.join(output, `search-${theme}-${width}.png`) });
                await page.keyboard.press('Escape'); check(await panel.isHidden(), 'Escape closes search', { theme, width });
            }
        }
        check(contrast.length === 0, 'Readable solid-surface text contrast', { count: contrast.length });
        check(bright.length === 0, 'No bright panels in dark mode', { count: bright.length });
        check(errors.length === 0, 'No JavaScript errors', { errors });
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, findings, contrast, bright, errors }, null, 2));
        console.log(JSON.stringify({ checks, findings, lowContrast: contrast.length, brightPanels: bright.length, errors }));
        if (findings.length) process.exitCode = 1;
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
