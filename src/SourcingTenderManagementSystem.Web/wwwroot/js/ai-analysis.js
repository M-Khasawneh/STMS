(() => {
 'use strict';
 const form=document.querySelector('[data-ai-run]');if(!form)return;
 const button=form.querySelector('button[type=submit]'),status=form.querySelector('[data-ai-status]');
 const original=button.textContent,description=status.textContent;
 form.addEventListener('submit',event=>{
  if(!form.checkValidity()||event.defaultPrevented)return;
  if(form.dataset.running==='true'){event.preventDefault();return;}
  form.dataset.running='true';button.disabled=true;button.setAttribute('aria-busy','true');button.textContent='Analyzing locally…';status.textContent='Private documents are being read locally and Ollama is generating preliminary advice. This may take up to five minutes. No workflow decisions are being changed.';
 });
 window.addEventListener('pageshow',()=>{delete form.dataset.running;button.disabled=false;button.removeAttribute('aria-busy');button.textContent=original;status.textContent=description;});
})();
