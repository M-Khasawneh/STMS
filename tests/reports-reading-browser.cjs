// Every authorized report category on the explicitly marked memory fixture. No SQL or POSTs.
const{chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');const fs=require('fs'),path=require('path');
(async()=>{
 const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try{
  const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));await context.route('**/*',r=>r.request().method()==='POST'?r.abort():r.continue());
  for(const actor of ['admin','supplier','requester','officer','manager','evaluator','top']){
   await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});let r=await page.goto(base+'/Reports');if(r.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing unmarked host');
   const categories=await page.locator('.report-tabs a').evaluateAll(items=>items.map(e=>e.getAttribute('href')));
   check(categories.length>0,'Authorized reports exist '+actor);
   for(const href of categories)for(const theme of ['light','dark'])for(const width of [1440,768,390,320]){
    await page.setViewportSize({width,height:1000});r=await page.goto(new URL(href,base).href);check(r.status()===200,'Authorized category renders '+actor+'/'+href);
    await page.evaluate(t=>{localStorage.setItem('stms-appearance',t);window.dispatchEvent(new StorageEvent('storage',{key:'stms-appearance',newValue:t}))},theme);
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Report fits '+actor+'/'+width);
    check(await page.locator('.report-tabs [aria-current=page]').count()===1,'One active category');
    check(await page.locator('.report-layout').evaluate((e,w)=>getComputedStyle(e).gridTemplateColumns.split(' ').length===(w>1150?2:1),width),'Responsive library and reading column');
    check(await page.locator('.report-results td').evaluateAll(items=>items.every(e=>parseFloat(getComputedStyle(e).fontSize)>=14)),'Readable table values');
    check(await page.locator('th.report-number').evaluateAll(items=>items.every(e=>getComputedStyle(e).textAlign==='end')),'Numeric headings align');
    if(await page.locator('#From').count())check(await page.locator('.report-date-range').evaluate(e=>e.querySelector('#From')&&e.querySelector('#To')&&getComputedStyle(e).gridTemplateColumns.split(' ').length===(innerWidth>480?2:1)),'Date pair stays grouped');
    check(await page.locator('.report-filter form').getAttribute('method')==='get','Original GET filtering preserved');
    const notes=page.locator('details.report-notes');check(await notes.count()===1,'Definitions available without clutter');await notes.locator('summary').click();check(await notes.locator('ul').isVisible(),'Definitions expand accessibly');
    if(href===categories[0]&&(actor==='admin'||actor==='supplier')&&(width===1440||width===390)){await page.evaluate(()=>{document.activeElement?.blur();scrollTo({top:0,behavior:'instant'})});await page.screenshot({path:path.join(out,`${actor}-${theme}-${width}.png`),fullPage:true});}
   }
  }
  for(const[actor,category]of[['admin','offers'],['supplier','requests'],['requester','decisions']]){const r=await context.request.get(base+'/Reports?Category='+category,{headers:{'X-Bid-Test-Actor':actor}});check(r.status()===403,'Forbidden category remains forbidden');}
  check(errors.length===0,'No JavaScript errors');fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log(`PASS: ${checks} report-category, role, responsive and reading checks; no SQL writes.`);
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
