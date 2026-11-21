(() => {
 'use strict';
 const decode=value=>Uint8Array.from(atob(value.replace(/-/g,'+').replace(/_/g,'/')),c=>c.charCodeAt(0));
 const encode=value=>btoa(String.fromCharCode(...new Uint8Array(value))).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
 const serialize=credential=>{
  if(typeof credential.toJSON==='function')return credential.toJSON();
  const r=credential.response;const response={clientDataJSON:encode(r.clientDataJSON)};
  if(r.attestationObject){response.attestationObject=encode(r.attestationObject);response.transports=r.getTransports?.()||[];}
  else{response.authenticatorData=encode(r.authenticatorData);response.signature=encode(r.signature);response.userHandle=r.userHandle?encode(r.userHandle):null;}
  return {id:credential.id,rawId:encode(credential.rawId),type:credential.type,authenticatorAttachment:credential.authenticatorAttachment,response,clientExtensionResults:credential.getClientExtensionResults()};
 };
 for(const form of document.querySelectorAll('[data-passkey-login],[data-passkey-enroll]')){
  const button=form.querySelector('[data-passkey-button]'),cancel=form.querySelector('[data-passkey-cancel]'),status=form.querySelector('[data-passkey-status]');
  const creation=form.hasAttribute('data-passkey-enroll');let abort=null,busy=false;
  const supported=window.isSecureContext&&typeof window.PublicKeyCredential==='function'&&navigator.credentials?.create&&navigator.credentials?.get;
  if(!supported){status.textContent='Passkeys are unavailable in this browser or connection. Use your password; a compatible HTTPS browser is required.';continue;}
  button.disabled=false;
  status.textContent=creation?'Ready to register. Complete your device prompt, then check that the passkey appears in the registered list.':'Use a passkey already registered for this STMS address. First time here? Sign in with your password and register one in Profile settings → Manage device passkeys.';
  const stop=()=>{abort?.abort();};cancel.addEventListener('click',stop);window.addEventListener('pagehide',stop);
  form.addEventListener('keydown',e=>{if(e.key==='Escape'&&busy){e.preventDefault();stop();}});
  form.addEventListener('submit',async event=>{
   event.preventDefault();if(busy||!form.reportValidity())return;busy=true;button.disabled=true;button.setAttribute('aria-busy','true');cancel.hidden=false;abort=new AbortController();
   const token=form.querySelector('[name=__RequestVerificationToken]').value;
   const post=async(url,body,signal)=>fetch(url,{method:'POST',body,credentials:'same-origin',redirect:'follow',signal,headers:{'Accept':'application/json'}});
   try{
    status.textContent=creation?'Verifying your account and preparing the device prompt…':'Preparing your device sign-in prompt…';
    const body=new FormData(form);const start=await post(form.action,body,abort.signal);form.querySelector('[name=CurrentPassword]')?.setAttribute('value','');const password=form.querySelector('[name=CurrentPassword]');if(password)password.value='';
    if(!start.ok||!start.headers.get('content-type')?.includes('application/json'))throw new Error(start.status===429?'Please wait before starting another passkey request.':'Unable to start. Check your current password, account eligibility and configured HTTPS address.');
    const options=await start.json();options.challenge=decode(options.challenge);
    if(creation){options.user.id=decode(options.user.id);for(const key of options.excludeCredentials||[])key.id=decode(key.id);}
    else for(const key of options.allowCredentials||[])key.id=decode(key.id);
    status.textContent='Follow your device prompt. Choose face, fingerprint or PIN if available. STMS never receives your biometric data.';
    const credential=creation?await navigator.credentials.create({publicKey:options,signal:abort.signal}):await navigator.credentials.get({publicKey:options,signal:abort.signal});
    if(!credential)throw new Error('No credential was returned. Use your password or try again.');
    const finish=new FormData();finish.set('__RequestVerificationToken',token);finish.set('CredentialJson',JSON.stringify(serialize(credential)));
    status.textContent='Verifying your passkey…';const response=await post(form.dataset.finish,finish,abort.signal);
    if(!response.ok||!response.headers.get('content-type')?.includes('application/json'))throw new Error('The passkey could not be verified or the request expired. Start again or use your password.');
    const result=await response.json();if(typeof result.redirectUrl!=='string')throw new Error('Invalid navigation destination.');const destination=new URL(result.redirectUrl,location.href);if(destination.origin!==location.origin)throw new Error('Invalid navigation destination.');location.assign(destination.href);
   }catch(error){
    const messages={NotAllowedError:'The device prompt was cancelled, timed out, or no matching passkey was found. Try again or use your password.',InvalidStateError:'This passkey may already be registered. Check your registered passkeys.',SecurityError:'This device cannot use a passkey at this site address. Check HTTPS and the configured origin.',NotSupportedError:'This authenticator is unsupported. Use another compatible device or your password.',AbortError:'Passkey request cancelled. Your password sign-in remains available.'};
    status.textContent=messages[error.name]||(error instanceof TypeError?'Unable to connect. Try again or use your password.':error.message||'Passkey request failed.');
    status.scrollIntoView({block:'nearest'});
    const data=new FormData();data.set('__RequestVerificationToken',token);try{await post(form.dataset.cancel,data);}catch{}
   }finally{const password=form.querySelector('[name=CurrentPassword]');if(password)password.value='';busy=false;abort=null;button.disabled=false;button.removeAttribute('aria-busy');cancel.hidden=true;}
  });
 }
})();
