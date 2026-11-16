// Checks shared styling only, using the existing memory-only test host. No production database access.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');const fs=require('fs'),path=require('path');
const {settled}=require('./browser-waits.cjs');
(async()=>{
  const[base,output]=process.argv.slice(2);if(!base||!output)throw Error('Usage: node tests/admin-shell-browser.cjs fixture-url output-directory');
  const browser=await chromium.launch({headless:true,channel:'msedge'});let page,count=0;const errors=[];
  const ok=(v,m)=>{if(!v)throw Error(m);count++;console.log('PASS: '+m)};
  try{
    fs.mkdirSync(output,{recursive:true});
    const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'admin','X-Admin-Test-State':'populated'}});
    page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
    const response=await page.goto(base+'/Workspace');if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Not a memory-only fixture');
    const colors=()=>page.evaluate(()=>({body:getComputedStyle(document.body).backgroundColor,sidebar:getComputedStyle(document.querySelector('.sidebar')).backgroundColor,header:getComputedStyle(document.querySelector('.topbar')).backgroundColor,accent:getComputedStyle(document.body).getPropertyValue('--accent').trim()}));
    const reference=await colors();
    for(const[route,label]of[['/Departments','Departments'],['/Employees','Employees'],['/Workspace','Dashboard']]){
      await page.locator('.sidebar a.side-link[href="'+route+'"]').click();await page.waitForURL(base+route);await settled(page);
      ok(JSON.stringify(await colors())===JSON.stringify(reference),'same dashboard colors after navigating to '+label);
      ok(await page.locator('.sidebar a[aria-current=page]').textContent().then(t=>t.trim()===label),'active navigation: '+label);
      ok(await page.locator('.admin-header-identity').isVisible(),'same identity/header: '+label);
      if(label!=='Dashboard')ok(await page.locator('link[href*="admin-dashboard.css"]').count()===0,'no dashboard widgets/styles in '+label);
      await page.screenshot({path:path.join(output,label.toLowerCase()+'-desktop.png'),fullPage:true});
    }
    await page.goto(base+'/Home/Privacy');ok(JSON.stringify(await colors())===JSON.stringify(reference),'Privacy retains the Admin shell');
    for(const width of[1440,768,390,320]){
      await page.setViewportSize({width,height:1000});await page.goto(base+'/Departments');await settled(page);
      ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Admin listing fits '+width+'px');
      await page.mouse.move(0,0); await settled(page);
      const primary = page.getByRole('link',{name:'Create department'});
      const palette = await primary.evaluate(e=>{const s=getComputedStyle(e);return {background:s.backgroundColor,color:s.color,border:s.borderColor}});
      await primary.click();const frame=page.frameLocator('dialog.workspace-popup iframe');
      await frame.locator('html.workspace-popup-embed').waitFor();
      ok(await frame.locator('.sidebar').isHidden()&&await frame.locator('.topbar').isHidden(),'popup has no duplicate Admin shell '+width+'px');
      ok(await frame.locator('html').evaluate(e=>e.scrollWidth<=innerWidth),'Admin popup fits '+width+'px');
      ok(await frame.locator('main button[type=submit]').evaluate((e,p)=>{const s=getComputedStyle(e);return s.backgroundColor===p.background&&s.color===p.color&&s.borderColor===p.border},palette),'popup primary action matches the current shared brand control');
      if(width===390){await settled(page);await page.screenshot({path:path.join(output,'department-popup-mobile.png'),fullPage:true});}
      await page.getByRole('button',{name:'Close form',exact:true}).click();
    }
    await page.locator('.menu-toggle').click();ok(await page.locator('.menu-toggle').getAttribute('aria-expanded')==='true','mobile menu still opens');await page.keyboard.press('Escape');ok(await page.locator('.menu-toggle').getAttribute('aria-expanded')==='false','mobile menu still closes');
    await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'supplier'});await page.goto(base+'/Workspace');
    ok(await page.locator('link[href*="admin-shell.css"]').count()===1&&await page.locator('body.admin-workspace').count()===0,'shared identity preserves role separation');
    ok(errors.length===0,'no JavaScript errors');fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({checks:count,errors},null,2));console.log('PASS: '+count+' shared Admin shell checks');
  }catch(e){if(page)await page.screenshot({path:path.join(output,'failure.png'),fullPage:true}).catch(()=>{});throw e}finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
