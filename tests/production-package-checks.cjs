const {spawn}=require('node:child_process'),fs=require('node:fs'),path=require('node:path'),net=require('node:net');
// Local smoke test of an already-published package. No database, credentials, mutations or external requests.
const [directory,evidence]=process.argv.slice(2);if(!directory||!evidence)throw Error('Supply publish directory and external evidence path');
const root=path.resolve(directory),repo=path.resolve(__dirname,'..'),out=path.resolve(evidence);
if(root===repo||out===repo||out.startsWith(repo+path.sep))throw Error('Use external package/evidence paths');
fs.mkdirSync(out,{recursive:true});let count=0;function check(ok,label){if(!ok)throw Error(label);count++;}
(async()=>{
 for(const name of ['appsettings.json','appsettings.Development.json','App_Data','uploads'])check(!fs.existsSync(path.join(root,name)),'Private/configuration exclusion: '+name);
 check(fs.existsSync(path.join(root,'appsettings.Example.json')),'Portable example remains');
 const server=net.createServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port;await new Promise(r=>server.close(r));
 const base='http://127.0.0.1:'+port;
 const env={...process.env,ASPNETCORE_ENVIRONMENT:'Production',DOTNET_ENVIRONMENT:'Production',ASPNETCORE_URLS:base,PublicSite__Origin:'https://stms.example.invalid',Passkeys__Origin:'https://stms.example.invalid',ConnectionStrings__DefaultConnection:'Server=127.0.0.1,1;Database=NoDatabaseConfigured;Integrated Security=True;Connect Timeout=1',FaceRecognition__Enabled:'false'};
 const p=spawn('dotnet',[path.join(root,'SourcingTenderManagementSystem.Web.dll')],{cwd:root,env,windowsHide:true});let logs='';
 const ready=new Promise((resolve,reject)=>{p.stdout.on('data',d=>{logs+=d;if(logs.includes('Now listening on:'))resolve();});p.stderr.on('data',d=>logs+=d);p.once('exit',code=>reject(Error('Published host exited '+code)));});
 let timer;try{
 await Promise.race([ready,new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error('Published host startup failed')),30000);})]);clearTimeout(timer);
 for(const route of ['/Account/Login','/Home/About','/Home/Contact','/Home/Privacy','/Suppliers/Register']){
 const response=await fetch(base+route,{redirect:'manual'});const html=await response.text();check(response.status===200,'Published Razor route '+route);
 const assets=[...html.matchAll(/(?:src|href)="([^"#]+)"/g)].map(x=>x[1].replaceAll('&amp;','&')).filter(x=>x.startsWith('/')&&/\.(?:css|js|png|svg|jpg|ico)(?:\?|$)/.test(x));
 for(const asset of new Set(assets))check((await fetch(base+asset)).status===200,'Published linked asset '+asset);
 }
 for(const route of ['/Employees','/Departments','/Bids','/Tenders','/App_Data/DemoPopulation.json','/appsettings.json']){
 const response=await fetch(base+route,{redirect:'manual'});check([302,401,403,404].includes(response.status),'Anonymous/private denial '+route);
 }
 const response=await fetch(base+'/Home/Contact',{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:'SenderName=Synthetic'});check(response.status===400,'Published contact rejects missing antiforgery before persistence');
 console.log('PASS: '+count+' published Production startup, Razor/asset, private-path, authorization and antiforgery checks. Loopback HTTP only; no SQL configured.');
 fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify({checks:count,passed:true,transport:'loopback HTTP; TLS deployment not certified',database:'unreachable synthetic target; no SQL operations'},null,2));
 }finally{clearTimeout(timer);p.kill();await new Promise(r=>{if(p.exitCode!==null)r();else p.once('exit',r);});fs.writeFileSync(path.join(out,'host.log'),logs);}
})().catch(e=>{console.error(e);process.exitCode=1});
