// Local, marked memory fixtures only. Never posts to a production account or database.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
  const [base,roleBase,output]=process.argv.slice(2);
  if(!base||!roleBase||!output||!process.env.STMS_UI_TEST_PASSWORD)throw Error('Supply HTTPS experience fixture, role fixture, output, and ephemeral test password environment.');
  const browser=await chromium.launch({headless:true,channel:'msedge'});let count=0;const errors=[];
  const ok=(v,m)=>{if(!v)throw Error(m);count++;console.log('PASS: '+m)};
  try{
    fs.mkdirSync(output,{recursive:true});
    const context=await browser.newContext({ignoreHTTPSErrors:true});const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
    const first=await page.goto(base);
    if(first.headers()['x-stms-experience-fixture']!=='memory-only')throw Error('Refusing non-fixture identity host');
    for(const width of[1440,768,390,320]){
      await page.setViewportSize({width,height:1000});
      for(const route of['/','/Home/About','/Home/Contact','/Account/Login']){
        const response=await page.goto(base+route);await page.waitForTimeout(850);
        ok(response.status()===200,'public route '+route+' at '+width);
        ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'no overflow '+route+' '+width);
        ok(await page.locator('a[href="/Home/Privacy"]').count()===(route==='/'?1:0),'Privacy only on Home '+route+' '+width);
        if(route!=='/Account/Login'){
          if(width<=768){const toggle=page.locator('.home-menu-toggle');if(await toggle.isVisible()){await toggle.click();ok(await toggle.getAttribute('aria-expanded')==='true','mobile public menu opens');await page.keyboard.press('Escape');ok(await toggle.getAttribute('aria-expanded')==='false','mobile public menu closes');}}
          ok(await page.locator('#home-navigation a[href="/Home/About"]').count()===1&&await page.locator('#home-navigation a[href="/Home/Contact"]').count()===1,'dedicated public navigation destinations');
        }else ok(await page.locator('.login-registration-button').isVisible(),'registration call to action visible');
        if((width===1440||width===390)&&(route==='/'||route==='/Account/Login'))await page.screenshot({path:path.join(output,(route==='/'?'home':'login')+'-'+width+'.png'),fullPage:true});
      }
    }
    await page.setViewportSize({width:1440,height:1000});await page.goto(base+'/Account/Login');
    await page.locator('[name=UserName]').fill('admin');await page.locator('[name=Password]').fill(process.env.STMS_UI_TEST_PASSWORD);
    await Promise.all([page.waitForURL('**/Workspace'),page.locator('form button[type=submit]').click()]);
    await page.goto(base+'/Employees');
    ok(await page.locator('tbody tr').filter({hasText:/\badmin\b/}).count()===1,'only ordinary department-admin remains; built-in admin hidden');
    ok(await page.locator('tbody tr td').filter({hasText:/^admin$/}).count()===0,'built-in admin absent from Employees');
    await page.getByRole('link',{name:'Create employee'}).click();
    let frame=page.frameLocator('dialog.workspace-popup iframe');await frame.locator('.role-picker').waitFor();
    await frame.locator('.role-picker summary').click();
    await frame.locator('[data-role-option][value=RequestingDepartment]').check();await frame.locator('[data-role-option][value=SourcingOfficer]').check();
    ok(await frame.locator('select[name=Roles]').evaluate(e=>[...e.selectedOptions].map(x=>x.value).includes('SourcingOfficer')),'dropdown syncs original MVC multi-select');
    ok(await frame.locator('[data-role-option][value=Supplier]').count()===0,'Supplier excluded from employee role picker');
    page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'Close form',exact:true}).click();
    await page.locator('.workspace-profile-link').click();await frame.locator('[name="Password.CurrentPassword"]').waitFor();
    ok(await frame.locator('[name="Password.NewPassword"]').getAttribute('type')==='password'&&await frame.locator('[name="Password.ConfirmPassword"]').count()===1,'profile offers protected current/new/confirmation password fields');
    for(const width of[1440,768,390,320]){
      await page.setViewportSize({width,height:1000});ok(await frame.locator('html').evaluate(e=>e.scrollWidth<=innerWidth),'profile popup fits '+width);
      if(width===1440||width===390)await page.screenshot({path:path.join(output,'profile-'+width+'.png'),fullPage:true});
    }
    await page.getByRole('button',{name:'Close form',exact:true}).click();
    const roles=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'anonymous'}});const rp=await roles.newPage();rp.on('pageerror',e=>errors.push(e.message));
    if((await rp.goto(roleBase)).headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing non-fixture role host');
    for(const[actor,title]of Object.entries({admin:'Administration',requester:'My requisitions',supplier:'Supplier workspace',officer:'Sourcing operations',manager:'Sourcing management',evaluator:'Evaluation committee',top:'Top committee'})){
      await roles.setExtraHTTPHeaders({'X-Bid-Test-Actor':actor,'X-Admin-Test-State':'populated'});
      for(const width of[1440,390,320]){
        await rp.setViewportSize({width,height:1000});await rp.goto(roleBase+'/Workspace');await rp.waitForTimeout(850);
        ok(await rp.locator('body.workspace-theme').count()===1&&await rp.locator('.stms-landing').count()===0,'shared workspace identity '+actor+' '+width);
        ok((await rp.locator('main').innerText()).includes(title),'role-specific dashboard '+actor);
        ok(await rp.locator('.notification-dropdown > summary.workspace-notification-bell').count()===1&&await rp.locator('.sidebar a[href="/Notifications"]').count()===0,'notifications in header icon '+actor);
        ok(await rp.locator('a[href="/Home/Privacy"]').count()===0,'no workspace Privacy link '+actor);
        ok(await rp.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'role dashboard fits '+actor+' '+width);
        if(width===390){await rp.locator('.menu-toggle').click();await rp.keyboard.press('Escape');ok(await rp.locator('.menu-toggle').getAttribute('aria-expanded')==='false','keyboard mobile navigation '+actor);}
        if(width===1440)await rp.screenshot({path:path.join(output,'dashboard-'+actor+'.png'),fullPage:true});
      }
    }
    await rp.emulateMedia({reducedMotion:'reduce'});await rp.goto(roleBase+'/Workspace');
    ok(await rp.evaluate(()=>!document.getAnimations().some(a=>a.playState==='running')),'workspace reduced motion');
    await page.emulateMedia({reducedMotion:'reduce'});await page.goto(base);await page.waitForTimeout(400);
    ok(await page.evaluate(()=>!document.getAnimations().some(a=>a.playState==='running')),'homepage reduced motion');
    ok(errors.length===0,'zero JavaScript errors');
    fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({checks:count,errors},null,2));console.log('PASS: '+count+' experience browser checks');
  }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
