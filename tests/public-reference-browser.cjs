// Compiled Razor / memory-only fixture. Never posts to SQL-backed endpoints.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');const fs=require('fs'),path=require('path');
(async()=>{const[base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
const check=(ok,message)=>{if(!ok)throw Error(message);checks++;};
try{const c=await browser.newContext({colorScheme:'light',reducedMotion:'reduce',extraHTTPHeaders:{'X-Bid-Test-Actor':'anonymous'}});const p=await c.newPage();p.on('pageerror',e=>errors.push(e.message));
for(const theme of ['light','dark']){await c.addInitScript(theme=>localStorage.setItem('stms-appearance',theme),theme);
for(const width of[1440,1024,768,390,320]){await p.setViewportSize({width,height:1000});for(const route of['/','/Home/About','/Home/Contact','/Account/Login','/Suppliers/Register']){const response=await p.goto(base+route);check(response.headers()['x-stms-bid-fixture']==='memory-only'&&response.status()===200,'Marked public route '+route);
check(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Responsive '+theme+'/'+width+route);
check(await p.locator('.public-topbar [data-appearance-toggle]').isVisible(),'Appearance available without opening mobile menu');
check(await p.locator('main h1').count()===1,'One meaningful page heading '+route);
check(await p.locator('.public-footer').count()===1,'One real footer');
check(await p.locator('svg.stms-link-arrow[aria-hidden=true]').count()>0,'Vector link arrows render without emoji glyphs');
if(width<=1024){
const menu=p.locator('.home-menu-toggle');await menu.click();await p.locator('#home-navigation').waitFor({state:'visible'});
const contrast=await p.locator('#home-navigation').evaluate(nav=>{
const luminance=color=>{const values=color.match(/[\d.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);return values[0]*.2126+values[1]*.7152+values[2]*.0722;};
const background=luminance(getComputedStyle(nav).backgroundColor);
return [...nav.querySelectorAll('a')].map(a=>{const style=getComputedStyle(a),text=luminance(style.color),surface=style.backgroundColor==='rgba(0, 0, 0, 0)'?background:luminance(style.backgroundColor);return (Math.max(text,surface)+.05)/(Math.min(text,surface)+.05);});
});check(contrast.every(value=>value>=4.5),'Mobile navigation contrast '+theme+'/'+width+route);
await p.keyboard.press('Escape');check(await menu.getAttribute('aria-expanded')==='false','Mobile menu closes with Escape');
}
if(route==='/'&&width<=768){
const flow=await p.locator('.lp-workflow-intro').evaluate(e=>({position:getComputedStyle(e).position,bottom:e.getBoundingClientRect().bottom,next:document.querySelector('.lp-steps>li').getBoundingClientRect().top}));
check(flow.position==='static'&&flow.bottom<=flow.next,'Mobile workflow introduction does not overlap the first stage');
}
if(route==='/'){check((await p.locator('h1').textContent()).replace(/\s+/g,' ').trim()==='Unified Platform for Sourcing & Tender Management','Required headline preserved');check(await p.locator('[data-journey-step]').count()===6&&await p.locator('.lp-steps>li').count()===6,'Six summary and detailed stages');check(await p.locator('.ref-hero-copy img').count()===0,'No logo in hero');check((await p.locator('.lp-metrics strong').allTextContents()).every(v=>v==='Unavailable'),'Unavailable fixture metrics remain unknown, never demo numbers');
await p.locator('[data-journey-step="3"]').click();check(await p.locator('[data-journey-step="3"]').getAttribute('aria-pressed')==='true','Workflow selection is functional');await p.keyboard.press('ArrowRight');check(await p.locator('[data-journey-step="4"]').getAttribute('aria-pressed')==='true','Workflow keyboard movement');await p.locator('[data-journey-reset]').click();check(await p.locator('[data-journey-step="0"]').getAttribute('aria-pressed')==='true','Workflow restart');check(!await p.locator('.ref-journey').evaluate(e=>e.classList.contains('is-playing')),'Reduced motion disables automatic cycling');}
if(width===1440||width===390){await p.evaluate(()=>scrollTo(0,0));await p.screenshot({path:path.join(out,(route==='/'?'home':route.split('/').at(-1))+'-'+theme+'-'+width+'.png'),fullPage:route!=='/'});}
}}
}
await p.emulateMedia({reducedMotion:'no-preference'});await p.goto(base+'/');await p.locator('[data-journey-play]').click();check(await p.locator('.ref-journey').evaluate(e=>e.classList.contains('is-playing')),'Play enables actual path animation');await p.waitForTimeout(3850);check(await p.locator('[data-journey-step="1"]').getAttribute('aria-pressed')==='true','Playback advances the active stage');await p.locator('[data-journey-pause]').click();check(!await p.locator('.ref-journey').evaluate(e=>e.classList.contains('is-playing')),'Pause stops motion');
await c.setExtraHTTPHeaders({'X-Bid-Test-Actor':'admin'});await p.goto(base+'/Workspace');check(await p.locator('link[href*="public-reference.css"]').count()===0,'Internal workspaces do not load this redesign');
check(errors.length===0,'No JavaScript errors: '+errors.join(';'));fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log('PASS: '+checks+' public composition, theme, workflow animation and isolation checks; no SQL writes.');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exit(1)});
