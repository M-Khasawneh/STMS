// Deterministic UI checks: test datasets only, guarded by the memory-only fixture header.
const {chromium} = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs=require('fs'), path=require('path');
(async()=>{
  const [base,output]=process.argv.slice(2);if(!base||!output)throw Error('Usage: node tests/admin-dashboard-browser.cjs fixture-url output-directory');
  const browser=await chromium.launch({headless:true,channel:'msedge'});let page,checks=0;const errors=[];
  const ok=(v,m)=>{if(!v)throw Error(m);checks++;console.log('PASS: '+m)};
  try{
    fs.mkdirSync(output,{recursive:true});
    const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'admin','X-Admin-Test-State':'populated'}});
    page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
    const response=await page.goto(base+'/Workspace');
    if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Not an isolated test fixture');
    ok(response.status()===200,'Admin route renders');
    ok(await page.locator('[data-admin-overview]').count()===1&&await page.locator('.workspace-welcome').count()===0,'new overview replaces oversized welcome');
    ok(await page.locator('[data-metric-value]').count()===6,'six entity/message KPI cards');
    ok((await page.locator('[data-metric=people] [data-metric-value]').textContent()).trim()==='42','KPI reflects the test service result');
    ok(await page.locator('.admin-queue-row').count()===4,'pending operations retain read-only oversight');
    ok(await page.locator('.admin-distribution').count()===3,'three existing workflow distributions');
    ok(await page.locator('.admin-activity tbody tr').count()===3,'existing audit metadata rendered');
    ok(await page.locator('.admin-header-identity').textContent().then(t=>t.includes('admin')&&t.includes('Administrator')),'real authenticated identity displayed');
    ok(await page.locator('.sidebar a[aria-current=page]').count()===1,'one accessible active navigation state');
    ok(await page.locator('.sidebar form[action="/Account/Logout"] input[name=__RequestVerificationToken]').count()===1,'logout and antiforgery preserved');
    const targets={people:'/Employees',department:'/Departments',supplier:'/Suppliers',request:'/Requisitions',tender:'/Tenders'};
    for(const [key,target]of Object.entries(targets))ok(await page.locator('[data-metric='+key+']').getAttribute('href')===target,'working route '+target);
    for(const width of[1440,1280,1024,768,390,320]){
      await page.setViewportSize({width,height:1000});await page.goto(base+'/Workspace');await page.waitForTimeout(1000);
      ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'no page overflow '+width+'px');
      ok(await page.locator('h1').isVisible(),'heading readable '+width+'px');
      if(width===1440||width===390)await page.screenshot({path:path.join(output,'admin-'+width+'.png'),fullPage:true});
    }
    await page.locator('.menu-toggle').click();ok(await page.locator('.menu-toggle').getAttribute('aria-expanded')==='true','mobile drawer opens');
    await page.keyboard.press('Escape');ok(await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false','mobile drawer closes with Escape');
    await page.locator('[data-metric=people]').focus();ok(await page.locator('[data-metric=people]').evaluate(e=>getComputedStyle(e).outlineStyle!=='none'),'visible keyboard focus');
    await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin','X-Admin-Test-State':'empty'});await page.goto(base+'/Workspace');
    ok((await page.locator('[data-metric-value]').allTextContents()).every(v=>v.trim()==='0'),'empty data displays actual zero totals');
    ok(await page.getByText('No records yet.',{exact:true}).count()===3,'empty distribution states');
    ok(await page.getByRole('heading',{name:'No recorded activity yet'}).count()===1,'no fabricated activity');
    await page.screenshot({path:path.join(output,'admin-empty.png'),fullPage:true});
    await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin','X-Admin-Test-State':'unavailable'});await page.goto(base+'/Workspace');
    ok((await page.locator('[data-metric-value]').allTextContents()).every(v=>v.trim()==='—'),'unavailable data never becomes fake zero');
    ok(await page.locator('[role=alert]').filter({hasText:'temporarily unavailable'}).count()===1,'data failure announced accessibly');
    await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin','X-Admin-Test-State':'populated'});await page.emulateMedia({reducedMotion:'reduce'});await page.goto(base+'/Workspace');
    ok(await page.evaluate(()=>!document.getAnimations().some(a=>a.playState==='running')),'reduced-motion disables entrance animations');
    await page.goto(base+'/Departments');ok(await page.locator('body.admin-workspace').count()===1&&await page.locator('link[href*="admin-shell.css"]').count()===1&&await page.locator('link[href*="admin-dashboard.css"]').count()===0,'Admin pages share dashboard identity without dashboard widgets');
    for(const actor of['supplier','requester','officer','manager','evaluator','top']){
      await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor});await page.goto(base+'/Workspace');
      ok(await page.locator('[data-admin-overview]').count()===0&&await page.locator('[data-role-dashboard]').count()===1,'non-Admin workspace preserved: '+actor);
    }
    ok(errors.length===0,'no JavaScript errors');
    fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({checks,errors,data:'Synthetic test fixtures only; production dashboard queries real SQL data.'},null,2));
    console.log('PASS: '+checks+' Admin dashboard browser checks');
  }catch(e){console.error('Browser errors:',errors);if(page)await page.screenshot({path:path.join(output,'failure.png'),fullPage:true}).catch(()=>{});throw e}finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
