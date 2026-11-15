// Browser lifecycle uses injected recognition events; real search navigation uses existing MVC actions.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,text)=>{if(!ok)throw Error(text);checks++;console.log('PASS: '+text)};
 try{
 const c=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'},reducedMotion:'reduce'});
 await c.addInitScript(()=>{window.voiceTests={sessions:[],fail:false};window.SpeechRecognition=class{constructor(){voiceTests.sessions.push(this);this.aborts=0}start(){if(voiceTests.fail)throw Error('Test start failure');this.onstart?.()}abort(){this.aborts++;this.onend?.()}}});
 const p=await c.newPage();p.on('pageerror',e=>errors.push(e.message));
 const visit=async route=>{const r=await p.goto(base+route);check(r.headers()['x-stms-bid-fixture']==='memory-only'&&r.status()===200,'Marked authorized page '+route)};
 for(const actor of ['admin','supplier','requester','officer','manager','evaluator','top']){
  await c.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});
  for(const route of ['/Workspace','/Reports','/Work/Tasks','/Work/Search?q=network']){
   await visit(route);check(await p.locator('.topbar [data-work-search-toggle]').isVisible(),'Global header icon '+actor+route);
   check(await p.locator('[data-work-search-toggle]').count()===1&&await p.locator('[data-dashboard-search]').isHidden(),'One closed search panel');
  }
 }
 await c.setExtraHTTPHeaders({'X-Bid-Test-Actor':'officer'});await visit('/Workspace');
 const toggle=p.locator('[data-work-search-toggle]'),q=p.locator('[data-work-query]'),voice=p.locator('[data-work-voice]'),status=p.locator('[data-work-status]');
 const open=async()=>{if(await toggle.getAttribute('aria-expanded')!=='true')await toggle.click()};
 const result=async text=>p.evaluate(text=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:text}],{isFinal:true})]}),text);
 check(await toggle.evaluate(e=>e.closest('[data-work-search-menu]').nextElementSibling.matches('.notification-dropdown')),'Search beside notifications');
 check(await p.locator('main [data-dashboard-search]').count()===0,'No search form inside dashboard content');
 await open();check(await q.evaluate(e=>document.activeElement===e)&&await p.evaluate(()=>voiceTests.sessions.length===0),'Opening focuses text without opening microphone');
 const beforeRows=await p.locator('[data-work-record]:visible').count();await q.fill('unknown words');check(await p.locator('[data-work-record]:visible').count()===beforeRows,'Global query does not filter or hide dashboard records');
 await voice.click();check(await voice.getAttribute('aria-pressed')==='true','Explicit voice activation');await result('Network renewal.');
 check(await q.inputValue()==='Network renewal'&&await voice.getAttribute('aria-pressed')==='false','Final transcript removes terminal punctuation and stops capture');
 check(p.url()===base+'/Workspace','Speech does not auto-navigate');
 await Promise.all([p.waitForURL('**/Work/Search?q=Network+renewal'),p.locator('[data-work-search-submit]').click()]);
 check(await p.locator('#work-query').inputValue()==='Network renewal','Explicit search binds to existing global MVC search');
 await open();await p.locator('[data-work-language]').selectOption('ar-JO');await voice.click();check(await p.evaluate(()=>voiceTests.sessions.at(-1).lang)==='ar-JO','Arabic language selected');await result('مراجعة الأجهزة؟');check(await q.inputValue()==='مراجعة الأجهزة','Arabic phrase prepared safely');
 await voice.click();await p.evaluate(()=>voiceTests.sessions.at(-1).onresult({results:[Object.assign([{transcript:'interim'}],{isFinal:false})]}));check(await q.inputValue()==='مراجعة الأجهزة'&&await voice.getAttribute('aria-pressed')==='true','Interim results do not replace query');await voice.click();
 await voice.click();await result('   ');check(await q.inputValue()==='مراجعة الأجهزة'&&(await status.textContent()).includes('No speech'),'Empty result preserves query');
 await voice.click();await result('x'.repeat(300));check((await q.inputValue()).length===160,'Transcript length bound');
 await voice.click();await voice.click();await voice.click();await p.evaluate(()=>{const r=voiceTests.sessions.at(-2);r.onresult({results:[Object.assign([{transcript:'stale'}],{isFinal:true})]});r.onerror({error:'network'});r.onend()});check(await voice.getAttribute('aria-pressed')==='true'&&(await q.inputValue()).length===160,'Stale callbacks cannot replace current session');await voice.click();
 for(const[code,word]of[['not-allowed','permission'],['service-not-allowed','does not allow'],['audio-capture','microphone'],['network','connect'],['no-speech','No speech'],['language-not-supported','language'],['aborted','stopped'],['unknown','unavailable']]){const previous=await q.inputValue();await voice.click();await p.evaluate(code=>voiceTests.sessions.at(-1).onerror({error:code}),code);check((await status.textContent()).includes(word)&&await q.inputValue()===previous&&await voice.getAttribute('aria-pressed')==='false','Recovery '+code)}
 await p.evaluate(()=>voiceTests.fail=true);await voice.click();check((await status.textContent()).includes('could not start'),'Start failure recovers');await p.evaluate(()=>voiceTests.fail=false);
 await voice.click();await q.fill('Finance');check(await voice.getAttribute('aria-pressed')==='false','Typing cancels capture');
 await voice.click();await p.locator('[data-work-language]').selectOption('en-US');check(await voice.getAttribute('aria-pressed')==='false','Language change cancels capture');
 await voice.click();await p.keyboard.press('Escape');check(await voice.getAttribute('aria-pressed')==='false'&&await q.evaluate(e=>document.activeElement===e),'Escape stops recording');await p.keyboard.press('Escape');check(await toggle.getAttribute('aria-expanded')==='false','Second Escape closes panel');await open();
 await voice.click();await p.locator('[data-work-search-close]').click();await result('late closed');check(await toggle.getAttribute('aria-expanded')==='false'&&await q.inputValue()==='Finance','Close aborts and rejects late speech');await open();
 await voice.click();await p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));delete document.hidden});check(await voice.getAttribute('aria-pressed')==='false','Hidden page stops recording');
 await voice.click();await p.evaluate(()=>window.dispatchEvent(new Event('pagehide')));check(await voice.getAttribute('aria-pressed')==='false','Page exit stops recording');
 await p.clock.install();await voice.click();await p.clock.fastForward(21000);check((await status.textContent()).includes('timed out'),'Time limit stops recording');await p.clock.resume();
 await voice.click();await result('<img src=x onerror=alert(1)>');check(await p.locator('[data-dashboard-search] img').count()===0,'Transcript is never rendered as HTML');
 await p.locator('[data-work-clear]').click();await p.locator('[data-work-search-submit]').click();check(await q.evaluate(e=>!e.checkValidity()),'Search requires valid input');
 await p.locator('.workspace-notification-bell').click();check(await toggle.getAttribute('aria-expanded')==='false','Notifications dismiss search');await open();check(await p.locator('.notification-dropdown').getAttribute('open')===null,'Search dismisses notifications');
 for(const width of[1440,768,390,320]){await p.setViewportSize({width,height:1000});check(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Header fits '+width);check(await p.locator('[data-dashboard-search]').evaluate(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&r.bottom<=innerHeight}),'Search panel fits '+width);await p.screenshot({path:path.join(out,'global-search-'+width+'.png'),fullPage:true})}
 await voice.click();check(await voice.evaluate(e=>getComputedStyle(e).animationName)==='none','Reduced motion');await voice.click();
 for(const actor of['supplier','requester','officer','manager','evaluator','top']){await c.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});for(const width of[1440,390]){await p.setViewportSize({width,height:1000});await visit('/Workspace');check(await p.locator('main h1').count()===1&&await p.locator('.role-work-panel').count()>0&&await p.locator('.role-dashboard table').count()===0,'Calm dashboard composition '+actor);check(await p.locator('.role-updates').count()===1&&(await p.locator('.role-updates').innerText()).includes('Your workflow update'),'Existing personal notifications retained '+actor);check(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Dashboard fits '+actor+width);await p.screenshot({path:path.join(out,'dashboard-'+actor+'-'+width+'.png'),fullPage:true})}}
 await c.close();
 for(const mode of['unsupported','insecure','webkit','constructor-failure']){
 const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});await context.addInitScript(mode=>{const Fake=class{constructor(){if(mode==='constructor-failure')throw Error('constructor failure')}start(){this.onresult?.({results:[Object.assign([{transcript:'Finance.'}],{isFinal:true})]})}abort(){throw Error('Already stopped')}};window.SpeechRecognition=mode==='unsupported'||mode==='webkit'?undefined:Fake;window.webkitSpeechRecognition=mode==='webkit'?Fake:undefined;if(mode==='insecure')Object.defineProperty(window,'isSecureContext',{configurable:true,value:false})},mode);
 const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/Reports');await page.locator('[data-work-search-toggle]').click();const v=page.locator('[data-work-voice]');if(mode==='unsupported'||mode==='insecure')check(await v.isDisabled(),'Voice disabled '+mode);else{await v.click();check(mode==='webkit'?await page.locator('[data-work-query]').inputValue()==='Finance':(await page.locator('[data-work-status]').innerText()).includes('could not start'),'Safe API handling '+mode)}await page.locator('[data-work-query]').fill('Network');await Promise.all([page.waitForURL('**/Work/Search?q=Network'),page.locator('[data-work-search-submit]').click()]);check(await page.locator('#work-query').inputValue()==='Network','Typed global fallback '+mode);await context.close();
 }
 const nojs=await browser.newContext({javaScriptEnabled:false,extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});const plain=await nojs.newPage();await plain.goto(base+'/Workspace');check(await plain.locator('[data-work-search-menu]').isHidden()&&await plain.locator('a.workspace-search-link').count()===1,'No-JS retains ordinary workspace search route');await nojs.close();
 const anon=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'anonymous'}});const publicPage=await anon.newPage();await publicPage.goto(base+'/Account/Login');check(await publicPage.locator('[data-work-search-toggle]').count()===0,'No private search on public login');await anon.close();
 check(errors.length===0,'No JavaScript errors');fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors,recognition:'mocked'},null,2));console.log('PASS: '+checks+' global voice-search and role-dashboard checks. Speech events are simulated.');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exitCode=1});
