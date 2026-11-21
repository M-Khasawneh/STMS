// Memory-only MVC fixture: never signs in to or writes to the production database.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const [base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[],panels=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++;};
 try {
  const context=await browser.newContext({colorScheme:'light',reducedMotion:'reduce',extraHTTPHeaders:{'X-Bid-Test-Actor':'admin'}});
  const page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));
  await context.route('**/*',route=>route.request().method()==='POST'?route.abort():route.continue());
  const visit=async route=>{const response=await page.goto(base+route);check(response.status()===200&&response.headers()['x-stms-bid-fixture']==='memory-only','Marked memory fixture '+route);};
  await visit('/Workspace');
  const toggle=page.locator('[data-appearance-toggle]');
  check(await toggle.isVisible(),'Appearance toggle is visible');
  check(await page.locator('html').getAttribute('data-theme')==='light','System light preference');
  await toggle.click();
  check(await toggle.getAttribute('aria-pressed')==='true','Dark toggle state');
  check(await page.evaluate(()=>localStorage.getItem('stms-appearance'))==='dark','Preference persists');
  for(const actor of ['admin','supplier','requester','officer','manager','evaluator','top']) {
   await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});await visit('/Workspace');
   check(await page.locator('html').getAttribute('data-theme')==='dark','Theme persists '+actor);
   for(const width of [1440,768,390,320]) {
    await page.setViewportSize({width,height:1000});
    check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'No overflow '+actor+'/'+width);
    if(width===1440||width===390)await page.screenshot({path:path.join(out,actor+'-dark-'+width+'.png'),fullPage:true});
   }
  }
  const pages=[['requester','Requisitions/Index'],['requester','Requisitions/Create'],['requester','Requisitions/Edit'],['requester','Requisitions/Details'],['officer','Tenders/Index'],['officer','Tenders/Create'],['officer','Tenders/Edit'],['officer','Tenders/Details'],['officer','Tenders/Criteria'],['supplier','Tenders/Inbox'],['supplier','Tenders/Invitation'],['officer','Tenders/Invitations'],['admin','Suppliers/Index'],['admin','Suppliers/Details'],['supplier','Suppliers/Status'],['requester','Profile/Index'],['requester','Passkeys/Index'],['manager','Committees/Edit'],['manager','UnifiedReports/Review'],['requester','Shared/Error'],['requester','Shared/WorkspaceProblem'],['requester','Account/AccessDenied'],...['AiAnalysis','Bids','Clarifications','Committees','Compliance','Evaluations','Passkeys','Reports','ReviewBids','Tenders','TopCommittee','UnifiedReports'].map(c=>[c==='Bids'?'supplier':'officer',c+'/Problem'])];
  pages.push(['admin','Employees/Create'],['admin','Employees/Edit'],['admin','Departments/Create'],['admin','Departments/Edit'],['admin','Suppliers/Register']);
  for(const [actor,name] of pages) {
   await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});await visit('/UiPagePreview/Page?name='+encodeURIComponent(name));
   check(await toggle.count()===1,'One appearance control '+name);
   check(!await page.locator('script[src*="face-api"]').count(),'Face library is not globally loaded '+name);
   check(await page.locator('main .form-section').evaluateAll(es=>es.every(e=>getComputedStyle(e).backgroundColor===getComputedStyle(document.documentElement).getPropertyValue('--st-panel').trim()||getComputedStyle(e).backgroundColor==='rgb(24, 34, 45)')),'Fieldsets use the dark surface '+name);
   check(await page.locator('main .role-picker summary').evaluateAll(es=>es.every(e=>getComputedStyle(e).color==='rgb(231, 237, 243)')),'Role selection text stays readable '+name);
   panels.push(...await page.evaluate(()=>[...document.querySelectorAll('main *')].filter(e=>{const r=e.getBoundingClientRect();return r.width>150&&r.height>80&&['rgb(255, 255, 255)','rgb(255, 254, 250)','rgb(246, 243, 237)'].includes(getComputedStyle(e).backgroundColor)}).map(e=>e.className)));
   await page.screenshot({path:path.join(out,name.replace('/','-')+'-dark.png'),fullPage:true});
  }
  check(panels.length===0,'No untokenized bright panels in dark workspace: '+panels.join(','));
  await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin'});
  await visit('/UiPagePreview/Page?name=Departments%2FCreate');
  await page.evaluate(()=>{const frame=document.createElement('iframe');frame.dataset.workspacePopup='true';frame.id='dark-form-fixture';frame.src='/UiPagePreview/Page?name=Employees%2FCreate';document.body.append(frame);});
  const popup=page.frameLocator('#dark-form-fixture');
  await popup.locator('html.workspace-popup-embed').waitFor();
  check(await popup.locator('.form-section').first().evaluate(e=>getComputedStyle(e).backgroundColor==='rgb(24, 34, 45)'),'Popup iframe retains dark fieldsets');
  await page.locator('#dark-form-fixture').evaluate(e=>e.remove());
  await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin'});
  for(const name of ['Suppliers/Index','Requisitions/Index','Tenders/Index']) {
   await visit('/UiPagePreview/Page?name='+encodeURIComponent(name)+'&state=empty');
   const contrast=await page.locator('.workspace-empty-state').evaluate(panel=>{
    const lum=color=>{const a=color.match(/[\d.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);return a[0]*.2126+a[1]*.7152+a[2]*.0722;};
    const surface=getComputedStyle(panel).backgroundColor,b=lum(surface);
    return {opaque:surface.startsWith('rgb('),minimum:Math.min(...[...panel.querySelectorAll('h2,p')].map(e=>{const t=lum(getComputedStyle(e).color);return (Math.max(t,b)+.05)/(Math.min(t,b)+.05);}))};
   });
   check(contrast.opaque&&contrast.minimum>=4.5,'Readable dark empty state '+name);
  }
  await page.setViewportSize({width:390,height:1000});
  const menu=page.locator('.menu-toggle');await menu.click();
  check(await menu.getAttribute('aria-expanded')==='true','Mobile drawer opens');
  await page.keyboard.press('Escape');check(await menu.getAttribute('aria-expanded')==='false','Mobile drawer Escape');
  await toggle.click();check(await page.locator('html').getAttribute('data-theme')==='light','Switch back to light');
  await visit('/UiPagePreview/Page?name=Departments%2FCreate');
  check(await page.locator('.form-section').first().evaluate(e=>getComputedStyle(e).backgroundColor==='rgb(255, 255, 255)'),'Light fieldsets retain their white surface');
  await page.screenshot({path:path.join(out,'workspace-light.png'),fullPage:true});
  await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'anonymous'});await visit('/Account/Login');
  check(await page.locator('.public-topbar>[data-appearance-toggle]').isVisible(),'Mobile public appearance remains available outside navigation');
  await page.locator('.home-menu-toggle').click();await toggle.click();check(await page.locator('html').getAttribute('data-theme')==='dark','Public theme toggle');
  await page.locator('.home-menu-toggle').click();
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Mobile login no overflow');
  await page.screenshot({path:path.join(out,'login-dark-mobile.png'),fullPage:true});
  await page.setViewportSize({width:1440,height:1000});
  check(await page.locator('.login-composition').evaluate(e=>e.clientWidth>900),'Desktop login uses both columns');
  check(await page.locator('#UserName').evaluate(e=>getComputedStyle(e).backgroundColor!=='rgb(255, 255, 255)'),'Dark login field uses theme surface');
  check(await page.locator('.login-panel .btn-primary').evaluate(e=>getComputedStyle(e).color!==getComputedStyle(e).backgroundColor),'Login button remains visible in dark mode');
  await page.screenshot({path:path.join(out,'login-dark-desktop.png'),fullPage:true});
  await toggle.click();await page.screenshot({path:path.join(out,'login-light-desktop.png'),fullPage:true});
  check(errors.length===0,'No JavaScript errors: '+errors.join('; '));
  fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors,panels},null,2));console.log('PASS: '+checks+' appearance and responsive checks; no SQL writes.');
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exit(1)});
