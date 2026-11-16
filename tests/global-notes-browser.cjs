// Shared visual notes, checked against real Razor views on a marked memory-only fixture.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const [base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try{
  const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();
  page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/*',async r=>{
   if(r.request().method()==='POST')return r.abort();
   // Render-only records have no persisted QR target: use the real generator on a fixture-only endpoint.
   if(/\/(Requisitions|Tenders)\/Qr[/?]/.test(r.request().url()))return r.fulfill({response:await context.request.get(base+'/UiPagePreview/Qr')});
   return r.continue();
  });
  async function visit(actor,route,theme,width){
   await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});await page.setViewportSize({width,height:1000});
   const response=await page.goto(base+route);
   if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing non-fixture');
   check(response.status()===200,'Razor renders '+route);
   await page.evaluate(t=>{localStorage.setItem('stms-appearance',t);window.dispatchEvent(new StorageEvent('storage',{key:'stms-appearance',newValue:t}))},theme);
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Page fits '+route+'/'+width);
  }
  for(const theme of ['light','dark'])for(const width of [1440,768,390,320]){
   await visit('anonymous','/Account/Login',theme,width);
   check(await page.locator('.passkey-login').evaluate(e=>{const s=getComputedStyle(e);return parseFloat(s.borderRadius)>=16&&parseFloat(s.paddingLeft)>=16}),'Device sign-in has rounded, padded surface');
   check(await page.locator('[data-passkey-login] input[name=__RequestVerificationToken]').count()===1,'Passkey antiforgery preserved');
   for(const name of ['Requisitions','Tenders']){
    await visit('officer','/UiPagePreview/Page?name='+name+'/Details',theme,width);
    const qr=page.locator('[data-qr-preview]');check(await qr.count()===1,'One visible QR control '+name);
    check(await qr.evaluate(e=>e.getBoundingClientRect().top<document.querySelector('main .table-responsive').getBoundingClientRect().top),'QR precedes item tables '+name);
    await qr.click();await page.locator('.qr-preview-image').evaluate(e=>e.decode());check(await page.locator('.qr-preview-image').evaluate(e=>e.naturalWidth>0),'QR actually loads '+name);await page.keyboard.press('Escape');
   }
   await visit('requester','/NotesPreview/Record',theme,width);
   check(await page.locator('.rq-activity').evaluate(e=>parseFloat(getComputedStyle(e).borderRadius)>=16),'Activity rounded container');
   check(await page.locator('.requisition-history>li').first().evaluate(e=>getComputedStyle(e).backgroundColor==='rgba(0, 0, 0, 0)'),'History entries do not become square blocks');
   await visit('officer','/Reports?Category=offers&Search=commercial-fixture',theme,width);
   const table=page.locator('.report-results table').first();
   check(await table.locator('thead th').count()===3&&!(await table.locator('thead').innerText()).includes('Currency'),'Separate currency column removed');
   check((await table.locator('td.report-number').innerText()).includes('JOD'),'Actual currency accompanies amount');
   check(await table.locator('th.report-number').evaluate(e=>getComputedStyle(e).textAlign==='end')&&await table.locator('td.report-number').evaluate(e=>getComputedStyle(e).textAlign==='end'),'Numeric headers and amounts align');
   await visit('officer','/Reports?Search=participation-fixture',theme,width);
   check(await page.locator('th.report-number').count()===4,'All participation metric headers align with numeric cells');
   if(width===1440||width===390)await page.screenshot({path:path.join(out,`participation-${theme}-${width}.png`),fullPage:true});
  }
  for(const actor of ['admin','requester','supplier','officer','manager','evaluator','top']){
   await visit(actor,'/Workspace','dark',1440);
   check(await page.locator('.sidebar .side-link').evaluateAll(links=>links.every(l=>l.querySelector('.nav-icon'))),'Every visible sidebar link has an icon '+actor);
  }
  await visit('officer','/Reports/Print?Category=offers&Search=commercial-fixture','light',1440);
  check(await page.locator('thead th').count()===3&&(await page.locator('td.report-number').innerText()).includes('JOD'),'Print has matching combined amount and currency');
  check(errors.length===0,'No JavaScript errors');
  fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log(`PASS: ${checks} shared-note checks, no SQL writes.`);
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
