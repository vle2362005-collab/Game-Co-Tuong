const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {spawn} = require('node:child_process');
const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname,'..');
const temporary = require('node:os').tmpdir();
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
const {DatabaseSync} = require('node:sqlite');
async function apiCall(token,route,method='GET',body) {
  const response=await fetch(api+route,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(body!==undefined?{body:JSON.stringify(body)}:{})});
  return {status:response.status,body:await response.json()};
}
async function tokenFor(username) {const result=await apiCall(null,'/api/v1/auth/login','POST',{requestId:crypto.randomUUID(),username,password:'Password123!'});assert.equal(result.status,200);return result.body.data.accessToken;}
async function version(page,value) {await page.waitForFunction(v=>Number(document.querySelector('.game')?.dataset.version)>=v,value,{timeout:15000});}
async function moveUI(page,from,to) {await page.locator(`[data-pos="${from}"]`).click();await page.locator(`[data-pos="${to}"]`).click();}
function piece(id,type,side,col,row) {return {id,type,side,position:{col,row}};}
(async()=>{
  const dbPath=path.join(temporary,'gct-s4-browser-'+Date.now()+'.db');
  start(process.env.DOTNET_PATH||'dotnet',[path.join(root,'src/backend/GameCoTuong.Api/bin/Release/net10.0/GameCoTuong.Api.dll')],path.join(root,'src/backend/GameCoTuong.Api'),{Database__Provider:'Sqlite',ConnectionStrings__GameDatabase:'Data Source='+dbPath,ASPNETCORE_ENVIRONMENT:'Development',ASPNETCORE_URLS:api},path.join(temporary,'gct-s4-browser-api.log'));
  start(process.execPath,[path.join(root,'src/frontend/node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','5173','--strictPort'],path.join(root,'src/frontend'),{VITE_API_BASE_URL:api},path.join(temporary,'gct-s4-browser-vite.log'));
  await Promise.all([ready(api+'/health'),ready(web)]);
  browser=await chromium.launch({headless:true,...(process.env.CHROMIUM_PATH?{executablePath:process.env.CHROMIUM_PATH}:{}),args:['--no-sandbox','--no-zygote','--single-process','--in-process-gpu','--disable-features=AudioServiceOutOfProcess,IsolateOrigins,site-per-process','--disable-site-isolation-trials','--no-proxy-server','--disable-dev-shm-usage','--allow-loopback-in-peer-connection','--use-fake-device-for-media-stream','--use-fake-ui-for-media-stream','--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader'],env:{...process.env,...(process.env.CHROME_LIBS?{LD_LIBRARY_PATH:process.env.CHROME_LIBS}:{}),...(process.env.CHROME_FONTS?{FONTCONFIG_PATH:process.env.CHROME_FONTS}:{})}});
  const context=await browser.newContext({viewport:{width:1280,height:1000}});
  // Fault injection at the WebSocket boundary; application code has no test backdoors.
  await context.addInitScript(()=>{
    const Native=window.WebSocket;window.__matchFrames=[];window.__dropMatches=0;
    window.WebSocket=class extends Native {
      set onmessage(handler) {
        window.__injectMatch=snapshot=>handler(new MessageEvent('message',{data:JSON.stringify({type:1,target:'MatchChanged',arguments:[snapshot]})+'\x1e'}));
        super.onmessage=event=>{
          if(typeof event.data!=='string')return handler(event);
          const forwarded=[];
          for(const frame of event.data.split('\x1e').filter(Boolean)) {
            let value;try {value=JSON.parse(frame);}catch {forwarded.push(frame);continue;}
            if(value.type===1&&value.target==='MatchChanged') {
              window.__matchFrames.push(value.arguments[0]);
              if(window.__dropMatches>0){window.__dropMatches--;continue;}
            }
            forwarded.push(frame);
          }
          if(forwarded.length)handler(new MessageEvent('message',{data:forwarded.join('\x1e')+'\x1e'}));
        };
      }
      get onmessage(){return super.onmessage;}
    };
  });
  const red=await context.newPage(), black=await context.newPage();pages.push(red,black);
  const pageErrors=[];for(const p of pages)p.on('pageerror',e=>pageErrors.push(e.message));
  const redName=await user('s4_red'),blackName=await user('s4_black'),thirdName=await user('s4_third');
  await red.goto(web);await red.locator('#login-username').fill(redName);await red.locator('#login-password').fill('WrongPassword123!');await red.locator('#login-submit-btn').click();await waitText(red,'không chính xác');
  await login(red,redName);await login(black,blackName);
  const redToken=await tokenFor(redName),blackToken=await tokenFor(blackName),thirdToken=await tokenFor(thirdName);
  await red.getByLabel('Tên phòng').fill('S4 core acceptance');await red.getByRole('button',{name:'Tạo phòng',exact:true}).click();await red.waitForURL('**/rooms/**');
  const roomId=red.url().split('/').at(-1),code=(await red.locator('h1').innerText()).match(/\b\d{6}\b/)[0];
  await black.getByLabel('Mã phòng').fill(code);await black.getByRole('button',{name:'Tham gia',exact:true}).click();await black.waitForURL('**/rooms/**');
  assert.equal((await apiCall(thirdToken,'/api/v1/rooms/join/'+code,'POST')).status,409);
  assert.equal((await apiCall(thirdToken,'/api/v1/rooms/'+roomId)).status,403);
  await red.getByRole('button',{name:'Sẵn sàng',exact:true}).click();await black.getByRole('button',{name:'Rời phòng',exact:true}).click();await black.waitForURL('**/lobby');
  await red.getByText('Đang chờ đối thủ…').waitFor();assert.equal(await red.getByRole('button',{name:'Bắt đầu',exact:true}).isDisabled(),true);
  await black.getByLabel('Mã phòng').fill(code);await black.getByRole('button',{name:'Tham gia',exact:true}).click();await black.waitForURL('**/rooms/**');
  await red.getByRole('button',{name:'Sẵn sàng',exact:true}).click();await black.getByRole('button',{name:'Sẵn sàng',exact:true}).click();
  await red.waitForFunction(()=>Array.from(document.querySelectorAll('button')).some(b=>b.textContent==='Bắt đầu'&&!b.disabled));await red.getByRole('button',{name:'Bắt đầu',exact:true}).click();
  await Promise.all([red.waitForURL('**/game/**'),black.waitForURL('**/game/**')]);await Promise.all([version(red,1),version(black,1)]);
  const matchId=red.url().split('/').at(-1),matchRoute='/api/v1/matches/'+matchId;
  let redReads=0,blackReads=0;red.on('request',r=>{if(r.method()==='GET'&&r.url()===api+matchRoute)redReads++;});black.on('request',r=>{if(r.method()==='GET'&&r.url()===api+matchRoute)blackReads++;});
  assert.equal(await red.locator('.board image').count(),32);assert.equal(await black.locator('.board [data-pos]').count(),90);
  results.push('GCT-6/31/103/47: wrong login, real two-user room flow, full-room/outsider rejection, leave/rejoin and ready/start controls.');
  await red.locator('[data-pos="0-6"]').click();await red.locator('[data-legal="0-5"]').waitFor();await red.locator('[data-pos="0-5"]').click();await version(black,2);
  await moveUI(black,'0-3','0-4');await version(red,3);await moveUI(red,'1-7','1-0');await version(black,4);
  assert.equal(await black.locator('.board image').count(),31);await moveUI(black,'7-2','7-9');await version(red,5);
  assert.equal(await red.locator('.board image').count(),30);assert.equal(await red.locator('[data-last-to]').count(),1);assert.equal(await black.locator('[data-last-from]').count(),1);
  results.push('GCT-9/82: legal hints, both board orientations, two captures and last-move markers synchronize from confirmed snapshots.');
  await black.evaluate(()=>{window.__dropMatches=1;});await moveUI(red,'2-6','2-5');await version(red,6);
  const gapReads=blackReads;
  const forwarded=await apiCall(blackToken,matchRoute+'/moves','POST',{requestId:crypto.randomUUID(),expectedVersion:6,pieceId:'black-soldier-2',toCol:2,toRow:4});assert.equal(forwarded.status,200);
  await Promise.all([version(red,7),version(black,7)]);assert.ok(blackReads>gapReads);
  results.push('GCT-82: dropping a real event then receiving a later version triggers an authoritative API read.');
  await black.evaluate(()=>{window.__dropMatches=1;});await moveUI(red,'4-6','4-5');await version(red,8);const focusReads=blackReads;
  await black.evaluate(()=>window.dispatchEvent(new Event('focus')));await version(black,8);assert.ok(blackReads>focusReads);
  results.push('GCT-82: a missing final event is recovered by the focus snapshot check without waiting for another move.');
  const database=new DatabaseSync(dbPath);database.exec("CREATE TRIGGER reject_s4_move BEFORE INSERT ON Moves BEGIN SELECT RAISE(ABORT,'browser save failure'); END;");
  const frameCount=await red.evaluate(()=>window.__matchFrames.length);await moveUI(black,'6-3','6-4');await black.getByRole('button',{name:'Gửi lại cùng mã nước',exact:true}).waitFor();
  assert.equal((await apiCall(redToken,matchRoute)).body.data.version,8);assert.equal(await red.evaluate(()=>window.__matchFrames.length),frameCount);
  database.exec('DROP TRIGGER reject_s4_move;');await black.getByRole('button',{name:'Gửi lại cùng mã nước',exact:true}).click();await Promise.all([version(red,9),version(black,9)]);
  results.push('GCT-115: an actual SQLite write failure publishes no success; retrying the same request succeeds once.');
  const snapshot9=(await apiCall(redToken,matchRoute)).body.data;await red.locator('[data-pos="6-6"]').click();await red.locator('[data-legal="6-5"]').waitFor();
  const duplicateReads=redReads;
  await red.evaluate(s=>{window.__injectMatch(s);window.__injectMatch({...s,version:s.version-1});window.__injectMatch({...s,id:crypto.randomUUID(),version:s.version+10});},snapshot9);
  await red.evaluate(()=>new Promise(r=>requestAnimationFrame(r)));assert.equal(await red.locator('[data-selected="red-soldier-4"]').count(),1);assert.equal(redReads,duplicateReads);
  await black.getByRole('link',{name:'Sảnh chờ',exact:true}).click();await black.waitForURL('**/lobby');const leftReads=blackReads;
  await black.evaluate(s=>window.__injectMatch({...s,version:s.version+2}),snapshot9);await black.evaluate(()=>new Promise(r=>requestAnimationFrame(r)));assert.equal(blackReads,leftReads);
  await black.getByRole('button',{name:'Trở lại',exact:true}).click();await black.waitForURL('**/game/**');await version(black,9);
  results.push('GCT-82: duplicate/old/wrong-match events keep selection; leaving/re-entering removes the departed screen listener.');
  const seed=(side,pieces)=>{
    const board=JSON.stringify(pieces);database.exec('BEGIN');
    database.prepare('DELETE FROM Moves WHERE lower(MatchId)=lower(?)').run(matchId);
    database.prepare("UPDATE Matches SET InitialBoardJson=?,CurrentBoardJson=?,SideToMove=?,MoveNumber=0,Version=Version+1 WHERE lower(Id)=lower(?)").run(board,board,side,matchId);database.exec('COMMIT');
    return Number(database.prepare('SELECT Version FROM Matches WHERE lower(Id)=lower(?)').get(matchId).Version);
  };
  let current=seed('black',[piece('red-general-1','General','Red',4,9),piece('black-general-1','General','Black',4,0),piece('red-chariot-1','Chariot','Red',4,3),piece('red-soldier-5','Soldier','Red',4,5)]);
  for(const p of pages)await p.evaluate(()=>window.dispatchEvent(new Event('focus')));await Promise.all(pages.map(p=>version(p,current)));
  await waitText(black,'Chiếu Tướng!');await black.locator('[data-pos="4-0"]').click();await black.locator('[data-legal="5-0"]').waitFor();assert.equal(await black.locator('[data-check]').count(),1);assert.equal(await black.locator('[data-legal="4-1"]').count(),0);
  fs.mkdirSync(path.join(root,'artifacts/sprint4-core'),{recursive:true});await black.setViewportSize({width:390,height:844});assert.equal(await black.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true);await black.screenshot({path:path.join(root,'artifacts/sprint4-core/check-and-hints.png'),fullPage:true});
  await black.locator('[data-pos="4-1"]').click();await waitText(black,'Nước đi không hợp lệ');assert.equal((await apiCall(blackToken,matchRoute)).body.data.version,current);
  await moveUI(black,'4-0','5-0');await version(red,++current);assert.equal(await red.locator('[data-check]').count(),0);
  current=seed('red',[piece('red-general-1','General','Red',4,9),piece('black-general-1','General','Black',4,0),piece('red-soldier-5','Soldier','Red',4,5),piece('red-chariot-1','Chariot','Red',3,1),piece('red-chariot-2','Chariot','Red',5,1),piece('red-soldier-1','Soldier','Red',4,2)]);
  for(const p of pages)await p.evaluate(()=>window.dispatchEvent(new Event('focus')));await Promise.all(pages.map(p=>version(p,current)));
  await moveUI(red,'4-2','4-1');await waitText(red,'Bạn thắng');await waitText(black,'Bạn thua');assert.equal((await apiCall(redToken,matchRoute)).body.data.resultReason,'checkmate');
  database.close();results.push('GCT-10/61: seeded server positions show check and legal escapes, reject self-check, and finish a mate identically in both browsers.');
  assert.deepEqual(pageErrors,[]);fs.writeFileSync(path.join(root,'artifacts/sprint4-core/browser-results.json'),JSON.stringify({passed:true,results},null,2));console.log(JSON.stringify({passed:true,results},null,2));
})().catch(async error=>{console.error(error);for(let i=0;i<pages.length;i++){console.error('PAGE '+i+': '+await pages[i].locator('body').innerText().catch(()=>''));await pages[i].screenshot({path:path.join(temporary,'gct-s4-failure-'+i+'.png'),fullPage:true}).catch(()=>{});}process.exitCode=1;}).finally(async()=>{await browser?.close();children.forEach(p=>p.kill('SIGTERM'));});
