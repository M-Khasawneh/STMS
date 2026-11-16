// Read-only, marked in-memory fixture. Protected record endpoints remain independently authorized.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path'),crypto=require('crypto');
(async()=>{
 const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});
 let checks=0;const errors=[];const check=(ok,label)=>{if(!ok)throw Error(label);checks++};
 try{
  const context=await browser.newContext({reducedMotion:'reduce',extraHTTPHeaders:{'X-Bid-Test-Actor':'officer'}}),page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  await context.route('**/*',async r=>{
   if(r.request().method()==='POST')return r.abort();
   const url=new URL(r.request().url()),match=url.pathname.match(/^\/(Requisitions|Tenders)\/Qr\/([^/]+)$/);
   // Render-only synthetic records do not exist in the protected store. Use the real generator with their exact identity.
   if(match)return r.fulfill({response:await context.request.get(base+'/UiPagePreview/Qr?module='+match[1]+'&recordId='+match[2])});
   return r.continue();
  });
  for(const theme of ['light','dark'])for(const width of [1440,768,390,320])for(const module of ['Requisitions','Tenders']){
   await page.setViewportSize({width,height:1000});const response=await page.goto(base+'/UiPagePreview/Page?name='+module+'/Details');
   if(response.headers()['x-stms-bid-fixture']!=='memory-only')throw Error('Refusing unmarked fixture');check(response.status()===200,'Details render');
   await page.evaluate(t=>{localStorage.setItem('stms-appearance',t);window.dispatchEvent(new StorageEvent('storage',{key:'stms-appearance',newValue:t}))},theme);
   const trigger=page.locator('[data-qr-preview]');check((await trigger.getAttribute('href')).startsWith('/'+module+'/Qr/'),'Preview uses the existing protected record endpoint');
   await trigger.click();const dialog=page.locator('dialog.qr-preview');await dialog.waitFor();await dialog.locator('img').evaluate(e=>e.decode());
   check(await dialog.locator('img').evaluate(e=>e.naturalWidth>200&&e.getBoundingClientRect().width>200),'QR enlarged and decoded at '+width);
   check(await page.evaluate(()=>document.activeElement?.getAttribute('aria-label')==='Close QR preview'),'Focus moves into dialog');
   check(await dialog.locator('a[download]').getAttribute('href')===new URL(await trigger.getAttribute('href'),base).href,'Original protected download retained');
   check(await dialog.evaluate(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&r.height<=innerHeight}),'Dialog fits screen');
   if(module==='Tenders'&&(width===1440||width===390))await page.screenshot({path:path.join(out,`qr-${theme}-${width}.png`)});
   await page.keyboard.press('Tab');check(await page.evaluate(()=>document.activeElement.closest('dialog')!==null),'Keyboard focus remains in modal');
   await page.keyboard.press('Escape');check(await dialog.isHidden()&&await trigger.evaluate(e=>e===document.activeElement),'Escape closes and restores trigger focus');
   await trigger.click();await dialog.getByRole('button',{name:'Close QR preview'}).click();check(await dialog.isHidden(),'Close control works');
   check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Detail page fits');
   if(width===1440)await page.screenshot({path:path.join(out,`details-${module}-${theme}.png`),fullPage:true});
  }
  const hash=async(module,id)=>crypto.createHash('sha256').update(await (await context.request.get(base+'/UiPagePreview/Qr?module='+module+'&recordId='+id)).body()).digest('hex');
  const id='11111111-1000-4000-8000-000000000002',other='11111111-1000-4000-8000-000000000003';
  check(await hash('Tenders',id)!==await hash('Tenders',other),'Different record IDs yield different QR images');
  check(await hash('Tenders',id)!==await hash('Requisitions',id),'Different modules yield different QR images');
  check(await hash('Tenders',id)===await hash('Tenders',id),'Same record has a stable code');
  // An unavailable endpoint must show actionable feedback rather than a broken image.
  await page.route('**/Tenders/Qr/**',r=>r.fulfill({status:503,body:'Unavailable'}));await page.reload();await page.locator('[data-qr-preview]').click();
  await page.getByRole('status').filter({hasText:'could not be loaded'}).waitFor();check(await page.locator('.qr-preview-image').isHidden(),'QR load errors handled');await page.keyboard.press('Escape');
  check(errors.length===0,'No JavaScript errors');fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log(`PASS: ${checks} detail/QR preview checks; no SQL writes.`);
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
