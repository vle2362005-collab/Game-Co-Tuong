const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {spawn} = require('node:child_process');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname,'..');
const api = 'http://127.0.0.1:5000', web = 'http://127.0.0.1:5173';
const results = [];
const children = [];
let browser;
const pages=[];
async function ready(url) {
  for(let n=0;n<150;n++) { try { if((await fetch(url)).ok) return; } catch {} await new Promise(r=>setTimeout(r,100)); }
  throw new Error('Server did not start: '+url);
}
function start(command,args,cwd,env,log) {
  const fd=fs.openSync(log,'w'); const child=spawn(command,args,{cwd,env:{...process.env,...env},stdio:['ignore',fd,fd]});children.push(child);
}
async function user(name) {
  const username=`${name}_${Date.now()}`;
  const response=await fetch(api+'/api/v1/auth/register',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({requestId:crypto.randomUUID(),username,displayName:name,password:'Password123!',email:username+'@example.com'})});
  assert.equal(response.status,201,await response.text());return username;
}
async function login(page,username) {
  await page.goto(web);await page.locator('#login-username').fill(username);await page.locator('#login-password').fill('Password123!');await page.locator('#login-submit-btn').click();await page.waitForURL('**/lobby');
}
async function waitText(page,value) { await page.getByText(value,{exact:false}).first().waitFor({timeout:20000}); }
(async()=>{
  const db='/tmp/gct-browser-'+Date.now()+'.db';
  start(process.env.DOTNET_PATH||'dotnet',[path.join(root,'src/backend/GameCoTuong.Api/bin/Debug/net10.0/GameCoTuong.Api.dll')],path.join(root,'src/backend/GameCoTuong.Api'),{Database__Provider:'Sqlite',ConnectionStrings__GameDatabase:'Data Source='+db,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:api},'/tmp/gct-e2e-api.log');
  start(process.execPath,[path.join(root,'src/frontend/node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','5173','--strictPort'],path.join(root,'src/frontend'),{VITE_API_BASE_URL:api},'/tmp/gct-e2e-vite.log');
  await Promise.all([ready(api+'/health'),ready(web)]);
  browser=await chromium.launch({headless:true,...(process.env.CHROMIUM_PATH?{executablePath:process.env.CHROMIUM_PATH}:{}),args:['--no-sandbox','--no-zygote','--single-process','--in-process-gpu','--disable-features=AudioServiceOutOfProcess,IsolateOrigins,site-per-process','--disable-site-isolation-trials','--no-proxy-server','--disable-dev-shm-usage','--allow-loopback-in-peer-connection','--use-fake-device-for-media-stream','--use-fake-ui-for-media-stream','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'],env:{...process.env,...(process.env.CHROME_LIBS?{LD_LIBRARY_PATH:process.env.CHROME_LIBS}:{}),...(process.env.CHROME_FONTS?{FONTCONFIG_PATH:process.env.CHROME_FONTS}:{}),...(process.env.VK_ICD_FILENAMES?{VK_ICD_FILENAMES:process.env.VK_ICD_FILENAMES}:{})}});
  const context=await browser.newContext({permissions:['camera','microphone'],viewport:{width:1280,height:1000}});
  await context.addInitScript(() => { const Native=window.RTCPeerConnection; window.__testPeers=[]; window.RTCPeerConnection=class extends Native { constructor(...args){ super(...args); window.__testPeers.push(this) } } });
  const [red,black]=await Promise.all([context.newPage(),context.newPage()]);
  pages.push(red,black);
  const errors=[];for(const page of [red,black]) page.on('pageerror',e=>errors.push(e.message));
  await login(red,await user('s3_red'));await login(black,await user('s3_black'));
  await red.getByLabel('Tên phòng').fill('Sprint 3 integration');await red.getByRole('button',{name:'Tạo phòng',exact:true}).click();await red.waitForURL('**/rooms/**');
  const code=(await red.locator('h1').innerText()).match(/\b\d{6}\b/)[0];
  await black.getByLabel('Mã phòng').fill(code);await black.getByRole('button',{name:'Tham gia',exact:true}).click();await black.waitForURL('**/rooms/**');
  await red.getByRole('button',{name:'Sẵn sàng',exact:true}).click();await black.getByRole('button',{name:'Sẵn sàng',exact:true}).click();
  await red.waitForFunction(()=>Array.from(document.querySelectorAll('button')).some(b=>b.textContent==='Bắt đầu'&&!b.disabled));
  await red.getByRole('button',{name:'Bắt đầu',exact:true}).click();await Promise.all([red.waitForURL('**/game/**'),black.waitForURL('**/game/**')]);
  await Promise.all([red.locator('.board image').first().waitFor(),black.locator('.board image').first().waitFor()]);
  assert.equal(await red.locator('.board image').count(),32);assert.equal(await black.locator('.board image').count(),32);results.push('Two authenticated users create/join/ready/start the same match with 32 SVG pieces.');
  await red.getByPlaceholder('Nhắn với đối thủ…').fill('Hello Sprint 3 <script>literal</script>');await red.getByRole('button',{name:'Gửi',exact:true}).click();await waitText(black,'Hello Sprint 3 <script>literal</script>');results.push('Room chat arrives in the other browser and renders HTML as literal text.');
  await red.locator('[data-pos="0-9"]').click();await red.locator('[data-pos="1-8"]').click();await waitText(red,'Nước đi không hợp lệ');
  await red.locator('[data-pos="0-6"]').click();await red.locator('[data-pos="0-5"]').click();await waitText(black,'Nước 1');
  await black.locator('[data-pos="0-3"]').click();await black.locator('[data-pos="0-4"]').click();await waitText(red,'Nước 2');results.push('Invalid move is rejected; valid red and flipped-black moves synchronize.');
  await red.getByRole('button',{name:'Gọi video',exact:true}).click();await waitText(black,'Có cuộc gọi đến');await black.getByRole('button',{name:'Nhận cuộc gọi',exact:true}).click();
  for(const page of [red,black]) { await waitText(page,'Đã kết nối');await page.waitForFunction(()=>{const v=document.querySelector('video[aria-label="Video đối thủ"]');return v&&v.videoWidth>0&&v.srcObject?.getAudioTracks().length>0},{timeout:20000}); }
  results.push('Real WebRTC connection exchanges video frames and audio tracks using synthetic camera/microphone in two browsers.');
  await red.getByRole('button',{name:'Tắt mic',exact:true}).click();await red.getByRole('button',{name:'Tắt camera',exact:true}).click();
  assert.equal(await red.locator('video[aria-label="Video của bạn"]').evaluate(v=>v.srcObject.getTracks().every(t=>!t.enabled)),true);
  fs.mkdirSync(path.join(root,'artifacts/sprint3'),{recursive:true});await red.screenshot({path:path.join(root,'artifacts/sprint3/desktop.png'),fullPage:true});
  await red.getByRole('button',{name:'Kết thúc',exact:true}).click();await waitText(black,'Chưa gọi');
  assert.equal(await red.locator('video[aria-label="Video của bạn"]').evaluate(v=>v.srcObject===null),true);results.push('Mic/camera toggles work; ending the call clears both sides and releases local media.');
  await black.setViewportSize({width:390,height:844});assert.equal(await black.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true);
  await black.screenshot({path:path.join(root,'artifacts/sprint3/mobile.png'),fullPage:true});results.push('390px mobile layout has no horizontal overflow.');
  red.on('dialog',d=>d.accept());await red.getByRole('button',{name:'Đầu hàng',exact:true}).click();await waitText(black,'Bạn thắng');results.push('Resignation finishes both screens with the same winner.');
  assert.deepEqual(errors,[]);fs.writeFileSync(path.join(root,'artifacts/sprint3/browser-results.json'),JSON.stringify({passed:true,results},null,2));console.log(JSON.stringify({passed:true,results},null,2));
})().catch(async error=>{console.error(error);for(let i=0;i<pages.length;i++){console.error('PAGE '+i+': '+await pages[i].locator('body').innerText().catch(()=>''));console.error(await pages[i].evaluate(()=>window.__testPeers?.map(p=>({state:p.connectionState,ice:p.iceConnectionState,local:p.localDescription?.sdp.match(/a=candidate/g)?.length,remote:p.remoteDescription?.sdp.match(/a=candidate/g)?.length}))).catch(()=>null));await pages[i].screenshot({path:'/tmp/gct-failure-'+i+'.png',fullPage:true}).catch(()=>{})}process.exitCode=1}).finally(async()=>{await browser?.close();children.forEach(p=>p.kill('SIGTERM'))});
