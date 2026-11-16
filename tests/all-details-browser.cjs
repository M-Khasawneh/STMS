// All production Details views, compiled Razor, synthetic read-only models. No SQL writes.
const{chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});
 const cases=[['requester','Requisitions'],['officer','Tenders'],['admin','Suppliers'],['supplier','SupplierPortal'],['supplier','Bids'],['officer','ReviewBids'],['officer','Compliance'],['evaluator','Evaluations'],['manager','UnifiedReports'],['top','TopCommittee'],['officer','AiAnalysis'],['officer','Clarifications'],['admin','Messages']];
 const findings=[],errors=[];let checks=0;const check=(ok,issue,info)=>{checks++;if(!ok)findings.push({issue,...info})};
 try{
  const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/*',r=>r.request().method()==='POST'?r.abort():r.continue());
  // Refuse to silently skip any new Details page in future.
  const views=fs.readdirSync('src/SourcingTenderManagementSystem.Web/Views',{withFileTypes:true}).filter(e=>e.isDirectory()&&fs.existsSync(`src/SourcingTenderManagementSystem.Web/Views/${e.name}/Details.cshtml`)).map(e=>e.name).sort();
  check(JSON.stringify(views)===JSON.stringify(cases.map(c=>c[1]).sort()),'Every Details view has a test case',{});
  for(const theme of ['light','dark'])for(const[actor,name]of cases)for(const width of[1440,768,390,320]){
   const info={name,actor,theme,width};await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});await page.setViewportSize({width,height:1000});
   const response=await page.goto(base+'/UiPagePreview/Page?name='+name+'/Details');if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing unmarked host');
   check(response.status()===200,'Details renders',info);if(response.status()!==200)continue;
   await page.evaluate(t=>{localStorage.setItem('stms-appearance',t);window.dispatchEvent(new StorageEvent('storage',{key:'stms-appearance',newValue:t}))},theme);
   check(await page.locator(`[data-detail-page="${name}"]`).count()===1,'Shared Details layout applied',info);
   check(await page.locator('main h1').count()===1,'Single clear page heading',info);
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'No page overflow',info);
   check(await page.locator('main form[method=post]').evaluateAll(forms=>forms.every(f=>f.querySelector('input[name=__RequestVerificationToken]'))),'Original POST antiforgery retained',info);
   check(await page.locator('main input:not([type=hidden]):not([type=submit]),main select,main textarea').evaluateAll(inputs=>inputs.every(e=>!e.getBoundingClientRect().width||e.labels?.length||e.getAttribute('aria-label')||e.getAttribute('aria-labelledby'))),'Accessible field labels preserved',info);
   check(await page.locator('main h1').evaluate(e=>parseFloat(getComputedStyle(e).fontSize)>=25),'Readable title',info);
   check(await page.locator('.detail-standard .supplier-facts,.detail-standard .bid-facts').evaluateAll(items=>items.every(e=>getComputedStyle(e).gridTemplateColumns.split(' ').length<3)),'Metadata stays readable in at most two columns',info);
   if(theme==='dark')check(await page.locator('.record-section-nav').evaluateAll(items=>items.every(e=>getComputedStyle(e).backgroundColor!=='rgb(255, 254, 250)')),'Section navigation uses dark surface',info);
   const contrast=await page.evaluate(()=>{
    const rgb=s=>s.match(/[\d.]+/g)?.map(Number),lum=c=>c.slice(0,3).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);
    const result=[];
    for(const e of document.querySelectorAll('.detail-workspace :is(p,a,small,dt,dd,h1,h2,h3,label,summary)')){
     if(!e.getBoundingClientRect().width||!e.textContent.trim()||e.querySelector('p,a,strong,label,button'))continue;
     const style=getComputedStyle(e),fg=rgb(style.color);if(!fg||+style.opacity<.95)continue;
     const chain=[];let c=e,skip=false;while(c){const x=getComputedStyle(c);if(x.backgroundImage!=='none'){skip=true;break;}chain.unshift(rgb(x.backgroundColor)||[0,0,0,0]);c=c.parentElement;}if(skip)continue;
     const bg=chain.reduce((b,f)=>{const a=f.length>3?f[3]:1;return f.slice(0,3).map((v,i)=>v*a+b[i]*(1-a))},[255,255,255]);
     const a=lum(fg),b=lum(bg),ratio=(Math.max(a,b)+.05)/(Math.min(a,b)+.05),large=parseFloat(style.fontSize)>=24||parseFloat(style.fontSize)>=18.66&&+style.fontWeight>=700;
     if(ratio+.05<(large?3:4.5))result.push({text:e.textContent.trim().slice(0,45),ratio:+ratio.toFixed(2)});
    }return result;
   });check(contrast.length===0,'Readable text contrast',{...info,contrast});
   if(process.env.STMS_UI_SCREENSHOTS!=='0'&&(width===1440||width===390))await page.screenshot({path:path.join(out,`${name}-${theme}-${width}.png`),fullPage:true});
  }
  check(errors.length===0,'No JavaScript errors',{errors});fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({pages:cases.length,checks,findings,errors},null,2));console.log(JSON.stringify({pages:cases.length,checks,findings,errors}));if(findings.length)process.exitCode=1;
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
