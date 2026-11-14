// Presentation-only marked memory host; never target production or SQL-backed hosts.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const [base,out]=process.argv.slice(2);const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try {
  fs.mkdirSync(out,{recursive:true});const context=await browser.newContext({extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}});const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  const route='/Clarifications/Details/6fc71a15-682b-435a-a1b8-2e570b7af02d';
  for(const width of [1440,768,390,320]){
   await page.setViewportSize({width,height:1000});const response=await page.goto(base+route);
   if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing non-fixture server');
   check(response.status()===200,'Compiled details renders');
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'No horizontal overflow '+width);
   check(await page.locator('#RecipientId').isVisible(),'Accessible recipient field');
   check((await page.locator('.clarification-thread').innerText()).includes('<script>private question</script>'),'Question is displayed as plain text');
   check(await page.locator('.clarification-thread script').count()===0,'No executed user markup');
   check(await page.locator('form[action*=Ask] input[name=__RequestVerificationToken]').count()===1,'Antiforgery form');
   await page.locator('#Subject').focus();check(await page.locator('#Subject').evaluate(e=>e===document.activeElement),'Keyboard focus');
   if(width<=768){const toggle=page.locator('.menu-toggle');await toggle.click();check(await toggle.getAttribute('aria-expanded')==='true','Mobile sidebar opens');await page.keyboard.press('Escape');check(await toggle.getAttribute('aria-expanded')==='false','Escape closes navigation');}
   if(width===1440||width===390)await page.screenshot({path:path.join(out,'clarifications-'+width+'.png'),fullPage:true});
  }
  await page.emulateMedia({reducedMotion:'reduce'});await page.reload();check(await page.locator('.clarification-thread').first().evaluate(e=>getComputedStyle(e).animationName==='none'),'Reduced-motion fallback');
  await page.locator('#RecipientId').selectOption('evaluator');await page.locator('#Subject').fill('Memory-only question');await page.locator('#Description').fill('Please clarify the documented requirement.');
  await Promise.all([page.waitForNavigation(),page.getByRole('button',{name:'Send clarification',exact:true}).click()]);check((await page.locator('.alert-success').innerText()).includes('sent'),'Question PRG confirmation');
  await page.setExtraHTTPHeaders({'X-Bid-Test-Actor':'evaluator'});await page.goto(base+route);
  const question=page.locator('.clarification-thread').filter({has:page.getByRole('heading',{name:'Memory-only question',exact:true})});await question.locator('textarea[name=Response]').fill('Recorded response for the question.');
  await Promise.all([page.waitForNavigation(),question.getByRole('button',{name:'Send final response'}).click()]);
  check(await page.locator('.clarification-thread').filter({has:page.getByRole('heading',{name:'Memory-only question',exact:true})}).locator('textarea').count()===0,'Answered question becomes immutable');
  await page.setExtraHTTPHeaders({'X-Bid-Test-Actor':'officer'});await page.goto(base+route);
  const answered=page.locator('.clarification-thread').filter({has:page.getByRole('heading',{name:'Memory-only question',exact:true})});await Promise.all([page.waitForNavigation(),answered.getByRole('button',{name:'Mark resolved'}).click()]);
  check((await page.locator('.clarification-thread').filter({has:page.getByRole('heading',{name:'Memory-only question',exact:true})}).innerText()).includes('Resolved'),'Sender resolves answered thread');
  for(const actor of ['admin','supplier','requester','manager','top']){const response=await context.request.get(base+route,{headers:{'X-Bid-Test-Actor':actor}});check(response.status()===403,'Nonparticipant denied '+actor);}
  check(errors.length===0,'No JavaScript errors');fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log('PASS: '+checks+' internal clarification browser checks (memory presentation fixture).');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
