// Marked presentation fixture only; abort every POST. Selection feedback never uploads files.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');
(async () => {
    const [base, output] = process.argv.slice(2);
    fs.mkdirSync(output, { recursive: true });
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    let checks = 0, posts = 0; const errors = [];
    const check = (ok, message) => { if (!ok) throw Error(message); checks++; };
    try {
        const context = await browser.newContext({ reducedMotion: 'reduce', extraHTTPHeaders: { 'X-Bid-Test-Actor': 'officer' } });
        await context.route('**/*', route => { if (route.request().method() === 'POST') { posts++; return route.abort(); } return route.continue(); });
        const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
        const states = ['Draft', 'Published', 'Open', 'Closed', 'UnderEvaluation', 'AwaitingManagerReview', 'AwaitingCommitteeDecision', 'Awarded', 'Cancelled'];
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const state of states) {
                const response = await page.goto(base + '/UiPagePreview/Page?name=Tenders%2FDetails&state=' + state);
                check(response.headers()['x-stms-bid-fixture'] === 'memory-only' && response.status() === 200, 'Marked page renders');
                check(await page.locator('.tender-progress [aria-current=step]').count() === 1, 'Exactly one saved current state');
                check(await page.locator('.tender-progress [data-tender-state="' + state + '"]').getAttribute('aria-current') === 'step', 'Marker matches saved state ' + state);
                check(await page.locator('.tender-progress button,.tender-progress input,[data-phase][role=button]').count() === 0, 'Progress does not provide workflow actions');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Responsive guidance at ' + width);
            }
        }
        const navigation = page.getByRole('navigation', { name: 'On this page', exact: true });
        check(await navigation.count() === 1, 'Single section navigator for visible headings');
        const link = navigation.getByRole('link', { name: 'Requested items', exact: true });
        const target = await link.getAttribute('href'); await link.click();
        await page.waitForFunction(id => document.activeElement.id === id, target.slice(1));
        check(new URL(page.url()).hash === target, 'Native anchor/history navigation retained');
        check(await page.locator('dialog[open]').count() === 0, 'Section links do not open edit dialogs');
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.goto(base + '/UiPagePreview/Page?name=Tenders%2FDetails&state=UnderEvaluation');
        await page.screenshot({ path: path.join(output, 'tender-guidance-desktop.png'), fullPage: true });
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'admin' });
        await page.goto(base + '/UiPagePreview/Page?name=Tenders%2FDetails&state=Published');
        check(await page.locator('main form[method=post]').count() === 0, 'Admin details remain read-only');
        await page.goto(base + '/UiPagePreview/Page?name=Profile%2FIndex');
        check(await page.locator('main').getAttribute('data-record-navigation') === 'false' && await page.locator('.record-section-nav').count() === 0, 'Pages with unrelated headings do not acquire a record outline');
        await page.goto(base + '/Suppliers/Register');
        const fileFields = await page.locator('main input[type=file]').count();
        check(fileFields === 3 && await page.locator('.upload-selection').count() === fileFields, 'Authenticated registration gets one summary per input');
        await page.addScriptTag({ url: base + '/js/record-guidance.js' });
        check(await page.locator('.upload-selection').count() === fileFields, 'Repeated enhancement initialization does not duplicate controls');
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'supplier' });
        await page.goto(base + '/UiPagePreview/Page?name=Tenders%2FDetails&state=Published');
        check(await page.getByRole('link', { name: 'Criteria & approval', exact: true }).count() === 0 && await page.getByRole('link', { name: 'Supplier invitations', exact: true }).count() === 0, 'Supplier navigation does not acquire internal links');
        await context.setExtraHTTPHeaders({ 'X-Bid-Test-Actor': 'anonymous' });
        const registration = await page.goto(base + '/Suppliers/Register');
        check(registration.status() === 200 && registration.headers()['x-stms-bid-fixture'] === 'memory-only', 'Registration fixture renders');
        const input = page.locator('[data-supplier-upload]').first();
        const accept = await input.getAttribute('accept');
        await input.setInputFiles({ name: '<img src=x onerror=alert(1)>.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(2048, 65) });
        const summary = input.locator('xpath=following-sibling::div[contains(@class,"upload-selection")]');
        check((await summary.innerText()).includes('<img src=x onerror=alert(1)>.pdf') && (await summary.innerText()).includes('2.0 KB'), 'Actual selected filename and byte size displayed');
        check(await summary.locator('img,script,iframe').count() === 0, 'Filename is never executable markup');
        check((await summary.innerText()).includes('nothing has been uploaded'), 'Selection is not claimed as an upload');
        await summary.getByRole('button', { name: /^Clear selection/ }).click();
        check(await input.evaluate(element => element.files.length === 0) && await summary.isHidden(), 'Clear affects only local selection');
        check(await input.getAttribute('accept') === accept && await input.getAttribute('name') === 'CompanyCertificate', 'Formats and original MVC field binding preserved');
        await input.setInputFiles({ name: 'legal.pdf', mimeType: 'application/pdf', buffer: Buffer.alloc(2048, 65) });
        await page.setViewportSize({ width: 320, height: 1000 });
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Selection feedback fits mobile');
        check(posts === 0, 'No automatic file uploads or modifying requests');
        await context.close();
        const plain = await browser.newContext({ javaScriptEnabled: false, extraHTTPHeaders: { 'X-Bid-Test-Actor': 'officer' } });
        const nojs = await plain.newPage(); await nojs.goto(base + '/UiPagePreview/Page?name=Tenders%2FDetails&state=Open');
        check(await nojs.locator('.tender-progress [data-tender-state=Open]').getAttribute('aria-current') === 'step', 'Saved status renders without JavaScript');
        check(await nojs.locator('.record-section-nav').count() === 0 && await nojs.locator('main h2').count() >= 3, 'Without JavaScript all sections remain available');
        await plain.close();
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, posts, errors, mode: 'presentation-only' }, null, 2));
        console.log('PASS: ' + checks + ' tender-state, record navigation and local file-selection checks.');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
