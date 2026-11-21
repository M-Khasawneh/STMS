/* Shared camera capture; authorization and optional sign-in stay on the server. */
(() => {
    'use strict';
    const root = document.querySelector('[data-face-settings], [data-face-login]');
    if (!root) return;
    const status = root.querySelector('[data-face-status]');
    const preview = root.querySelector('[data-face-preview]');
    const video = preview.querySelector('video');
    const password = root.querySelector('[data-face-password]');
    const cancel = root.querySelector('[data-face-cancel]');
    const buttons = [...root.querySelectorAll('[data-face-action]')];
    let generation = 0, stream, busy = false, modelPromise;
    const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
    function stopCamera() { stream?.getTracks().forEach(track => track.stop()); stream = null; video.srcObject = null; preview.hidden = true; }
    function finish(message) {
        generation++; busy = false; stopCamera(); cancel.hidden = true;
        buttons.forEach(button => { button.disabled = false; button.removeAttribute('aria-busy'); });
        status.textContent = message;
    }
    async function post(url, data) {
        const response = await fetch(url, { method:'POST', credentials:'same-origin', redirect:'error', headers:{'Content-Type':'application/json','RequestVerificationToken':root.querySelector('[name="__RequestVerificationToken"]').value}, body:JSON.stringify(data), signal:AbortSignal.timeout(15000) });
        let body; try { body = await response.json(); } catch { /* Authentication or server errors are not recognition results. */ }
        if (!response.ok) throw Error(body?.error || (response.status === 429 ? 'Too many attempts. Wait a minute and try again.' : 'Could not complete this operation. Refresh the page and try again.'));
        return body;
    }
    async function loadModels() {
        if (modelPromise) return modelPromise;
        modelPromise = (async () => {
            if (!window.faceapi) await new Promise((resolve,reject) => {
                const script = document.createElement('script'); script.src = root.dataset.library;
                script.onload = resolve; script.onerror = () => { script.remove(); reject(Error('Face recognition could not load. Try again.')); }; document.head.append(script);
            });
            const nets = window.faceapi.nets;
            await Promise.all([nets.tinyFaceDetector.loadFromUri(root.dataset.models),nets.faceLandmark68Net.loadFromUri(root.dataset.models),nets.faceRecognitionNet.loadFromUri(root.dataset.models)]);
        })().catch(error => { modelPromise = null; throw error; });
        return modelPromise;
    }
    async function capture(ticket) {
        if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) throw Error('Camera access needs HTTPS and a compatible browser.');
        const camera = await navigator.mediaDevices.getUserMedia({video:{facingMode:'user',width:{ideal:640},height:{ideal:480}},audio:false});
        // Permission prompts may resolve after cancellation or page visibility changed.
        if (ticket !== generation) { camera.getTracks().forEach(track => track.stop()); return null; }
        stream = camera; video.srcObject = camera; preview.hidden = false; await video.play();
        const api = window.faceapi, samples = [], start = performance.now();
        // Laptop cameras may return widescreen frames even when 640x480 is requested.
        // Preserve their aspect ratio; stretching changes face landmark geometry.
        const sourceWidth = video.videoWidth || 640, sourceHeight = video.videoHeight || 480;
        const scale = Math.min(1, 640 / Math.max(sourceWidth, sourceHeight));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(sourceWidth * scale));
        canvas.height = Math.max(1, Math.round(sourceHeight * scale));
        const context = canvas.getContext('2d',{willReadFrequently:true});
        let lastTime = -1, lastSample = 0;
        while (ticket === generation && performance.now() - start < 30000 && samples.length < 5) {
            if (video.readyState < 2 || video.currentTime === lastTime) { await delay(80); continue; }
            lastTime = video.currentTime; context.drawImage(video,0,0,canvas.width,canvas.height);
            const faces = await api.detectAllFaces(canvas,new api.TinyFaceDetectorOptions({inputSize:320,scoreThreshold:.5})).withFaceLandmarks().withFaceDescriptors();
            if (ticket !== generation) return null;
            if (faces.length !== 1 || faces[0].detection.box.width < canvas.width / 8) {
                status.textContent = faces.length > 1 ? 'Only one person should be in the frame.' : 'Move into the frame and improve the lighting.';
                samples.length = 0;
            } else if (performance.now() - lastSample >= 900) {
                samples.push(Array.from(faces[0].descriptor)); lastSample = performance.now();
                status.textContent = `Capturing face samples ${samples.length} of 5. Keep looking at the camera.`;
            }
            await delay(140);
        }
        if (ticket !== generation) return null;
        if (samples.length < 5) throw Error('Face capture timed out. Try again in better lighting.');
        return samples;
    }
    buttons.forEach(button => button.addEventListener('click',async () => {
        if (busy) return;
        const operation = button.dataset.faceAction;
        const login = operation === 'login';
        const username = document.getElementById('UserName');
        if (login && !username?.value.trim()) { status.textContent = 'Enter your username above first.'; username?.focus(); return; }
        if (!login && operation !== 'verify' && !password.value) { status.textContent = 'Enter your current password first.'; password.focus(); return; }
        if (operation === 'remove' && !confirm('Remove your encrypted face template? Device passkeys are unaffected.')) return;
        busy = true; const ticket = ++generation;
        buttons.forEach(item => item.disabled = true); button.setAttribute('aria-busy','true'); cancel.hidden = false;
        status.textContent = operation === 'remove' ? 'Removing your template…' : 'Preparing camera capture. Models load locally on first use…';
        try {
            const begin = await post(root.dataset.begin,login ? {userName:username.value.trim()} : {operation,currentPassword:password.value});
            if (password) password.value = '';
            if (ticket !== generation) return;
            let samples = [];
            if (['login','enroll','verify'].includes(operation)) {
                await loadModels(); if (ticket !== generation) return;
                samples = await capture(ticket); if (!samples || ticket !== generation) return;
                stopCamera();
            }
            status.textContent = 'Checking your account-linked template…';
            const result = await post(root.dataset.complete,{token:begin.token,version:root.dataset.version,samples,returnUrl:login ? document.querySelector('[name="ReturnUrl"]')?.value : undefined});
            if (ticket !== generation) return;
            if (login) {
                const destination = new URL(result.redirectUrl, location.origin);
                if (destination.origin !== location.origin) throw Error('Invalid sign-in destination.');
                finish('Signed in. Opening your workspace…'); location.assign(destination.href);
            }
            else if (operation === 'verify') finish(result.matched ? 'Your face matches the registered template. Your sign-in session is unchanged.' : 'No match. Try again in better lighting.');
            else { finish(operation === 'remove' ? 'Template removed.' : 'Encrypted template saved.'); location.reload(); }
        } catch (error) { if (ticket === generation) finish(error.name === 'NotAllowedError' ? 'Camera permission was denied. Allow access in your browser settings to try again.' : error.message || 'Camera operation failed. Try again.'); }
    }));
    cancel.addEventListener('click',() => finish('Camera capture cancelled.'));
    document.addEventListener('keydown',event => { if (event.key === 'Escape' && busy) { event.preventDefault(); finish('Camera capture cancelled.'); } });
    document.addEventListener('visibilitychange',() => { if (document.hidden && busy) finish('Camera stopped when the page was hidden.'); });
    window.addEventListener('pagehide',() => { generation++; stopCamera(); });
})();
