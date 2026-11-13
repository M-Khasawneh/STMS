// Only runs against the explicitly marked memory-only UI fixture; never targets production records.
const {chromium} = require(process.env.STMS_PLAYWRIGHT || 'playwright');
const fs = require('fs'), path = require('path'), crypto = require('crypto');
(async () => {
  const [base, tenderId, output] = process.argv.slice(2);
  if (!base || !tenderId || !output) throw Error('Usage: node tests/workspace-ui-browser.cjs fixture-url tender-id output-directory');
  const browser = await chromium.launch({headless:true, channel:'msedge'});
  let page, count=0; const errors=[];
  const departmentName='UI Popup Department '+crypto.randomBytes(3).toString('hex');
  const employeeName='ui-popup-'+crypto.randomBytes(4).toString('hex');
  const ok=(v,m)=>{if(!v)throw Error(m);count++;console.log('PASS: '+m)};
  try {
    fs.mkdirSync(output,{recursive:true});
    const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'anonymous'}});
    page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
    const first=await page.goto(base);
    if(first.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing to mutate a non-fixture host');
    ok(await page.locator('.stms-landing').count()===1,'anonymous public Home preserved');
    for(const role of ['admin','supplier','requester','officer','manager','evaluator','top']){
      await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':role});await page.goto(base);
      ok(new URL(page.url()).pathname==='/Workspace',role+' opens internal dashboard');
      ok(await page.locator('.stms-landing').count()===0&&await page.locator(role==='admin'?'[data-admin-overview]':'.workspace-dashboard').count()===1,role+' has no public Home inside dashboard');
      ok(await page.locator('.side-link').filter({hasText:/^Home$/}).count()===0,role+' has Dashboard navigation');
    }
    await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin'});await page.goto(base+'/Workspace');
    await page.screenshot({path:path.join(output,'admin-dashboard.png'),fullPage:true});
    await page.goto(base+'/Departments');await page.getByRole('link',{name:'Create department'}).click();
    const popup=page.locator('dialog.workspace-popup'), frame=page.frameLocator('dialog.workspace-popup iframe');
    await frame.locator('html.workspace-popup-embed').waitFor();
    ok(await popup.evaluate(e=>e.open),'department form opens in modal');
    ok(new URL(page.url()).pathname==='/Departments','form retains list context');
    ok(await frame.locator('.sidebar').isHidden()&&await frame.locator('.topbar').isHidden(),'popup has no duplicate workspace shell');
    ok(await frame.locator('input[name=__RequestVerificationToken]').count()>0,'original antiforgery token preserved');
    await frame.getByRole('button',{name:'Create department'}).click();
    ok(await frame.locator('.field-validation-error').count()>0,'client validation remains inside popup');
    await frame.locator('[name=Name]').fill('   ');
    const child=page.frames().find(f=>f!==page.mainFrame()&&f.url().includes('/Departments/Create'));
    await Promise.all([child.waitForNavigation(),frame.locator('main form').evaluate(f=>HTMLFormElement.prototype.submit.call(f))]);
    await frame.locator('.field-validation-error').waitFor();
    ok(await popup.evaluate(e=>e.open),'server validation redisplays inside popup');
    await frame.locator('[name=Name]').fill(departmentName);
    await Promise.all([page.waitForNavigation(),frame.getByRole('button',{name:'Create department'}).click()]);
    ok(await page.getByText(departmentName,{exact:true}).count()===1&&await page.locator('dialog[open]').count()===0,'save closes popup and refreshes list');
    const row=page.locator('tbody tr').filter({hasText:departmentName});
    await row.getByRole('link',{name:/Edit/}).click();await frame.locator('[name=Name]').fill('Unsaved change');
    page.once('dialog',d=>d.dismiss());await frame.locator('[name=Name]').press('Escape');
    ok(await popup.evaluate(e=>e.open),'Escape respects cancelled discard prompt');
    page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'Close form',exact:true}).click();
    ok(await page.locator('dialog[open]').count()===0&&await page.getByText(departmentName,{exact:true}).count()===1,'discard does not save edits');
    ok(await row.getByRole('link',{name:/Edit/}).evaluate(e=>e===document.activeElement),'focus returns to launcher');
    await page.goto(base+'/Employees');await page.getByRole('link',{name:'Create employee'}).click();
    await frame.locator('html.workspace-popup-embed').waitFor();
    ok(await frame.locator('[name=Roles] option[value=Supplier]').count()===0&&await frame.locator('[name=Roles] option').count()>1,'employee role controls preserved without Supplier');
    await frame.locator('[name=UserName]').fill(employeeName);await frame.locator('[name=DepartmentId]').selectOption({label:'Administration'});
    await frame.locator('[name=Roles]').selectOption('RequestingDepartment');
    const password='A1!'+crypto.randomBytes(18).toString('hex');
    await frame.locator('[name=Password]').fill(password);await frame.locator('[name=ConfirmPassword]').fill(password);
    await Promise.all([page.waitForNavigation(),frame.getByRole('button',{name:'Create employee',exact:true}).click()]);
    ok(await page.getByText(employeeName,{exact:true}).count()===1,'employee saves through existing MVC service');
    const employeeRow=page.locator('tbody tr').filter({hasText:employeeName});
    await page.goto(base+await employeeRow.getByRole('link',{name:/Edit/}).getAttribute('href'));
    ok(await page.locator('form[data-popup-inline]').isHidden(),'inline action becomes popup launcher');
    await page.getByRole('button',{name:'Disable employee',exact:true}).click();await frame.locator('.workspace-inline-form').waitFor();
    ok(await frame.locator('main form').count()===1,'inline popup isolates exact original form');
    await Promise.all([page.waitForNavigation(),frame.getByRole('button',{name:'Disable employee',exact:true}).click()]);
    ok(await page.getByRole('button',{name:'Enable employee',exact:true}).count()===1,'inline decision refreshes original details');
    for(const width of [1440,768,390,320]){
      await page.setViewportSize({width,height:1000});await page.goto(base+'/Employees');await page.getByRole('link',{name:'Create employee'}).click();
      await frame.locator('html.workspace-popup-embed').waitFor();
      ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'parent fits '+width+'px');
      ok(await frame.locator('html').evaluate(e=>e.scrollWidth<=innerWidth),'employee popup fits '+width+'px');
      if(width===1440||width===390){await page.waitForTimeout(800);await page.screenshot({path:path.join(output,'employee-popup-'+width+'.png'),fullPage:true});}
      await page.getByRole('button',{name:'Close form',exact:true}).click();
    }
    await context.setExtraHTTPHeaders({'X-Bid-Test-Actor':'supplier'});await page.goto(base+'/Bids');
    // Emulate the real Accepted-invitation link without invoking the SQL-backed Tender controller in this fixture.
    await page.evaluate(url=>{const a=document.createElement('a');a.href=url;a.id='fixture-invitation';a.textContent='Prepare offer';document.querySelector('main').append(a)},`${base}/Bids/Create?tenderId=${tenderId}`);
    await page.locator('#fixture-invitation').click();await frame.locator('[data-bid-editor]').waitFor();
    ok(await popup.evaluate(e=>e.open)&&await frame.locator('[data-bid-line]').count()===2,'supplier popup includes every required item');
    await frame.locator('[name="Form.DeliveryTimeframe"]').fill('Two weeks');await frame.locator('[name="Form.TechnicalDetails"]').fill('All requested items');
    await frame.locator('[name="Form.PaymentTerms"]').fill('30 days');await frame.locator('[name="Form.OfferValidUntil"]').fill('2026-12-01');
    for(const input of await frame.locator('[data-unit-price]').all())await input.fill('2');
    await frame.locator('[data-save-draft]').click();await frame.locator('h1').filter({hasText:'Edit draft'}).waitFor();
    ok(await popup.evaluate(e=>e.open)&&new URL(page.url()).pathname==='/Bids','draft save-to-edit remains inside popup');
    const upload=frame.locator('form[action*="/Upload/"]');
    await upload.locator('input[type=file]').setInputFiles({name:'technical.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4\nprivate\n%%EOF')});
    await upload.getByRole('button',{name:'Upload document'}).click();await frame.getByRole('heading',{name:'technical.pdf'}).waitFor();
    ok(await popup.evaluate(e=>e.open),'private multipart upload remains inside popup');
    const download=page.waitForEvent('download');await frame.getByRole('link',{name:'Download private file'}).click();const file=await download;
    ok(file.suggestedFilename()==='technical.pdf'&&!(await file.failure()),'private download works inside popup');
    await frame.locator('[data-leave-editor]').click();await frame.locator('[data-bid-submit]').waitFor();await frame.locator('#confirm-offer').check();
    await Promise.all([page.waitForNavigation(),frame.locator('[data-bid-submit] button').click()]);
    ok(await page.locator('dialog[open]').count()===0&&(await page.locator('.bid-status').textContent()).trim()==='Submitted','final submission closes popup and updates My Offers');
    await page.emulateMedia({reducedMotion:'reduce'});await page.goto(base+'/Workspace');
    ok(await page.evaluate(()=>!document.getAnimations().some(a=>a.playState==='running')),'reduced motion preserved');
    ok(errors.length===0,'no JavaScript errors');
    fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({checks:count,errors},null,2));console.log('PASS: '+count+' workspace popup/browser checks');
  }catch(e){console.error('Browser errors:',errors);if(page)await page.screenshot({path:path.join(output,'failure.png'),fullPage:true}).catch(()=>{});throw e;}
  finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
