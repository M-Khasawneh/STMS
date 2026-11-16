// Motion and request behavior only, on a marked memory-only MVC fixture. No SQL or POSTs.
const{chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');const fs=require('fs'),path=require('path');
(async()=>{
 const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try{
  const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'anonymous'}}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/*',r=>r.request().method()==='POST'?r.abort():r.continue());
  for(const width of [1440,768,390,320]){
   await page.setViewportSize({width,height:1000});await page.emulateMedia({reducedMotion:'no-preference'});
   const response=await page.goto(base+'/');check(response.headers()['x-stms-bid-fixture']==='memory-only','Marked home');
   check(await page.locator('.ref-roof-light').getAttribute('d')==='m270 348 730-248 400 223','Highlight follows actual roof path');
   const state=()=>page.locator('.ref-roof-light').evaluate(e=>({offset:getComputedStyle(e).strokeDashoffset,opacity:getComputedStyle(e).opacity,state:e.getAnimations()[0]?.playState}));
   const before=await state();await page.waitForFunction(offset=>{const e=document.querySelector('.ref-roof-light');return getComputedStyle(e).strokeDashoffset!==offset&&e.getAnimations()[0]?.playState==='running'},before.offset);const after=await state();check(before.offset!==after.offset&&after.state==='running','Roof highlight actually moves '+width);
   const toggle=page.locator('[data-hero-light-toggle]');await toggle.click();await page.locator('.ref-roof-light').evaluate(e=>e.getAnimations()[0].ready);const paused=await state();await page.waitForTimeout(200);check((await state()).offset===paused.offset,'Pause freezes roof lighting');
   await toggle.click();await page.waitForTimeout(80);check((await state()).state==='running','Lighting resumes');
   await page.emulateMedia({reducedMotion:'reduce'});await page.waitForTimeout(80);const reduced=await state();check(reduced.state==='paused'&&reduced.opacity==='0'&&await toggle.isHidden(),'Reduced motion keeps the static artwork');
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Home fits '+width);
   await page.emulateMedia({reducedMotion:'no-preference'});await page.locator('footer').scrollIntoViewIfNeeded();await page.waitForTimeout(150);check((await state()).state==='paused','Offscreen hero stops animation');
  }
  let parentRequests=0,childRequests=0;
  context.on('request',r=>{if(/\/Notifications\/(Menu|Watch)/i.test(r.url())){if(r.frame().parentFrame())childRequests++;else parentRequests++;}});
  await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'officer'});await page.setViewportSize({width:1440,height:1000});
  await page.goto(base+'/Workspace');await page.evaluate(()=>{const f=document.createElement('iframe');f.dataset.workspacePopup='true';f.src='/UiPagePreview/Page?name=Profile/Index';f.style.cssText='width:900px;height:600px';document.body.append(f)});
  await page.frameLocator('iframe[data-workspace-popup]').locator('html.workspace-popup-embed').waitFor();await page.waitForTimeout(300);
  check(parentRequests>0,'Visible parent bell still connects');check(childRequests===0,'Embedded editor creates no duplicate notification requests');
  check(await page.frameLocator('iframe[data-workspace-popup]').locator('input[name="Contact.Email"]').count()===1,'Embedded form remains functional');
  await page.evaluate(()=>document.querySelector('iframe[data-workspace-popup]').remove());
  // Cancel only the test click's final browser default, after application handlers have run.
  // This keeps the old page available to inspect while simulating a pending navigation.
  await page.evaluate(()=>{document.addEventListener('click',e=>e.preventDefault(),{once:true});document.querySelector('.sidebar a[href="/Work/Tasks"]').click()});
  await page.waitForTimeout(220);
  check(await page.locator('main').evaluate(e=>getComputedStyle(e).opacity==='1'&&getComputedStyle(e).transform==='none'),'Navigation click does not dim or move content');
  await page.locator('.sidebar').getByRole('link',{name:'My tasks',exact:true}).click();await page.waitForURL('**/Work/Tasks');check((await page.title()).length>0,'Native MVC navigation completes');
  check(errors.length===0,'No JavaScript errors');const timing=await page.evaluate(()=>{const n=performance.getEntriesByType('navigation')[0];return{responseMs:n.responseEnd-n.requestStart,domContentLoadedMs:n.domContentLoadedEventEnd-n.startTime}});
  fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,parentRequests,childRequests,timing,errors},null,2));console.log(JSON.stringify({checks,parentRequests,childRequests,timing,errors}));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
