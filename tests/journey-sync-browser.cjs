// Geometry and motion checks on the marked memory-only MVC fixture. No database or form writes.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
    const [base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});
    const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
    const check=(ok,label)=>{if(!ok)throw Error(label);checks++;};
    try{
        for(const width of [1440,768,390,320]){
            const context=await browser.newContext({viewport:{width,height:1000},colorScheme:'dark',reducedMotion:'reduce',extraHTTPHeaders:{'X-Bid-Test-Actor':'anonymous'}});
            const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
            await page.clock.install();
            const response=await page.goto(base+'/');check(response.headers()['x-stms-bid-fixture']==='memory-only'&&response.status()===200,'Marked home '+width);
            await page.waitForFunction(()=>document.querySelector('[data-public-journey]').classList.contains('is-routed'));
            await page.locator('[data-public-journey]').scrollIntoViewIfNeeded();
            await page.waitForTimeout(200); // Let native visibility/layout callbacks settle before freezing time.
            await page.clock.pauseAt(await page.evaluate(()=>Date.now()+500));
            const click=selector=>page.locator(selector).dispatchEvent('click');
            const state=()=>page.evaluate(()=>{
                const journey=document.querySelector('[data-public-journey]'),svg=journey.querySelector('svg.ref-journey-line'),head=svg.querySelector('circle');
                const point=new DOMPoint(head.cx.baseVal.value,head.cy.baseVal.value).matrixTransform(svg.getScreenCTM());
                const dots=[...journey.querySelectorAll('[data-journey-step]>i')].map(dot=>{const r=dot.getBoundingClientRect();return{x:r.left+r.width/2,y:r.top+r.height/2}});
                return{index:[...journey.querySelectorAll('[data-journey-step]')].findIndex(s=>s.getAttribute('aria-pressed')==='true'),point:{x:point.x,y:point.y},dots,offset:svg.querySelector('.ref-journey-trace').style.strokeDashoffset,playing:journey.classList.contains('is-playing'),visited:journey.querySelectorAll('.is-visited').length};
            });
            const errorAt=(s,i)=>Math.hypot(s.point.x-s.dots[i].x,s.point.y-s.dots[i].y);
            // Every manual position must land at the actual DOM dot, including the second mobile row.
            for(let i=0;i<6;i++){
                await click(`[data-journey-step="${i}"]`);const s=await state();
                check(s.index===i&&errorAt(s,i)<1.1,'Head and selected dot coincide '+width+'/'+i);
                check(s.visited===i,'Only reached dots are completed '+width+'/'+i);
            }
            await click('[data-journey-reset]');let s=await state();check(s.index===0&&errorAt(s,0)<1.1&&!s.playing,'Reduced-motion reset is static '+width);
            await page.emulateMedia({reducedMotion:'no-preference'});await page.clock.runFor(48);
            await click('[data-journey-play]');await page.clock.runFor(1800);s=await state();
            check(s.playing&&s.index===0&&errorAt(s,0)>10,'Line travels before the next dot activates '+width);
            await click('[data-journey-pause]');const paused=await state();await page.clock.runFor(5000);s=await state();
            check(!s.playing&&s.offset===paused.offset&&s.point.x===paused.point.x&&s.point.y===paused.point.y,'Pause freezes line and dots together '+width);
            // Start each route from a known point and sample just after each arrival.
            await click('[data-journey-reset]');await click('[data-journey-play]');
            for(let i=1;i<6;i++){
                await page.clock.runFor(i===1?3632:3600);s=await state();
                check(s.index===i&&errorAt(s,i)<12,'Stage activates as the line reaches its dot '+width+'/'+i);
            }
            await page.clock.runFor(1800);s=await state();check(s.index===5&&s.point.x>s.dots[5].x,'Final stage remains active while trace continues toward right edge '+width);
            await page.clock.runFor(1800);s=await state();check(s.index===0&&s.visited===0,'Loop restarts line and markers together '+width);
            await click('[data-journey-pause]');await click('[data-journey-step="4"]');
            await page.setViewportSize({width:width>800?390:1440,height:1000});await page.clock.runFor(64);s=await state();
            check(s.index===4&&errorAt(s,4)<1.1,'Responsive reflow keeps dot alignment '+width);
            await page.emulateMedia({reducedMotion:'reduce'});await page.clock.runFor(64);const reduced=await state();await page.clock.runFor(5000);s=await state();
            check(!s.playing&&s.offset===reduced.offset&&errorAt(s,s.index)<1.1,'Reduced motion stops at a coherent node '+width);
            check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'No overflow from the SVG route '+width);
            if(width===1440||width===390)await page.screenshot({path:path.join(out,`journey-${width}.png`)});
            await context.close();
        }
        check(errors.length===0,'No JavaScript errors');
        fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors},null,2));console.log(`PASS: ${checks} synchronized route, dot, pause, loop, responsive and reduced-motion checks. No SQL writes.`);
    }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exit(1)});
