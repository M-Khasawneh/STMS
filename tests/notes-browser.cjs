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
                    if (width <= 390) check(await page.locator('[data-requisition-item] input:not([type=hidden])').first().evaluate(input => input.getBoundingClientRect().width >= 140), 'Mobile item descriptions remain usable');
                } else {
                    check(await page.locator('.requisition-history').first().evaluate(element => getComputedStyle(element).display === 'flex'), 'Straight horizontal history');
                    check(await page.locator('[data-request-state]').count() === 7, 'Every documented request state is visible');
                    check(await page.locator('[data-request-state][aria-current=step]').count() === 1, 'One actual current state');
                    check(await page.locator('[data-request-state=Approved]').getAttribute('aria-current') === 'step', 'Approved state matches fixture data');
                    check(await page.locator('.status-track.is-current').count() === 0 && await page.locator('.is-current .status-track-dot').evaluate(dot => getComputedStyle(dot).boxShadow !== 'none'), 'Current marker has visible lighting');
                    check(await page.locator('.table thead th').first().evaluate(cell => getComputedStyle(cell).backgroundColor === 'rgb(21, 43, 64)'), 'Shared table heading uses navy identity');
                }
                if (width === 1440 || width === 390) await page.screenshot({ path: path.join(output, name + '-' + width + '.png'), fullPage: true });
            }
        }
        for (const state of ['Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Returned', 'ConvertedToTender']) {
            await page.goto(base + '/NotesPreview/Record?status=' + state);
            check(await page.locator('[data-request-state][aria-current=step]').count() === 1 && await page.locator('[data-request-state=' + state + ']').getAttribute('aria-current') === 'step', 'Current marker matches ' + state);
        }
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const route of ['/Employees/Create', '/Departments/Create']) {
                const response = await page.goto(base + route);
                check(response.status() === 200, route + ' renders');
                check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), route + ' fits ' + width);
                check(await page.locator('form input[name=__RequestVerificationToken]').count() > 0, 'Original antiforgery control remains');
                check(await page.locator('fieldset.form-section').count() > 0, 'Sectioned form presentation');
                if (route.includes('Employees')) {
                    check(await page.locator('select[name=Roles]').count() === 1 && await page.locator('input[name=DepartmentId],select[name=DepartmentId]').count() === 1, 'Role and department bindings preserved');
                    await page.locator('.role-picker summary').click();
                    await page.locator('[data-role-option]').nth(0).check();
                    await page.locator('[data-role-option]').nth(1).check();
                    check(await page.locator('select[name=Roles]').evaluate(select => select.selectedOptions.length === 2), 'Multi-role picker still synchronizes original MVC field');
                    await page.keyboard.press('Escape');
                }
                if (width === 1440 || width === 390) await page.screenshot({ path: path.join(output, route.split('/')[1] + '-' + width + '.png'), fullPage: true });
            }
        }
        await page.goto(base + '/Employees');
        await page.getByRole('link', { name: 'Create employee', exact: true }).click();
        await page.locator('.workspace-popup[open]').waitFor();
        const editor = page.frameLocator('iframe[data-workspace-popup]');
        await editor.locator('fieldset.form-section').first().waitFor();
        check(await editor.locator('fieldset.form-section').count() === 3, 'Employee redesign works inside existing popup');
        check(await editor.locator('body').evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Popup form has no page overflow');
        await page.locator('.workspace-popup-close').click();
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
            check(await page.locator('.login-story').count() === 0, 'Requested side section removed from markup');
            check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Login fits ' + width);
            if (width !== 320) await page.screenshot({ path: path.join(output, 'login-' + width + '.png'), fullPage: true });
        }
        check(errors.length === 0, 'No JavaScript errors');
        fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ checks, errors }, null, 2));
        console.log('PASS: ' + checks + ' Word notes browser checks');
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
