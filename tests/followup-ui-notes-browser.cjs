// Read-only visual sweep on marked in-memory MVC fixtures. Blocks all POST requests.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const [base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try{
  const context=await browser.newContext({reducedMotion:'reduce'}),page=await context.newPage();
  page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/*',r=>r.request().method()==='POST'?r.abort():r.continue());
  async function visit(actor,route,theme,width){
   await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});await page.setViewportSize({width,height:1000});
   const response=await page.goto(base+route);
   if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing unmarked host');
   check(response.status()===200,'View renders '+actor+'/'+route);
   await page.evaluate(t=>{localStorage.setItem('stms-appearance',t);window.dispatchEvent(new StorageEvent('storage',{key:'stms-appearance',newValue:t}))},theme);
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'No page overflow '+width);
  }
  if(process.env.STMS_UI_EMBED_ONLY!=='1')for(const theme of ['light','dark']){
   for(const actor of ['admin','supplier','requester','officer','manager','evaluator','top'])for(const width of [1440,768,390,320]){
    await visit(actor,'/UiPagePreview/Page?name=Profile/Index',theme,width);
    const panels=page.locator('.profile-settings>.form-panel');
    check(await panels.evaluateAll(items=>items.every((e,i)=>{const s=getComputedStyle(e),r=e.getBoundingClientRect();return parseFloat(s.borderRadius)>=16&&parseFloat(s.paddingTop)>=20&&(!i||r.top-items[i-1].getBoundingClientRect().bottom>=24)})),'Separated rounded profile sections '+actor);
    check(await page.locator('form[action="/Profile/Password"] input[name=__RequestVerificationToken]').count()===1,'Password antiforgery binding preserved');
    const identity=page.locator('.topbar .workspace-profile-link');
    await page.keyboard.press('Tab');await identity.focus();
    check(await identity.evaluate(e=>{const s=getComputedStyle(e);return e.matches(':focus-visible')&&parseFloat(s.borderRadius)>=14&&s.outlineStyle==='solid'&&parseFloat(s.outlineWidth)>=2}),'Rounded visible keyboard identity focus');
    if(width===1440){
     const search=page.locator('.workspace-header-search input');await search.focus();
     check(await search.evaluate(e=>getComputedStyle(e).outlineStyle==='none'&&getComputedStyle(e.closest('form')).boxShadow!=='none'&&parseFloat(getComputedStyle(e.closest('form')).borderRadius)>=14),'Single rounded search focus ring');
     check(await search.getAttribute('name')==='q','Search binding retained');
    }
    if(actor==='admin'&&(width===1440||width===390))await page.screenshot({path:path.join(out,`profile-${theme}-${width}.png`),fullPage:true});
   }
   for(const width of [1440,768,390,320]){
    for(const state of ['Draft','Published','Open','Closed','UnderEvaluation','AwaitingManagerReview','AwaitingCommitteeDecision','Awarded','Cancelled']){
     await visit('officer','/UiPagePreview/Page?name=Tenders/Details&state='+state,theme,width);
     check(await page.locator('.tender-stage-list').evaluate(list=>{
      const items=[...list.children],current=items.findIndex(e=>e.getAttribute('aria-current')==='step');
      const gold=getComputedStyle(document.querySelector('.topbar')).getPropertyValue('--st-gold').trim();
      const probe=document.createElement('span');probe.style.color=gold;list.append(probe);const color=getComputedStyle(probe).color;probe.remove();
      return items.every((e,i)=>{const rail=getComputedStyle(e,'::before');return i===items.length-1?rail.display==='none':(rail.backgroundColor===color)===(current>=0&&i<current)});
     }),'Tender rail fills only before saved stage '+state);
     check(await page.locator('.tender-progress [aria-current=step]').count()===1,'Current stage unchanged '+state);
     if(state==='Open'&&(width===1440||width===390))await page.screenshot({path:path.join(out,`tender-${theme}-${width}.png`),fullPage:true});
    }
    for(const state of ['Draft','Submitted','UnderReview','Approved','ConvertedToTender','Returned','Rejected']){
     await visit('requester','/UiPagePreview/Page?name=Requisitions/Details&state='+state,theme,width);
     check(await page.locator('.status-track:not(.status-branches)').evaluate(list=>{
      const items=[...list.children],current=items.findIndex(e=>e.getAttribute('aria-current')==='step');
      const probe=document.createElement('span');probe.style.color='var(--st-gold)';list.append(probe);const color=getComputedStyle(probe).color;probe.remove();
      return items.slice(0,-1).every((e,i)=>(getComputedStyle(e,'::before').backgroundColor===color)===(current>=0&&i<current));
     }),'Request rails agree with actual current main/alternative stage '+state);
    }
   }
  }
  // The reported profile spacing is particularly visible inside the existing iframe editor.
  for(const theme of ['light','dark'])for(const actor of ['admin','supplier','requester','officer','manager','evaluator','top'])for(const width of [1440,390,320]){
   await visit(actor,'/Workspace',theme,width);
   await page.evaluate(()=>{const frame=document.createElement('iframe');frame.dataset.workspacePopup='';frame.style.cssText='display:block;width:100%;height:1000px;border:0';frame.src='/UiPagePreview/Page?name=Profile/Index';document.body.replaceChildren(frame)});
   const embedded=page.frameLocator('iframe[data-workspace-popup]');await embedded.locator('html.workspace-popup-embed').waitFor();
   check(await embedded.locator('.profile-settings>.form-panel').evaluateAll(items=>items.every((e,i)=>!i||e.getBoundingClientRect().top-items[i-1].getBoundingClientRect().bottom>=24)),'Embedded profile sections stay separated '+actor+'/'+width);
   check(await embedded.locator('body').evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Embedded profile has no overflow');
   if(actor==='admin'&&width===390)await embedded.locator('.profile-settings').screenshot({path:path.join(out,`profile-popup-${theme}.png`)});
  }
  check(errors.length===0,'No JavaScript errors');fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));
  console.log(`PASS: ${checks} follow-up notes checks across seven roles and both themes. No SQL writes.`);
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
