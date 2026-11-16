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
    let index = 0, timer, playing = !reduced.matches, visible = true;
    function select(next, announce=false) {
        index = (next+steps.length)%steps.length;
        steps.forEach((step,i)=>step.setAttribute('aria-pressed',String(index===i)));
        const text = `${String(index+1).padStart(2,'0')} / ${descriptions[index]}`;
        journey.querySelector('[data-journey-caption]').textContent = text;
        if (announce) journey.querySelector('[data-journey-status]').textContent = text;
    }
    function sync() {
        clearInterval(timer);
        const active = playing && !reduced.matches && visible && !document.hidden;
        journey.classList.toggle('is-playing',active);
        play.setAttribute('aria-pressed',String(active));pause.setAttribute('aria-pressed',String(!active));
        if (active) timer = setInterval(()=>select(index+1),3600);
    }
    steps.forEach((step,i)=>{
        step.addEventListener('click',()=>{playing=false;select(i,true);sync();});
        step.addEventListener('keydown',event=>{
            if (!['ArrowRight','ArrowLeft','Home','End'].includes(event.key)) return;
            event.preventDefault();playing=false;select(event.key==='Home'?0:event.key==='End'?steps.length-1:i+(event.key==='ArrowRight'?1:-1),true);sync();steps[index].focus();
        });
    });
    play.addEventListener('click',()=>{playing=true;sync();});pause.addEventListener('click',()=>{playing=false;sync();});
    journey.querySelector('[data-journey-reset]').addEventListener('click',()=>{select(0,true);sync();});
    // A keyboard user must never have the selected step changed while reading it.
    journey.addEventListener('focusin',()=>{playing=false;sync();});
    if ('IntersectionObserver' in window) { const visibility = new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;sync();});visibility.observe(journey); }
    document.addEventListener('visibilitychange',sync);
    reduced.addEventListener('change',()=>{playing=false;sync();});
    window.addEventListener('pagehide',()=>clearInterval(timer));
    window.addEventListener('pageshow',sync);
    select(0);sync();
})();
