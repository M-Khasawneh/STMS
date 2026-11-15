// Tests browser lifecycle with an explicitly injected recognizer. No microphone or speech service is used.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const [base,output]=process.argv.slice(2);if(!base||!output)throw Error('Usage: node tests/voice-search-browser.cjs memory-fixture-url output-directory');
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,message)=>{if(!ok)throw Error(message);checks++;console.log('PASS: '+message);};
 const route=base+'/Workspace';
 try{
  const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'},viewport:{width:1440,height:1000}});
  await context.addInitScript(()=>{window.voiceTests={sessions:[],throwOnStart:false};window.SpeechRecognition=class{constructor(){window.voiceTests.sessions.push(this);this.aborts=0;}start(){if(window.voiceTests.throwOnStart)throw Error('fixture failure');this.onstart?.();}abort(){this.aborts++;this.onend?.();}};});
  const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));const first=await page.goto(route);check(first.headers()['x-stms-bid-fixture']==='memory-only','refused non-memory fixture');
  const q=page.locator('[data-work-query]'),voice=page.locator('[data-work-voice]'),status=page.locator('[data-work-status]');
  check(await page.locator('[data-dashboard-search]').isVisible(),'search progressively enabled');check(await page.evaluate(()=>voiceTests.sessions.length)===0,'microphone never starts automatically');
  check(await page.locator('[data-work-record]:visible').count()===3,'actual compiled fixture rows displayed');
  await q.fill('network');check(await page.locator('[data-work-record]:visible').count()===1,'typed title search');
  await q.fill('returned');check(await page.locator('[data-work-record]:visible').count()===1,'typed status search');
  await q.fill('absent');check(await page.locator('[data-work-empty]').isVisible(),'empty results explained');
  await page.locator('[data-work-clear]').click();check(await page.locator('[data-work-record]:visible').count()===3,'clear restores rows');
  await voice.click();check(await voice.getAttribute('aria-pressed')==='true'&&(await status.textContent()).includes('Listening'),'real start callback controls listening feedback');
  await page.evaluate(()=>{const r=voiceTests.sessions.at(-1);r.onresult({results:[Object.assign([{transcript:'Network'}],{isFinal:true})]});});
  check(await q.inputValue()==='Network'&&await page.locator('[data-work-record]:visible').count()===1,'final transcript filters existing authorized rows');check(await voice.getAttribute('aria-pressed')==='false'&&await page.evaluate(()=>voiceTests.sessions.at(-1).aborts)===1,'recognition terminates after result');
  await page.locator('[data-work-language]').selectOption('ar-JO');await voice.click();check(await page.evaluate(()=>voiceTests.sessions.at(-1).lang)==='ar-JO','selected Arabic language passed to native API');
  await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'مراجعة الاجهزة'}],{isFinal:true})]}));check(await page.locator('[data-work-record]:visible').count()===1,'Arabic diacritics and hamza normalized');
  await voice.click();await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'late callback'}],{isFinal:true})]}));check(!(await q.inputValue()).includes('late callback'),'cancel ignores late transcript');
  await voice.click();await page.keyboard.press('Escape');check(await voice.getAttribute('aria-pressed')==='false'&&await q.evaluate(e=>document.activeElement===e),'Escape cancels and returns keyboard focus');
  for(const [code,phrase] of [['not-allowed','permission'],['audio-capture','microphone'],['network','connect'],['no-speech','No speech'],['language-not-supported','language']]){await voice.click();await page.evaluate(code=>voiceTests.sessions.at(-1).onerror({error:code}),code);check((await status.textContent()).includes(phrase)&&await voice.getAttribute('aria-pressed')==='false','safe recovery from '+code);}
  await page.evaluate(()=>voiceTests.throwOnStart=true);await voice.click();check((await status.textContent()).includes('could not start'),'synchronous start failure recovers');await page.evaluate(()=>voiceTests.throwOnStart=false);
  await voice.click();await q.fill('Finance');check(await voice.getAttribute('aria-pressed')==='false','typing cancels microphone');
  await voice.click();await page.locator('[data-work-language]').selectOption('en-US');check(await voice.getAttribute('aria-pressed')==='false','language change cancels microphone');
  await voice.click();await page.evaluate(()=>window.dispatchEvent(new Event('pagehide')));check(await voice.getAttribute('aria-pressed')==='false','page exit cancels microphone');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onend());check((await status.textContent()).includes('without a result'),'no-result end handled');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'<img src=x onerror=alert(1)>'}],{isFinal:true})]}));check(await page.locator('[data-dashboard-search] img').count()===0,'transcripts never become HTML');
  await page.clock.install();await voice.click();await page.clock.fastForward(21000);check((await status.textContent()).includes('timed out')&&await voice.getAttribute('aria-pressed')==='false','timeout stops recognition');await page.clock.resume();
  fs.mkdirSync(output,{recursive:true});
  for(const width of [1440,768,390,320]){await page.setViewportSize({width,height:1000});await page.emulateMedia({reducedMotion:'reduce'});await page.locator('[data-work-clear]').click();await page.waitForTimeout(120);check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'no horizontal overflow at '+width);await page.screenshot({path:path.join(output,'voice-search-'+width+'.png'),fullPage:true});}
  await voice.click();check(await voice.evaluate(e=>getComputedStyle(e).animationName)==='none','reduced motion disables listening pulse');await voice.click();
  await context.close();
  const fallback=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});await fallback.addInitScript(()=>{window.SpeechRecognition=undefined;window.webkitSpeechRecognition=undefined;});const noSpeech=await fallback.newPage();await noSpeech.goto(route);check(await noSpeech.locator('[data-work-voice]').isDisabled(),'unsupported browser disables voice');await noSpeech.locator('[data-work-query]').fill('network');check(await noSpeech.locator('[data-work-record]:visible').count()===1,'typed fallback remains functional');await fallback.close();
  for(const actor of ['admin','supplier']){const c=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':actor}});const p=await c.newPage();await p.goto(route);check(await p.locator('[data-dashboard-search]').count()===0,actor+' has no employee voice interface');await c.close();}
  const noJs=await browser.newContext({javaScriptEnabled:false,extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});const p=await noJs.newPage();await p.goto(route);check(await p.locator('[data-work-record]').count()===3&&await p.locator('[data-dashboard-search]').isHidden(),'no-JS preserves all original records');await noJs.close();
  check(errors.length===0,'no JavaScript errors');console.log('PASS: '+checks+' memory-fixture browser checks. Recognition events were mocked; no real microphone claim.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
