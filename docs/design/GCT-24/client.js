let state=initialState(),drawTimer;
const canvas=document.getElementById('canvas');
const fields=()=>Object.fromEntries([...canvas.querySelectorAll('input')].map(n=>[n.name,n.value]));
function show(){
 const active=document.activeElement?.getAttribute('aria-label');
 canvas.innerHTML=render(state);
 document.getElementById('announcer').textContent=state.notice||'Màn hình '+state.screen;
 const scope=canvas.querySelector('[role="dialog"]')||canvas;
 const next=[...scope.querySelectorAll('[role="button"],input')].find(n=>n.getAttribute('aria-label')===active);
 if(next)next.focus();else if(state.modal)scope.querySelector('[role="button"]')?.focus();
}
function dispatch(action,value=''){
 if(action==='help'){state={...state,modal:'help'};show();return;}
 const f=fields();
 if(action==='submit-login'||action==='submit-register'){
  const valid=String(f.username||'').trim().length>=3&&String(f.password||'').length>=8;
  const validRegister=action!=='submit-register'||(/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(f.email||'')&&f.password===f.confirm);
  if(!valid||!validRegister){state={...state,notice:'Thông tin chưa hợp lệ. Kiểm tra và thử lại.'};show();return;}
  action=action==='submit-login'?'login':'register';
 }
 if(action==='join-code'){
  if(!/^[A-Z0-9]{6}$/.test(String(f.roomCode||'').trim().toUpperCase())){state={...state,notice:'Mã phòng cần đúng 6 ký tự A–Z hoặc 0–9.'};show();return;}
  action='join';
 }
 if(action==='send-chat'){action='chat';value=f.chat||'';}
 if(action==='offer'||action==='incoming-draw'){
  clearTimeout(drawTimer);drawTimer=setTimeout(()=>{state=transition(state,'draw-expired');show();},30000);
 }
 const before=state.screen;state=transition(state,action,value);show();
 if(before!==state.screen&&!state.modal)window.scrollTo({top:0,behavior:'instant'});
}
canvas.addEventListener('click',e=>{const b=e.target.closest('[data-action]');if(b)dispatch(b.dataset.action,b.dataset.value);});
canvas.addEventListener('keydown',e=>{
 if(e.key==='Escape'&&state.modal){e.preventDefault();dispatch('cancel');return;}
 if(e.key==='Tab'&&state.modal){const nodes=[...canvas.querySelectorAll('[role="dialog"] [role="button"]')];const first=nodes[0],last=nodes.at(-1);if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}return;}
 if(e.key==='Enter'&&e.target.tagName.toLowerCase()==='input'){e.preventDefault();dispatch(e.target.name==='chat'?'send-chat':e.target.name==='roomCode'?'join-code':state.screen==='register'?'submit-register':'submit-login');return;}
 if(e.key==='Enter'||e.key===' '){const b=e.target.closest('[data-action]');if(b){e.preventDefault();dispatch(b.dataset.action,b.dataset.value);}}
});
document.querySelectorAll('[data-event]').forEach(b=>b.addEventListener('click',()=>dispatch(b.dataset.event,b.dataset.value||'')));
document.getElementById('scenario').addEventListener('change',e=>{clearTimeout(drawTimer);const entry=SCENARIOS.find(x=>x[0]===e.target.value);state=initialState(entry[2]);show();});
document.getElementById('reset').addEventListener('click',()=>{clearTimeout(drawTimer);state=initialState();document.getElementById('scenario').selectedIndex=0;show();});
document.getElementById('export').addEventListener('click',()=>{const url=URL.createObjectURL(new Blob([staticSvg(state)],{type:'image/svg+xml;charset=utf-8'}));const a=document.createElement('a');a.href=url;a.download='GCT-24-'+state.screen+'.svg';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);});
show();
