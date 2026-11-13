/* Vanilla ports of design-references. No JSX/runtime dependencies, routing or form interception.
   ScrollStack uses the reference's window-scroll mode, with smoothScroll/snap disabled:
   native wheel, touch, keyboard and history navigation stay authoritative. */
(() => {
  'use strict';
  const root = document.querySelector('.stms-landing');
  if (!root) return;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const fine = matchMedia('(hover: hover) and (pointer: fine)');
  const clamp = (x, a = 0, b = 1) => Math.max(a, Math.min(b, x));
  const smooth = (a, b, x) => { const t = clamp((x - a) / (b - a || .000001)); return t * t * (3 - 2 * t); };
  const running = new Set();
  const disposers = [];
  const revealed = new WeakSet();
  let heroEntered = false;
  function motion(element, frames, options) {
    if (reduced.matches || !element.animate) return null;
    const animation = element.animate(frames, options);
    running.add(animation);
    animation.finished.catch(() => {}).finally(() => running.delete(animation));
    return animation;
  }
  // Independent Aurora/orb implementation: cinematic type + damped pointer/parallax layers.
  function AuroraHero() {
    const hero = root.querySelector('.stms-hero');
    if (!hero) return () => {};
    const orb = hero.querySelector('.stms-orb-wrap'), aura = hero.querySelector('.stms-aurora');
    const intros = [];
    if (!heroEntered && !reduced.matches) {
      heroEntered = true;
      hero.querySelectorAll('[data-hero-reveal]').forEach((element,index) => {
        const headline = element.closest('h1');
        const animation = motion(element,[{opacity:0,transform:`perspective(900px) translate3d(0,${headline?48:20}px,0) rotateX(${headline?12:0}deg)`,filter:`blur(${headline?9:4}px)`},{opacity:1,transform:'perspective(900px) translate3d(0,0,0) rotateX(0deg)',filter:'blur(0px)'}],{duration:headline?1150:950,delay:120+index*95,easing:'cubic-bezier(.16,1,.3,1)',fill:'both'});
        if(animation){intros.push(animation);animation.finished.then(()=>animation.cancel()).catch(()=>{});}
      });
    }
    let visible = true, raf = 0, last = 0, x = 0, y = 0, tx = 0, ty = 0;
    function frame(now) {
      raf = 0;
      if (!visible || reduced.matches || document.hidden) return;
      const dt = last?Math.min((now-last)/1000,.1):1/60; last=now;
      const k=1-Math.exp(-dt/.12);x+=(tx-x)*k;y+=(ty-y)*k;
      const rect=hero.getBoundingClientRect(), progress=clamp(-rect.top/rect.height);
      orb.style.setProperty('--orb-x',`${x.toFixed(2)}px`);orb.style.setProperty('--orb-y',`${y.toFixed(2)}px`);
      aura.style.transform=`translate3d(${(x*.45).toFixed(2)}px,${(progress*55+y*.6).toFixed(2)}px,0)`;
      if(Math.abs(tx-x)+Math.abs(ty-y)>.03)raf=requestAnimationFrame(frame);else last=0;
    }
    function schedule(){if(!raf&&!reduced.matches)raf=requestAnimationFrame(frame);}
    const move=event=>{if(event.pointerType==='touch'||!fine.matches||reduced.matches)return;const r=hero.getBoundingClientRect();tx=clamp((event.clientX-r.left)/r.width-.5,-.5,.5)*20;ty=clamp((event.clientY-r.top)/r.height-.5,-.5,.5)*16;schedule();};
    const leave=()=>{tx=ty=0;schedule();};
    const focus=()=>{intros.forEach(a=>a.cancel());};
    const observer=new IntersectionObserver(entries=>{visible=entries[0].isIntersecting;hero.toggleAttribute('data-motion-paused',!visible);if(visible)schedule();else{cancelAnimationFrame(raf);raf=0;last=0;}},{threshold:0});
    observer.observe(hero);
    hero.addEventListener('pointermove',move,{passive:true});hero.addEventListener('pointerleave',leave);hero.addEventListener('focusin',focus);window.addEventListener('scroll',schedule,{passive:true});
    return()=>{cancelAnimationFrame(raf);observer.disconnect();intros.forEach(a=>a.cancel());hero.removeEventListener('pointermove',move);hero.removeEventListener('pointerleave',leave);hero.removeEventListener('focusin',focus);window.removeEventListener('scroll',schedule);orb.style.removeProperty('--orb-x');orb.style.removeProperty('--orb-y');aura.style.transform='';hero.removeAttribute('data-motion-paused');};
  }
  // AnimatedContent: axis, distance, blur, perspective tilt, wipe and child staggering.
  function AnimatedContent() {
    if (!('IntersectionObserver' in window) || reduced.matches || !Element.prototype.animate) return () => {};
    let wipes = false;
    try { CSS.registerProperty({ name: '--ac-wipe', syntax: '<number>', initialValue: '0', inherits: false }); wipes = true; }
    catch { wipes = !!window.CSS?.registerProperty; }
    const pending = new Map();
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue;
        const group = pending.get(entry.target);
        if (!group) continue;
        observer.unobserve(entry.target);
        revealed.add(entry.target);
        entry.target.dispatchEvent(new CustomEvent('animatedcontent:start'));
        group.forEach(animation => animation.play());
        Promise.all(group.map(a => a.finished.catch(() => {}))).then(() => {
          group.forEach(a => a.cancel()); pending.delete(entry.target);
          entry.target.dispatchEvent(new CustomEvent('animatedcontent:complete'));
        });
      }
    }, { threshold: .12 });
    root.querySelectorAll('[data-animated-content]').forEach(element => {
      if (revealed.has(element)) return;
      const stagger = Number(element.dataset.acStagger || 0);
      const targets = stagger ? [...element.children] : [element];
      const horizontal = element.dataset.acDirection === 'horizontal';
      const distance = Number(element.dataset.acDistance || 45);
      const tilt = Number(element.dataset.acTilt || 0);
      const wipe = element.dataset.acWipe;
      const animations = targets.map((target, index) => {
        const from = { opacity: 0, transform: `perspective(900px) translate3d(${horizontal ? distance : 0}px,${horizontal ? 0 : distance}px,0) rotateX(${tilt}deg) scale(.97)`, filter: 'blur(6px)' };
        const to = { opacity: 1, transform: 'perspective(900px) translate3d(0,0,0) rotateX(0deg) scale(1)', filter: 'blur(0px)' };
        if (wipe && wipes) {
          const gradient = 'linear-gradient(to top, #000 calc(var(--ac-wipe) * 140% - 40%), transparent calc(var(--ac-wipe) * 140%))';
          from.maskImage = to.maskImage = gradient; from['--ac-wipe'] = 0; to['--ac-wipe'] = 1;
        }
        const a = motion(target, [from, to], { duration: 850, delay: index * stagger, easing: 'cubic-bezier(.22,1,.36,1)', fill: 'both' });
        a.pause(); return a;
      });
      pending.set(element, animations); observer.observe(element);
    });
    const focus = event => { for (const [node, animations] of pending) if (node.contains(event.target)) { revealed.add(node); animations.forEach(a => a.cancel()); observer.unobserve(node); pending.delete(node); } };
    root.addEventListener('focusin', focus);
    return () => { observer.disconnect(); pending.forEach(group => group.forEach(a => a.cancel())); pending.clear(); root.removeEventListener('focusin', focus); };
  }
  // Magnet: the reference's exclusive proximity field + 170/16/1 damped spring.
  function Magnet() {
    const entries = [...root.querySelectorAll('[data-magnet]')].map(wrapper => ({ wrapper, inner: wrapper.querySelector('.stms-magnet-inner'), x: 0, y: 0, vx: 0, vy: 0, rx: 0, ry: 0, scale: 1, tx: 0, ty: 0, trx: 0, try: 0, ts: 1 }));
    let pointer = null, raf = 0, last = 0;
    function measure() {
      let winner = null, bestOutside = Infinity, bestCenter = Infinity;
      for (const entry of entries) {
        entry.tx = entry.ty = entry.trx = entry.try = 0; entry.ts = 1;
        entry.wrapper.removeAttribute('data-magnet-active');
        if (!pointer || reduced.matches || !fine.matches || entry.wrapper.contains(document.activeElement)) continue;
        const rect = entry.wrapper.getBoundingClientRect();
        const dx = pointer.x - rect.left - rect.width / 2, dy = pointer.y - rect.top - rect.height / 2;
        const outside = Math.hypot(Math.max(0, Math.abs(dx) - rect.width / 2), Math.max(0, Math.abs(dy) - rect.height / 2));
        const center = Math.hypot(dx, dy);
        if (outside > 90 || outside > bestOutside || outside === bestOutside && center >= bestCenter) continue;
        winner = { entry, dx, dy, outside, rect }; bestOutside = outside; bestCenter = center;
      }
      if (winner) {
        const { entry, dx, dy, outside, rect } = winner;
        const pull = 1 - smooth(45, 90, outside);
        let x = dx / 2 * pull, y = dy / 2 * pull;
        const length = Math.hypot(x, y); if (length > 22) { x *= 22 / length; y *= 22 / length; }
        Object.assign(entry, { tx: x, ty: y, trx: -clamp(dy / Math.max(rect.height / 2, 1), -1, 1) * 4 * pull, try: clamp(dx / Math.max(rect.width / 2, 1), -1, 1) * 4 * pull, ts: 1 + .025 * pull });
        entry.wrapper.setAttribute('data-magnet-active', '');
      }
    }
    function frame(now) {
      raf = 0; measure();
      const dt = last ? Math.min((now - last) / 1000, 1 / 30) : 1 / 60; last = now;
      let unsettled = false;
      for (const e of entries) {
        const steps = Math.ceil(dt * 120), step = dt / steps;
        for (let i = 0; i < steps; i++) { e.vx += (170 * (e.tx - e.x) - 16 * e.vx) * step; e.vy += (170 * (e.ty - e.y) - 16 * e.vy) * step; e.x += e.vx * step; e.y += e.vy * step; }
        const k = 1 - Math.exp(-dt * 15); e.rx += (e.trx - e.rx) * k; e.ry += (e.try - e.ry) * k; e.scale += (e.ts - e.scale) * k;
        const rest = Math.abs(e.tx - e.x) < .025 && Math.abs(e.ty - e.y) < .025 && Math.hypot(e.vx, e.vy) < .05 && Math.abs(e.ts - e.scale) < .0001 && Math.abs(e.trx - e.rx) + Math.abs(e.try - e.ry) < .01;
        if (rest) { e.x = e.tx; e.y = e.ty; e.rx = e.trx; e.ry = e.try; e.scale = e.ts; e.vx = e.vy = 0; } else unsettled = true;
      }
      for (const e of entries) e.inner.style.transform = `translate3d(${e.x.toFixed(2)}px,${e.y.toFixed(2)}px,0) perspective(800px) rotateX(${e.rx.toFixed(2)}deg) rotateY(${e.ry.toFixed(2)}deg) scale(${e.scale.toFixed(4)})`;
      if (unsettled) raf = requestAnimationFrame(frame); else last = 0;
    }
    const wake = () => { if (!raf) raf = requestAnimationFrame(frame); };
    const move = event => { if (event.pointerType === 'touch' || !fine.matches) return; pointer = { x: event.clientX, y: event.clientY }; wake(); };
    const leave = () => { pointer = null; wake(); };
    window.addEventListener('pointermove', move, { passive: true }); window.addEventListener('scroll', wake, { passive: true }); window.addEventListener('blur', leave); document.documentElement.addEventListener('pointerleave', leave); root.addEventListener('focusin', wake); root.addEventListener('focusout', wake);
    return () => { cancelAnimationFrame(raf); window.removeEventListener('pointermove', move); window.removeEventListener('scroll', wake); window.removeEventListener('blur', leave); document.documentElement.removeEventListener('pointerleave', leave); root.removeEventListener('focusin', wake); root.removeEventListener('focusout', wake); entries.forEach(e => { e.inner.style.transform = ''; e.wrapper.removeAttribute('data-magnet-active'); }); };
  }
  // Shared passive scheduler: measure all scroll geometry, then write. No wheel/touch interception.
  function ScrollScenes() {
    const expand = root.querySelector('[data-scroll-expand]');
    if (!expand || !root.querySelector('[data-scroll-stack]')) return () => {};
    const track = expand.querySelector('.stms-expand-track'), stage = expand.querySelector('.stms-expand-stage');
    const frame = expand.querySelector('.stms-expand-frame'), media = expand.querySelector('.stms-expand-media');
    const title = expand.querySelector('.stms-expand-title'), hint = expand.querySelector('.stms-expand-hint'), overlay = expand.querySelector('.stms-expand-overlay'), scrim = expand.querySelector('.stms-expand-scrim');
    const stack = root.querySelector('[data-scroll-stack]'), slots = [...stack.querySelectorAll('.stms-stack-slot')];
    let raf = 0, current = 0, target = 0, stageHeight = 0, span = 0, gap = 24, pin = 0, header = 0, fits = false, lastTick = 0;
    function resize() {
      const topbar = document.querySelector('.topbar');
      header = topbar ? topbar.offsetHeight + (parseFloat(getComputedStyle(topbar).top) || 0) + 12 : 0;
      // Never trap a stage taller than the available viewport, including landscape/zoom.
      stageHeight = Math.max(220, Math.min(innerHeight - header - 12, 760));
      span = stageHeight * (innerWidth <= 580 ? .85 : 1.2);
      expand.style.setProperty('--expand-height', `${stageHeight}px`); expand.style.setProperty('--expand-top', `${header}px`);
      track.style.height = `${stageHeight + span + stageHeight * .35}px`; expand.classList.add('is-motion');
      gap = innerWidth <= 580 ? 12 : 20; pin = Math.max(header + 14, innerHeight * .15);
      fits = slots.every(s => s.firstElementChild.offsetHeight + pin + (slots.length - 1) * gap < innerHeight - 12);
      stack.classList.toggle('is-stack', fits); stack.style.setProperty('--stack-top', `${pin}px`); stack.style.setProperty('--stack-gap', `${gap}px`);
      slots.forEach((slot, i) => { slot.style.setProperty('--stack-index', i); slot.style.setProperty('--stack-layer', i + 1); });
      target = clamp((header - track.getBoundingClientRect().top) / span); current = target; schedule();
    }
    // ScrollExpand equations follow the supplied smoothstep aperture and overlay choreography.
    function ScrollExpand(progress) {
      const e = smooth(0, 1, progress), width = 42 + 58 * e, height = 58 + 42 * e;
      frame.style.clipPath = `inset(${(100-height)/2}% ${(100-width)/2}% ${(100-height)/2}% ${(100-width)/2}% round ${24*(1-e)}px)`;
      media.style.transform = `scale(${1.35-.35*e})`;
      scrim.style.opacity = .45 * e;
      const out = smooth(.4, .88, progress), inn = smooth(.68, 1, progress), gone = smooth(0, .12, progress);
      title.style.opacity = 1 - out; title.style.transform = `translate3d(0,${-28*out}px,0) scale(${1+.06*out})`;
      hint.style.opacity = 1 - gone; hint.style.transform = `translate3d(0,${8*gone}px,0)`;
      overlay.style.opacity = inn; overlay.style.transform = `translate3d(0,${18*(1-inn)}px,0)`;
      expand.dataset.progress = progress.toFixed(3);
    }
    // ScrollStack: accumulate overlap depth, then scale/dim/tilt the covered cards.
    function ScrollStack(tops, heights) {
      const depths = slots.map(() => 0);
      if (fits) for (let j = 1; j < slots.length; j++) {
        const pinBefore = pin + (j - 1) * gap, start = pinBefore + heights[j - 1];
        const cover = clamp((start - tops[j]) / Math.max(1, start - pinBefore - gap));
        for (let i = 0; i < j; i++) depths[i] += cover;
      }
      const activeIndex = tops.reduce((active, top, i) => top <= pin + i * gap + 35 ? i : active, 0);
      const counter = root.querySelector('[data-workflow-current]'), progress = root.querySelector('[data-workflow-progress]');
      if(counter)counter.textContent=String(activeIndex+1).padStart(2,'0');
      if(progress)progress.style.transform=`scaleX(${(activeIndex+1)/slots.length})`;
      slots.forEach((slot, i) => {
        const card = slot.firstElementChild, depth = depths[i], lean = Math.min(depth, 1);
        card.style.transform = depth < .0005 ? '' : `perspective(1200px) rotateX(${-lean*2}deg) rotate(${lean*1.1*(i%2?-1:1)}deg) scale(${Math.max(.8,1-depth*.05)})`;
        card.style.setProperty('--stack-dim', (1-Math.pow(.8,depth)).toFixed(3));
        slot.dataset.depth = depth.toFixed(3);
      });
    }
    function tick(now) {
      raf = 0;
      const dt = lastTick ? Math.min((now-lastTick)/1000, .1) : 1/60; lastTick = now;
      target = clamp((header - track.getBoundingClientRect().top) / span);
      const tops = slots.map(s => s.getBoundingClientRect().top), heights = slots.map(s => s.offsetHeight);
      current += (target - current) * (1-Math.exp(-dt/.1));
      if (Math.abs(target-current) < .0004) current = target;
      ScrollExpand(current); ScrollStack(tops, heights);
      if (current !== target) raf = requestAnimationFrame(tick); else lastTick = 0;
    }
    function schedule() { if (!raf) raf = requestAnimationFrame(tick); }
    const observer = new ResizeObserver(resize);
    slots.forEach(s => observer.observe(s.firstElementChild));
    window.addEventListener('scroll', schedule, { passive: true }); window.addEventListener('resize', resize);
    resize();
    return () => { cancelAnimationFrame(raf); observer.disconnect(); window.removeEventListener('scroll', schedule); window.removeEventListener('resize', resize); track.style.height = ''; expand.classList.remove('is-motion'); stack.classList.remove('is-stack'); [frame,media,title,hint,overlay,scrim].forEach(el => el.removeAttribute('style')); slots.forEach(s => { s.firstElementChild.style.transform = ''; s.firstElementChild.style.removeProperty('--stack-dim'); delete s.dataset.depth; }); delete expand.dataset.progress; };
  }
  // SlingButton: rubber-band pull, tangent bands, armed threshold, launch/recoil and particles.
  function SlingButton() {
    const shell = root.querySelector('[data-sling]');
    if (!shell) return () => {};
    const stage = shell.querySelector('.stms-sling-stage'), pad = shell.querySelector('button');
    const moving = shell.querySelector('.stms-sling-move'), icon = shell.querySelector('.stms-sling-icon');
    const band = shell.querySelector('.stms-sling-band'), hot = shell.querySelector('.stms-sling-hot'), arc = shell.querySelector('.stms-sling-arc');
    const fallback = shell.querySelector('[data-sling-fallback]'), help = shell.querySelector('#sling-help');
    stage.hidden = false; fallback.hidden = true; help.hidden = false;
    const particles = Array.from({ length: 14 }, () => { const dot = document.createElement('span'); dot.className = 'stms-sling-dot'; dot.setAttribute('aria-hidden','true'); stage.append(dot); return dot; });
    let grip = null, x = 0, y = 0, vx = 0, vy = 0, raf = 0, last = 0, skip = false;
    const R = 160, ARM = 48;
    function paint() {
      const distance = Math.hypot(x,y), p = clamp(distance/ARM);
      moving.style.transform = `translate3d(${x}px,${y}px,0)`;
      let d = '';
      if (distance > .5) { const angle = Math.atan2(y,x), b = Math.acos(clamp((36-26)/distance,-1,1)); d = [angle+b,angle-b].map(t => `M${36*Math.cos(t)},${36*Math.sin(t)}L${x+26*Math.cos(t)},${y+26*Math.sin(t)}`).join(''); }
      band.setAttribute('d',d); hot.setAttribute('d',d); hot.style.opacity=p;
      arc.setAttribute('stroke-dasharray',`${p} ${1-p}`); arc.setAttribute('stroke-dashoffset',p/2); arc.style.opacity=p>.01?'1':'0';
      arc.setAttribute('transform',`rotate(${Math.atan2(-y,-x)*180/Math.PI})`);
      icon.style.transform = distance > .1 ? `rotate(${(Math.atan2(-y,-x)*180/Math.PI+90)*clamp(distance/12)}deg)` : '';
      shell.toggleAttribute('data-armed', !!grip && distance >= ARM);
    }
    function settle(now) {
      raf = 0; const dt = last ? Math.min((now-last)/1000,1/30) : 1/60; last = now;
      const steps = Math.ceil(dt*120), step = dt/steps;
      for(let i=0;i<steps;i++){vx+=(240*-x-20*vx)*step;vy+=(240*-y-20*vy)*step;x+=vx*step;y+=vy*step;}
      const rest = Math.hypot(x,y)<.04 && Math.hypot(vx,vy)<.1;
      if(rest){x=y=vx=vy=0;last=0;} paint(); if(!rest)raf=requestAnimationFrame(settle);
    }
    function send(ux=0,uy=1,power=1) {
      shell.dispatchEvent(new CustomEvent('sling:send'));
      if(!reduced.matches) particles.forEach((dot,i) => {
        const angle = Math.atan2(-uy,-ux)+(i ? (Math.random()+Math.random()-1)*Math.PI/6 : 0);
        const reach = (i ? 35+Math.random()*85 : 120)*(1+.25*power), cx=Math.cos(angle), cy=Math.sin(angle), scale=i?.3+Math.random()*.6:1;
        motion(dot,[{opacity:1,transform:`translate(${cx*36}px,${cy*36}px) scale(${scale})`},{opacity:1,offset:.5},{opacity:0,transform:`translate(${cx*(36+reach)}px,${cy*(36+reach)}px) scale(.2)`}],{duration:300+Math.random()*90,easing:'cubic-bezier(.23,1,.32,1)'});
      });
      window.scrollTo({top:0,behavior:reduced.matches?'auto':'smooth'});
    }
    function down(e) {
      if(e.button!==0||grip||reduced.matches)return;
      cancelAnimationFrame(raf); raf=0; last=0;
      const distance=Math.hypot(x,y), raw=distance>.5?R*Math.min(distance,.95*R)/(R-Math.min(distance,.95*R)):0;
      grip={id:e.pointerId,startX:e.clientX,startY:e.clientY,ox:distance>.5?raw*x/distance:0,oy:distance>.5?raw*y/distance:0,moved:false,slop:e.pointerType==='touch'?8:4,history:[]};
      pad.setPointerCapture(e.pointerId); pad.setAttribute('data-held','');
    }
    function move(e) {
      if(!grip||grip.id!==e.pointerId)return;
      const dx=e.clientX-grip.startX,dy=e.clientY-grip.startY,rx=grip.ox+dx,ry=grip.oy+dy,raw=Math.hypot(rx,ry);
      if(Math.hypot(dx,dy)>grip.slop)grip.moved=true;
      if(raw<.01){x=y=0;paint();return;}
      const distance=R*raw/(R+raw); x=distance*rx/raw;y=distance*ry/raw;
      const now=performance.now();grip.history.push({x,y,t:now});while(grip.history.length>4||now-grip.history[0].t>80)grip.history.shift();paint();
    }
    function release(e,cancelled=false) {
      if(!grip||grip.id!==e.pointerId)return;
      const g=grip;grip=null;skip=true;pad.removeAttribute('data-held');shell.removeAttribute('data-armed');
      if(pad.hasPointerCapture(e.pointerId))pad.releasePointerCapture(e.pointerId);
      const d=Math.hypot(x,y),ux=d?x/d:0,uy=d?y/d:1,p=d/ARM,fire=g.moved&&d>=ARM&&!cancelled;
      let fx=0,fy=0;
      if(g.history.length>1&&!cancelled){const a=g.history[0],b=g.history.at(-1),dt=b.t-a.t;if(dt>0&&performance.now()-b.t<50){fx=(b.x-a.x)/dt*1000;fy=(b.y-a.y)/dt*1000;}}
      const speed=Math.hypot(fx,fy);if(speed>3000){fx*=3000/speed;fy*=3000/speed;}
      if(!g.moved&&!cancelled)send();else if(fire)send(ux,uy,Math.min(p,1.5));
      const launch=2600*(fire?1:.5)*Math.min(p,fire?1.5:1);vx=fx-ux*launch;vy=fy-uy*launch;
      const velocity=Math.hypot(vx,vy);if(velocity>6000){vx*=6000/velocity;vy*=6000/velocity;}
      if(!raf)raf=requestAnimationFrame(settle);
    }
    const up=e=>release(e), cancel=e=>release(e,true), key=e=>{if(e.key==='Escape'&&grip)release({pointerId:grip.id},true);};
    const click=e=>{if(skip&&e.detail!==0){skip=false;return;}skip=false;send();};
    pad.addEventListener('pointerdown',down);pad.addEventListener('pointermove',move);pad.addEventListener('pointerup',up);pad.addEventListener('pointercancel',cancel);pad.addEventListener('lostpointercapture',cancel);pad.addEventListener('keydown',key);pad.addEventListener('click',click);
    return ()=>{const held=grip;grip=null;if(held&&pad.hasPointerCapture(held.id))pad.releasePointerCapture(held.id);cancelAnimationFrame(raf);[...particles].forEach(dot=>dot.remove());moving.style.transform='';icon.style.transform='';band.setAttribute('d','');hot.setAttribute('d','');arc.style.opacity='0';shell.removeAttribute('data-armed');pad.removeAttribute('data-held');pad.removeEventListener('pointerdown',down);pad.removeEventListener('pointermove',move);pad.removeEventListener('pointerup',up);pad.removeEventListener('pointercancel',cancel);pad.removeEventListener('lostpointercapture',cancel);pad.removeEventListener('keydown',key);pad.removeEventListener('click',click);stage.hidden=true;fallback.hidden=false;help.hidden=true;};
  }
  function reset() {
    while(disposers.length)disposers.pop()();
    running.forEach(animation=>animation.cancel());running.clear();
  }
  function start() {
    reset();
    try {
      if(!reduced.matches){disposers.push(AuroraHero());disposers.push(AnimatedContent());disposers.push(Magnet());disposers.push(ScrollScenes());}
      disposers.push(SlingButton());
    } catch(error) { reset(); console.warn('Homepage motion enhancement unavailable; native content remains accessible.',error); }
  }
  reduced.addEventListener('change',start);fine.addEventListener('change',start);
  window.addEventListener('pagehide',reset);
  window.addEventListener('pageshow',event=>{if(event.persisted)start();});
  document.addEventListener('visibilitychange',()=>{document.body.classList.toggle('home-motion-paused',document.hidden);if(document.hidden)reset();else start();});
  start();
})();
