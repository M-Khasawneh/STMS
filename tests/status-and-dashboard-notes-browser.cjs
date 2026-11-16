// No SQL: only the explicitly marked in-memory MVC presentation fixture.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try{
  const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/*',r=>r.request().method()==='POST'?r.abort():r.continue());
  const visit=async(actor,route,theme,width)=>{
   await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor,'X-Admin-Test-State':'populated'});await page.setViewportSize({width,height:1000});
   const response=await page.goto(base+route);if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing unmarked host');
   check(response.status()===200,'Page renders');await page.evaluate(t=>{localStorage.setItem('stms-appearance',t);window.dispatchEvent(new StorageEvent('storage',{key:'stms-appearance',newValue:t}))},theme);
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Responsive page');
  };
  for(const theme of ['light','dark'])for(const width of [1440,1024,768,390,320]){
   await visit('admin','/Workspace',theme,width);
   check(await page.locator('.admin-metric').count()===6,'Six real bound metrics retained');
   check(await page.locator('.admin-metrics').evaluate((e,w)=>getComputedStyle(e).gridTemplateColumns.split(' ').length===(w>600?3:2),width),'Three columns desktop/tablet, two mobile');
   if(width===1440)await page.screenshot({path:path.join(out,`admin-${theme}.png`),fullPage:true});
   for(const state of ['Draft','Submitted','UnderReview','Approved','Rejected','Returned','ConvertedToTender']){
    await visit('requester','/UiPagePreview/Page?name=Requisitions/Details&state='+state,theme,width);
    check(await page.locator('[data-reference-state]').count()===7,'Every status is shown even without audit history');
    check(await page.locator('[data-reference-state][aria-current=step]').count()===1&&await page.locator(`[data-reference-state="${state}"]`).getAttribute('aria-current')==='step','Reference matches actual status');
    check(await page.locator('.requisition-history>li').count()===0,'No invented audit events for absent history');
    check(await page.locator('[data-reference-state]').evaluateAll(items=>items.every(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth+1})),'All status labels wrap inside page');
   }
  }
  for(const actor of ['admin','supplier','requester','officer','manager','evaluator','top']){
   await visit(actor,'/NotesPreview/Welcome','dark',1440);
   check(await page.locator('[data-signin-welcome]').count()===1,'Welcome renders for '+actor);
   check((await page.locator('[data-signin-welcome]').innerText()).includes('Welcome, '+actor+'.'),'Uses authenticated name');
   await page.reload();check(await page.locator('[data-signin-welcome]').count()===0,'Welcome appears once');
  }
  await visit('requester','/NotesPreview/Record','light',390);
  check(await page.locator('.requisition-history>li').count()===4,'Real recorded events remain intact');
  await page.locator('.request-state-reference').screenshot({path:path.join(out,'all-statuses-mobile.png')});
  check(errors.length===0,'No JavaScript errors');fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log(`PASS: ${checks} status reference, dashboard grid and one-time welcome checks; no SQL writes.`);
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
