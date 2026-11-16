/* Public composition enhancement. No intercepted navigation, forms or external dependencies. */
(() => {
    'use strict';
    if (!document.body.classList.contains('stms-public-reference')) return;
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const animations = new Set();
    const reveal = element => {
        if (reduced.matches || !element.animate) return;
        const animation = element.animate([{opacity:0,transform:'translateY(14px)'},{opacity:1,transform:'none'}],{duration:550,easing:'cubic-bezier(.22,1,.36,1)'});
        animations.add(animation); animation.finished.catch(()=>{}).finally(()=>animations.delete(animation));
    };
    const elements = document.querySelectorAll('[data-public-reveal]');
    let observer;
    if ('IntersectionObserver' in window) { observer = new IntersectionObserver(entries=>entries.forEach(entry=>{if(entry.isIntersecting){observer.unobserve(entry.target);reveal(entry.target);}}),{threshold:.12});elements.forEach(element=>observer.observe(element)); }
    const cancelReveals = () => { animations.forEach(animation=>animation.cancel()); animations.clear(); };
    reduced.addEventListener('change',cancelReveals);
    window.addEventListener('pagehide',cancelReveals);
    const journey = document.querySelector('[data-public-journey]');
    if (!journey) return;
    const steps = [...journey.querySelectorAll('[data-journey-step]')];
    const play = journey.querySelector('[data-journey-play]'), pause = journey.querySelector('[data-journey-pause]');
    const descriptions = ['Define the need, quantities and specifications.','Review the request and prepare the tender.','Invite eligible suppliers and receive their offers.','Check compliance and evaluate against approved criteria.','Bring the evaluation to the authorized decision-makers.','Record the outcome, notify participants and close the tender.'];
    const svg = journey.querySelector('.ref-journey-line');
    const base = svg.querySelector('path'), trace = svg.querySelector('.ref-journey-trace');
    const head = svg.querySelector('.ref-journey-head');
    const grid = journey.querySelector('.ref-journey-stages');
    const legMs = 3600, cycleMs = steps.length * legMs;
    let index = -1, phase = 0, frame = 0, measureFrame = 0, previous = 0;
    let playing = !reduced.matches, visible = true, onPage = true, distances = [], total = 0;
    function select(next, announce=false) {
        index = (next+steps.length)%steps.length;
        steps.forEach((step,i)=>{
            step.setAttribute('aria-pressed',String(index===i));
            step.classList.toggle('is-visited',i<index);
        });
        const text = `${String(index+1).padStart(2,'0')} / ${descriptions[index]}`;
        journey.querySelector('[data-journey-caption]').textContent = text;
        if (announce) journey.querySelector('[data-journey-status]').textContent = text;
    }
    function render() {
        const arrived = Math.min(steps.length-1,Math.floor(phase/legMs));
        if (arrived!==index) select(arrived);
        if (!total) return;
        const start = distances[arrived], end = distances[Math.min(arrived+1,steps.length-1)];
        const distance = start+(end-start)*((phase%legMs)/legMs);
        trace.style.strokeDashoffset = String(total-distance);
        const point = base.getPointAtLength(distance);
        head.setAttribute('cx',String(point.x)); head.setAttribute('cy',String(point.y));
    }
    function seek(next, announce=false) {
        phase = ((next+steps.length)%steps.length)*legMs;
        select(next,announce); render();
    }
    function tick(now) {
        phase = (phase+Math.max(0,now-previous))%cycleMs;
        previous = now; render();
        frame = requestAnimationFrame(tick);
    }
    function sync() {
        cancelAnimationFrame(frame); frame=0;
        const active = playing && !reduced.matches && visible && !document.hidden && onPage && total>0;
        journey.classList.toggle('is-playing',active);
        play.setAttribute('aria-pressed',String(active));pause.setAttribute('aria-pressed',String(!active));
        if (active) { previous=performance.now(); frame=requestAnimationFrame(tick); }
    }
    function measureRoute() {
        measureFrame=0;
        // Read geometry once on layout changes, never on the animation's per-frame path.
        const origin=journey.getBoundingClientRect();
        const points=steps.map(step=>{
            const dot=step.querySelector('i').getBoundingClientRect(), card=step.getBoundingClientRect();
            return {x:dot.left+dot.width/2-origin.left,y:dot.top+dot.height/2-origin.top,top:card.top-origin.top};
        });
        if (!origin.width || points.some(point=>!Number.isFinite(point.x+point.y))) return;
        const probe=document.createElementNS('http://www.w3.org/2000/svg','path');
        const first=points[0];
        let d=`M0 ${first.y} C${first.x*.3} ${first.y+12} ${first.x*.6} ${first.y} ${first.x} ${first.y}`;
        probe.setAttribute('d',d); distances=[probe.getTotalLength()];
        for(let i=1;i<points.length;i++) {
            const a=points[i-1], b=points[i];
            if (b.x<a.x) {
                // On mobile, route through the row gap and outer gutter instead of crossing a card.
                const gutter=8, right=origin.width-gutter, gap=(a.y+b.top)/2;
                d+=` C${right} ${a.y} ${right} ${gap} ${right} ${gap} L${gutter} ${gap} C${gutter} ${gap} ${gutter} ${b.y} ${gutter} ${b.y} L${b.x} ${b.y}`;
            } else {
                const middle=(a.x+b.x)/2;
                d+=` C${middle} ${a.y} ${middle} ${b.y} ${b.x} ${b.y}`;
            }
            probe.setAttribute('d',d); distances.push(probe.getTotalLength());
        }
        total=distances.at(-1);
        if (!Number.isFinite(total) || total<=0) { total=0; sync(); return; }
        const height=Math.ceil(Math.max(...points.map(point=>point.y))+24);
        svg.setAttribute('viewBox',`0 0 ${origin.width} ${height}`); svg.style.height=`${height}px`;
        base.setAttribute('d',d); trace.setAttribute('d',d);
        trace.style.strokeDasharray=`${total} ${total}`;
        journey.classList.add('is-routed'); render(); sync();
    }
    function queueMeasure() {
        if (onPage && !measureFrame) measureFrame=requestAnimationFrame(measureRoute);
    }
    steps.forEach((step,i)=>{
        step.addEventListener('click',()=>{playing=false;seek(i,true);sync();});
        step.addEventListener('keydown',event=>{
            if (!['ArrowRight','ArrowLeft','Home','End'].includes(event.key)) return;
            event.preventDefault();playing=false;seek(event.key==='Home'?0:event.key==='End'?steps.length-1:i+(event.key==='ArrowRight'?1:-1),true);sync();steps[index].focus();
        });
    });
    play.addEventListener('click',()=>{playing=true;sync();});pause.addEventListener('click',()=>{playing=false;sync();});
    journey.querySelector('[data-journey-reset]').addEventListener('click',()=>{seek(0,true);sync();});
    // A keyboard user must never have the selected step changed while reading it.
    journey.addEventListener('focusin',()=>{playing=false;sync();});
    if ('IntersectionObserver' in window) { const visibility = new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;sync();});visibility.observe(journey); }
    if ('ResizeObserver' in window) new ResizeObserver(queueMeasure).observe(grid);
    else window.addEventListener('resize',queueMeasure);
    document.fonts?.ready.then(queueMeasure);
    document.addEventListener('visibilitychange',sync);
    reduced.addEventListener('change',()=>{playing=false;seek(Math.max(0,index));sync();});
    window.addEventListener('pagehide',()=>{onPage=false;cancelAnimationFrame(measureFrame);measureFrame=0;sync();});
    window.addEventListener('pageshow',()=>{onPage=true;queueMeasure();sync();});
    select(0);measureRoute();
})();
