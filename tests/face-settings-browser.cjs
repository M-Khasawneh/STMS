// HTTPS memory-only fixture. Camera lifecycle is simulated; no physical camera, biometric data or SQL writes.
const {chromium}=require(process.env.STMS_PLAYWRIGHT||'playwright');
const fs=require('fs'),path=require('path');
(async()=>{
 const [base,out]=process.argv.slice(2);fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({channel:'msedge',headless:true});let checks=0;const errors=[];
 const check=(ok,label)=>{if(!ok)throw Error(label);checks++;console.log('PASS: '+label)};
 try{
  const context=await browser.newContext({ignoreHTTPSErrors:true,colorScheme:'dark',reducedMotion:'reduce'});
  await context.addInitScript(()=>{
   window.faceFixture={denied:false,delay:false,tracks:[],frames:[]};
   const loaded=()=>Promise.resolve();
   window.faceapi={nets:{tinyFaceDetector:{loadFromUri:loaded},faceLandmark68Net:{loadFromUri:loaded},faceRecognitionNet:{loadFromUri:loaded}},TinyFaceDetectorOptions:class{},detectAllFaces(canvas){faceFixture.frames.push({width:canvas.width,height:canvas.height});return{withFaceLandmarks(){return{withFaceDescriptors:async()=>[{descriptor:Float32Array.from({length:128},(_,i)=>i===0?1:0),detection:{box:{width:160}}}]}}}}};
   Object.defineProperty(navigator.mediaDevices,'getUserMedia',{configurable:true,value:async options=>{
    if(options.audio!==false)throw Error('Audio requested');
    if(faceFixture.denied)throw new DOMException('Denied','NotAllowedError');
    const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;
    const stream=canvas.captureStream(5);faceFixture.tracks.push(...stream.getTracks());
    if(faceFixture.delay)await new Promise(resolve=>faceFixture.resume=resolve);
    return stream;
   }});
   HTMLMediaElement.prototype.play=async()=>{};
   Object.defineProperty(HTMLVideoElement.prototype,'videoWidth',{get(){return 1280}});
   Object.defineProperty(HTMLVideoElement.prototype,'videoHeight',{get(){return 720}});
   Object.defineProperty(HTMLMediaElement.prototype,'readyState',{get(){return 4}});
   Object.defineProperty(HTMLMediaElement.prototype,'currentTime',{get(){return performance.now()/1000}});
  });
  const page=await context.newPage();page.on('pageerror',error=>errors.push(error.message));
  const response=await page.goto(base+'/Account/Login');check(response.headers()['x-stms-passkey-fixture']==='memory-only','Refusing non-fixture host');
  await page.locator('#UserName').fill('passkey-user');await page.locator('#Password').fill('Fixture-Only!9427');
  await Promise.all([page.waitForURL('**/Workspace*'),page.locator('form[action="/Account/Login"] button[type=submit]').click()]);
  await page.goto(base+'/FaceSettings');
  check(await page.locator('[data-face-settings]').isVisible(),'Camera settings Razor page');
  check(!await page.locator('script[src*="face-api"]').count(),'Models are not loaded on page entrance');
  const enroll=()=>page.locator('[data-face-action=enroll]'),password=()=>page.locator('[data-face-password]'),status=()=>page.locator('[data-face-status]');
  await enroll().click();check((await status().textContent()).includes('current password'),'Enrollment requires password');
  await password().fill('Fixture-Only!9427');await page.evaluate(()=>faceFixture.denied=true);await enroll().click();
  await page.waitForFunction(()=>document.querySelector('[data-face-status]').textContent.includes('permission was denied'));
  check(await enroll().isEnabled(),'Permission denial recovers');
  await page.evaluate(()=>{faceFixture.denied=false;faceFixture.delay=true});await password().fill('Fixture-Only!9427');await enroll().click();
  await page.waitForFunction(()=>typeof faceFixture.resume==='function');await page.locator('[data-face-cancel]').click();await page.evaluate(()=>faceFixture.resume());
  await page.waitForFunction(()=>faceFixture.tracks.every(track=>track.readyState==='ended'));
  check(await page.locator('[data-face-preview]').isHidden(),'Late camera permission grant is stopped after cancellation');
  await page.evaluate(()=>faceFixture.delay=false);await password().fill('Fixture-Only!9427');
  const completed=page.waitForRequest(request=>request.url().endsWith('/FaceSettings/Complete'));
  await enroll().click();const payload=JSON.parse((await completed).postData());
  check(await page.evaluate(()=>faceFixture.frames.length>0&&faceFixture.frames.every(f=>f.width===640&&f.height===360)),'Widescreen laptop frames retain their aspect ratio');
  check(payload.samples.length===5&&payload.samples.every(s=>s.length===128)&&!('image' in payload)&&!('password' in payload),'Only numeric descriptors and one-use token submitted');
  await page.waitForFunction(()=>document.querySelector('[data-face-settings]')?.dataset.registered==='true');
  check(await page.locator('[data-face-action=verify]').isVisible(),'Encrypted enrollment persisted in memory fixture');
  await page.locator('[data-face-action=verify]').click();await page.waitForFunction(()=>document.querySelector('[data-face-status]').textContent.includes('matches the registered template'));
  check(await page.evaluate(()=>faceFixture.tracks.every(track=>track.readyState==='ended')),'Camera tracks stop after verification');
  for(const width of [1440,768,390,320]){await page.setViewportSize({width,height:1000});check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Camera settings responsive '+width)}
  await page.screenshot({path:path.join(out,'face-settings-mobile.png'),fullPage:true});
  check(await page.locator('[data-face-action=enable-login]').isVisible(),'Registering a face leaves camera login disabled');
  await password().fill('Fixture-Only!9427');await page.locator('[data-face-action=enable-login]').click();
  await page.locator('[data-face-action=disable-login]').waitFor({state:'visible'});
  const logout=async()=>{const token=await page.locator('[name=__RequestVerificationToken]').first().inputValue();await page.request.post(base+'/Account/Logout',{form:{__RequestVerificationToken:token}});await page.goto(base+'/Account/Login');};
  await logout();await page.locator('#UserName').fill('passkey-user');
  await page.locator('[data-face-action=login]').click();await page.waitForURL('**/Workspace*');
  check(page.url().includes('/Workspace'),'Opted-in camera grants a real Identity session without a password');
  await page.goto(base+'/FaceSettings');await password().fill('Fixture-Only!9427');await page.locator('[data-face-action=disable-login]').click();
  await page.locator('[data-face-action=enable-login]').waitFor({state:'visible'});
  await logout();await page.locator('#UserName').fill('passkey-user');await page.locator('[data-face-action=login]').click();
  await page.waitForFunction(()=>document.querySelector('[data-face-status]').textContent.includes('Unable to sign in'));
  check(page.url().includes('/Account/Login'),'Matching face cannot log in after opt-out');
  check(await page.evaluate(()=>faceFixture.tracks.every(track=>track.readyState==='ended')),'Camera sign-in releases media tracks after refusal');
  // Verify the real vendored models load independently, without using a physical camera.
  await page.addScriptTag({url:base+'/lib/face-api/face-api.js'});
  const models=await page.evaluate(async()=>{await faceapi.tf.setBackend('cpu');await Promise.all([faceapi.nets.tinyFaceDetector.loadFromUri('/lib/face-api/model'),faceapi.nets.faceLandmark68Net.loadFromUri('/lib/face-api/model'),faceapi.nets.faceRecognitionNet.loadFromUri('/lib/face-api/model')]);const canvas=document.createElement('canvas');canvas.width=160;canvas.height=160;const face=await faceapi.detectSingleFace(canvas,new faceapi.TinyFaceDetectorOptions({inputSize:160}));return{loaded:faceapi.nets.tinyFaceDetector.isLoaded&&faceapi.nets.faceLandmark68Net.isLoaded&&faceapi.nets.faceRecognitionNet.isLoaded,noFace:!face}});
  check(models.loaded&&models.noFace,'Real local library/models load and reject a blank frame');
  check(errors.length===0,'No JavaScript errors');
  fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({checks,errors,physicalCameraTested:false},null,2));console.log('PASS: '+checks+' camera interface, lifecycle and local model checks; no SQL writes.');
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exit(1)});
