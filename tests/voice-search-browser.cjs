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
  const toggle=page.locator('[data-work-search-toggle]');
  check(await toggle.isVisible()&&await page.locator('[data-dashboard-search]').isHidden(),'search starts as a header icon with closed panel');
  check(await toggle.evaluate(e=>e.closest('.topbar')!==null&&e.closest('[data-work-search-menu]').nextElementSibling.matches('.notification-dropdown')),'microphone icon is directly beside notifications');
  check(await page.locator('main [data-dashboard-search]').count()===0,'large search section removed from dashboard content');
  await toggle.click();check(await page.locator('[data-dashboard-search]').isVisible()&&await q.evaluate(e=>document.activeElement===e),'icon opens panel and focuses search');check(await page.evaluate(()=>voiceTests.sessions.length)===0,'microphone never starts automatically');
  check(await page.locator('[data-work-record]:visible').count()===3,'actual compiled fixture rows displayed');
  await q.fill('network');check(await page.locator('[data-work-record]:visible').count()===1,'typed title search');
  await q.fill('returned');check(await page.locator('[data-work-record]:visible').count()===1,'typed status search');
  await q.fill('absent');check(await page.locator('[data-work-empty]').isVisible(),'empty results explained');
  await page.locator('[data-work-clear]').click();check(await page.locator('[data-work-record]:visible').count()===3,'clear restores rows');
  await voice.click();check(await voice.getAttribute('aria-pressed')==='true'&&(await status.textContent()).includes('Listening'),'real start callback controls listening feedback');
  await page.evaluate(()=>{const r=voiceTests.sessions.at(-1);r.onresult({results:[Object.assign([{transcript:'Network'}],{isFinal:true})]});});
  check(await q.inputValue()==='Network'&&await page.locator('[data-work-record]:visible').count()===1,'final transcript filters existing authorized rows');check(await voice.getAttribute('aria-pressed')==='false'&&await page.evaluate(()=>voiceTests.sessions.at(-1).aborts)===1,'recognition terminates after result');
  for(const text of ['Network renewal.', '“Network”, renewal!', 'مُراجعة الأجهزة؟']){
   await voice.click();await page.evaluate(text=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:text}],{isFinal:true})]}),text);
   check(await q.inputValue()===text&&await page.locator('[data-work-record]:visible').count()===1,'punctuated transcript matches authorized words: '+text);
  }
  await q.fill('Network—renewal');check(await page.locator('[data-work-record]:visible').count()===1,'typed and spoken punctuation use the same normalization');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'Finance'}],{isFinal:false})]}));
  check(await q.inputValue()==='Network—renewal'&&await voice.getAttribute('aria-pressed')==='true','interim speech does not change search');
  await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'interim'}],{isFinal:false}),Object.assign([{transcript:'Finance'}],{isFinal:true})]}));
  check(await q.inputValue()==='Finance'&&await page.locator('[data-work-record]:visible').count()===1,'first final result used after interim results');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'   '}],{isFinal:true})]}));
  check(await q.inputValue()==='Finance'&&(await status.textContent()).includes('No speech'),'empty transcript preserves prior search');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'x'.repeat(300)}],{isFinal:true})]}));
  check((await q.inputValue()).length===160&&await page.locator('[data-work-empty]').isVisible(),'long transcript bounded to field limit');
  await voice.click();await voice.click();await voice.click();
  await page.evaluate(()=>{const stale=voiceTests.sessions.at(-2);stale.onstart?.();stale.onerror?.({error:'network'});stale.onend?.();stale.onresult?.({results:[Object.assign([{transcript:'stale'}],{isFinal:true})]});});
  check(await voice.getAttribute('aria-pressed')==='true'&&(await q.inputValue()).length===160,'old session cannot stop or overwrite new session');await voice.click();
  await page.locator('[data-work-language]').selectOption('ar-JO');await voice.click();check(await page.evaluate(()=>voiceTests.sessions.at(-1).lang)==='ar-JO','selected Arabic language passed to native API');
  await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'مراجعة الاجهزة'}],{isFinal:true})]}));check(await page.locator('[data-work-record]:visible').count()===1,'Arabic diacritics and hamza normalized');
  await voice.click();await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'late callback'}],{isFinal:true})]}));check(!(await q.inputValue()).includes('late callback'),'cancel ignores late transcript');
  await voice.click();await page.keyboard.press('Escape');check(await voice.getAttribute('aria-pressed')==='false'&&await q.evaluate(e=>document.activeElement===e),'Escape cancels and returns keyboard focus');
  for(const [code,phrase] of [['not-allowed','permission'],['service-not-allowed','does not allow'],['audio-capture','microphone'],['network','connect'],['no-speech','No speech'],['language-not-supported','language'],['aborted','stopped'],['unexpected','unavailable']]){const before=await q.inputValue();await voice.click();await page.evaluate(code=>voiceTests.sessions.at(-1).onerror({error:code}),code);check((await status.textContent()).includes(phrase)&&await voice.getAttribute('aria-pressed')==='false'&&await q.inputValue()===before,'safe recovery from '+code);}
  await page.evaluate(()=>voiceTests.throwOnStart=true);await voice.click();check((await status.textContent()).includes('could not start'),'synchronous start failure recovers');await page.evaluate(()=>voiceTests.throwOnStart=false);
  await voice.click();await q.fill('Finance');check(await voice.getAttribute('aria-pressed')==='false','typing cancels microphone');
  await voice.click();await page.locator('[data-work-language]').selectOption('en-US');check(await voice.getAttribute('aria-pressed')==='false','language change cancels microphone');
  await voice.click();await page.locator('[data-work-clear]').click();check(await voice.getAttribute('aria-pressed')==='false'&&await q.inputValue()===''&&await page.locator('[data-work-record]:visible').count()===3,'clear cancels capture and restores records');
  await voice.click();await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));delete document.hidden;});check(await voice.getAttribute('aria-pressed')==='false','hidden page cancels microphone');
  await voice.focus();await page.keyboard.press('Space');check(await voice.getAttribute('aria-pressed')==='true','voice starts from keyboard');await page.keyboard.press('Escape');
  await voice.click();await page.evaluate(()=>window.dispatchEvent(new Event('pagehide')));check(await voice.getAttribute('aria-pressed')==='false','page exit cancels microphone');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onend());check((await status.textContent()).includes('without a result'),'no-result end handled');
  await voice.click();await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'<img src=x onerror=alert(1)>'}],{isFinal:true})]}));check(await page.locator('[data-dashboard-search] img').count()===0,'transcripts never become HTML');
  await page.clock.install();await voice.click();await page.clock.fastForward(21000);check((await status.textContent()).includes('timed out')&&await voice.getAttribute('aria-pressed')==='false','timeout stops recognition');await page.clock.resume();
  await voice.click();await page.locator('[data-work-search-close]').click();
  check(await voice.getAttribute('aria-pressed')==='false'&&await toggle.getAttribute('aria-expanded')==='false'&&await toggle.evaluate(e=>document.activeElement===e),'closing panel aborts capture and restores icon focus');
  await page.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'late closed panel'}],{isFinal:true})]}));
  check(!(await q.inputValue()).includes('late closed panel'),'closed panel ignores late speech');
  await toggle.click();await page.keyboard.press('Escape');check(await toggle.getAttribute('aria-expanded')==='false','Escape closes idle search panel');
  await toggle.click();await page.locator('.workspace-notification-bell').click();check(await toggle.getAttribute('aria-expanded')==='false','opening notifications closes search');
  await toggle.click();check(await page.locator('.notification-dropdown').getAttribute('open')===null,'opening search closes notification popover');
  await page.locator('[data-work-clear]').focus();await page.keyboard.press('Tab');check(await toggle.getAttribute('aria-expanded')==='false','tabbing out dismisses nonmodal search');await toggle.click();
  fs.mkdirSync(output,{recursive:true});
  for(const width of [1440,768,390,320]){await page.setViewportSize({width,height:1000});await page.emulateMedia({reducedMotion:'reduce'});await page.locator('[data-work-clear]').click();await page.waitForTimeout(120);check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'no horizontal overflow at '+width);check(await page.locator('[data-dashboard-search]').evaluate(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight}),'search popover stays inside viewport at '+width);await page.screenshot({path:path.join(output,'voice-search-'+width+'.png'),fullPage:true});}
  await voice.click();check(await voice.evaluate(e=>getComputedStyle(e).animationName)==='none','reduced motion disables listening pulse');await voice.click();
  await context.close();
  const fallback=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});await fallback.addInitScript(()=>{window.SpeechRecognition=undefined;window.webkitSpeechRecognition=undefined;});const noSpeech=await fallback.newPage();await noSpeech.goto(route);await noSpeech.locator('[data-work-search-toggle]').click();check(await noSpeech.locator('[data-work-voice]').isDisabled(),'unsupported browser disables voice');await noSpeech.locator('[data-work-query]').fill('network');check(await noSpeech.locator('[data-work-record]:visible').count()===1,'typed fallback remains functional');await fallback.close();
  for(const mode of ['webkit','insecure','constructor-failure']){
   const c=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});await c.addInitScript(mode=>{
    const Fake=class{constructor(){if(mode==='constructor-failure')throw Error('Cannot construct recognizer');}start(){this.onstart?.();this.onresult?.({results:[Object.assign([{transcript:'Finance.'}],{isFinal:true})]});}abort(){throw Error('Already stopped');}};
    window.SpeechRecognition=mode==='webkit'?undefined:Fake;window.webkitSpeechRecognition=mode==='webkit'?Fake:undefined;
    if(mode==='insecure')Object.defineProperty(window,'isSecureContext',{configurable:true,value:false});
   },mode);
   const p=await c.newPage();p.on('pageerror',e=>errors.push(e.message));await p.goto(route);await p.locator('[data-work-search-toggle]').click();const v=p.locator('[data-work-voice]');
   if(mode==='insecure')check(await v.isDisabled(),'insecure connection disables voice');
   else{await v.click();if(mode==='webkit')check(await p.locator('[data-work-query]').inputValue()==='Finance.'&&await p.locator('[data-work-record]:visible').count()===1,'prefixed API works and abort failure is contained');
    else check((await p.locator('[data-work-status]').innerText()).includes('could not start')&&await v.getAttribute('aria-pressed')==='false','constructor failure restores usable controls');}
   await p.locator('[data-work-query]').fill('Network');check(await p.locator('[data-work-record]:visible').count()===1,'typed search remains available: '+mode);await c.close();
  }
  for(const actor of ['admin','supplier']){const c=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':actor}});const p=await c.newPage();await p.goto(route);check(await p.locator('[data-dashboard-search]').count()===0,actor+' has no employee voice interface');await c.close();}
  const noJs=await browser.newContext({javaScriptEnabled:false,extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});const p=await noJs.newPage();await p.goto(route);check(await p.locator('[data-work-record]').count()===3&&await p.locator('[data-dashboard-search]').isHidden(),'no-JS preserves all original records');await noJs.close();
  check(errors.length===0,'no JavaScript errors');fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({checks,errors,recognition:'mocked',physicalMicrophone:false},null,2));console.log('PASS: '+checks+' memory-fixture browser checks. Recognition events were mocked; no real microphone claim.');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
