// Native browser recognition, synthetic audio only. Never opens the physical microphone.
// Supply an external PCM WAV with a harmless test phrase; recognition success is reported separately.
const { chromium } = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path');

(async () => {
    const [base, output, audio] = process.argv.slice(2);
    if (!base || !output || !audio || !fs.existsSync(audio)) throw Error('Supply marked fixture URL, external output directory and synthetic PCM WAV.');
    if (!['127.0.0.1', 'localhost'].includes(new URL(base).hostname)) throw Error('Local fixtures only.');
    fs.mkdirSync(output, { recursive: true });
    const results = [];
    for (const channel of ['msedge', 'chrome']) {
        for (const scenario of ['en-US', 'ar-JO', 'denied']) {
            const language = scenario === 'denied' ? 'en-US' : scenario;
            const browser = await chromium.launch({ channel, headless: true, args: [
                ...(scenario === 'denied' ? [] : ['--use-fake-ui-for-media-stream']), '--use-fake-device-for-media-stream',
                '--use-file-for-fake-audio-capture=' + path.resolve(audio)
            ] });
            try {
                const context = await browser.newContext({ permissions: scenario === 'denied' ? [] : ['microphone'], extraHTTPHeaders: { 'X-Bid-Test-Actor': 'officer' } });
                const page = await context.newPage(), errors = [];
                if (scenario === 'denied') {
                    const cdp = await context.newCDPSession(page);
                    await cdp.send('Browser.setPermission', { permission: { name: 'microphone' }, setting: 'denied', origin: new URL(base).origin });
                }
                page.on('pageerror', error => errors.push(error.message));
                const response = await page.goto(base + '/Workspace');
                if (response.headers()['x-stms-bid-fixture'] !== 'memory-only') throw Error('Refusing non-memory host.');
                await page.locator('[data-work-search-toggle]').click();
                // Observe genuine events without replacing the recognizer or injecting results.
                const supported = await page.evaluate(() => {
                    window.nativeVoiceEvents = [];
                    const Native = window.SpeechRecognition || window.webkitSpeechRecognition;
                    if (!Native) return false;
                    const start = Native.prototype.start;
                    Native.prototype.start = function (...args) {
                        for (const type of ['start', 'audiostart', 'soundstart', 'speechstart', 'result', 'error', 'end']) {
                            this.addEventListener(type, event => nativeVoiceEvents.push({ type, error: event.error,
                                transcript: event.results?.[0]?.[0]?.transcript }));
                        }
                        return start.apply(this, args);
                    };
                    return true;
                });
                const query = page.locator('[data-work-query]'), voice = page.locator('[data-work-voice]');
                await query.fill('Finance');
                if (supported) {
                    await page.locator('[data-work-language]').selectOption(language);
                    await voice.click();
                    await page.waitForFunction(() => document.querySelector('[data-work-voice]').getAttribute('aria-pressed') === 'false', null, { timeout: 25000 });
                }
                const events = await page.evaluate(() => nativeVoiceEvents);
                const transcribed = events.some(event => event.type === 'result' && event.transcript?.trim());
                if (supported && scenario === 'denied' && !events.some(event => event.error === 'not-allowed')) throw Error('Native permission denial was not handled.');
                if (!transcribed && await query.inputValue() !== 'Finance') throw Error('Native failure erased existing search.');
                await query.fill('Network');
                if (await page.locator('[data-work-record]:visible').count() !== 1 || errors.length) throw Error('Typed fallback or JavaScript failed.');
                const result = { channel, scenario, language, supported, transcribed, events, status: await page.locator('[data-work-status]').innerText(), errors };
                results.push(result);
                console.log(JSON.stringify(result));
            } finally { await browser.close(); }
        }
    }
    fs.writeFileSync(path.join(output, 'native-results.json'), JSON.stringify(results, null, 2));
    console.log('Native lifecycle/fallback checks passed. Transcription successes: ' + results.filter(result => result.transcribed).length + '/' + results.filter(result => result.scenario !== 'denied').length + '. Physical microphone and human speech were not tested.');
})().catch(error => { console.error(error); process.exitCode = 1; });
