// Interaction model for a design prototype. No backend, account storage, or rules engine.
export const screens=['login','register','lobby','room','match','result','history','detail'];
export function initialState(overrides={}){return {screen:'login',view:'normal',role:'host',ready:false,otherReady:false,guestPresent:false,paused:false,recovery:'disconnect',budget:60,selected:false,moved:false,offer:false,offerFrom:'self',result:'win',modal:null,notice:'',messages:[],filter:'all',chatText:'',returnTo:null,panel:'chat',call:'idle',mic:false,camera:false,callError:'',...overrides};}
export function canStart(s){return s.screen==='room'&&s.role==='host'&&s.guestPresent&&s.ready&&s.otherReady&&s.view==='normal';}
export function transition(state,action,value=''){
 const s={...state,notice:'',messages:[...state.messages]};
 const stopCall=()=>Object.assign(s,{call:'idle',mic:false,camera:false,callError:'',panel:'chat'});
 const go=screen=>{if(s.screen!==screen&&!(s.screen==='room'&&screen==='match'))stopCall();return Object.assign(s,{screen,view:'normal',modal:null,notice:''});};
 const inRoom=['room','match'].includes(s.screen),canCall=inRoom&&s.view==='normal'&&!s.paused&&(s.screen==='match'||s.guestPresent);
 if(action==='panel'){if(inRoom&&['chat','video'].includes(value))s.panel=value;return s;}
 if(action==='call-open'){if(canCall&&['idle','ended','error'].includes(s.call))Object.assign(s,{panel:'video',call:'preview',mic:false,camera:false,callError:''});else s.notice='Chờ đối thủ online trong cùng phòng để gọi.';return s;}
 if(action==='call-send'){if(canCall&&s.call==='preview')s.call='calling';return s;}
 if(action==='call-incoming'){if(canCall&&['idle','ended','error'].includes(s.call))Object.assign(s,{panel:'video',call:'incoming',mic:false,camera:false,callError:''});return s;}
 if(action==='call-accept'){if(canCall&&s.call==='incoming')Object.assign(s,{call:'connected',mic:false,camera:false});return s;}
 if(action==='call-answered'){if(canCall&&s.call==='calling')s.call='connected';return s;}
 if(action==='call-decline'){if(s.call==='incoming')Object.assign(s,{call:'ended',mic:false,camera:false,callError:'declined'});return s;}
 if(action==='call-end'){if(inRoom&&s.call!=='idle')Object.assign(s,{call:'ended',mic:false,camera:false,callError:''});return s;}
 if(action==='call-mic'||action==='call-camera'){if(canCall&&['preview','calling','connected'].includes(s.call)){const key=action==='call-mic'?'mic':'camera';s[key]=!s[key];}return s;}
 if(action==='call-error'){if(inRoom&&['permission','missing','busy','network'].includes(value))Object.assign(s,{panel:'video',call:'error',callError:value,mic:false,camera:false});return s;}
 if(action==='call-network-lost'){if(inRoom&&['connected','calling'].includes(s.call))Object.assign(s,{call:'reconnecting',mic:false,camera:false});return s;}
 if(action==='call-retry'){if(canCall&&['error','reconnecting','ended'].includes(s.call))Object.assign(s,{call:'preview',callError:'',mic:false,camera:false});return s;}
 if(action==='go'){if(s.screen==='match'&&!['match','result'].includes(value)){s.notice='Trận đang diễn ra. Hãy đầu hàng nếu muốn kết thúc.';return s;}if(['room','result'].includes(s.screen)&&value!==s.screen){s.modal='leave';s.pendingNav=value;return s;}go(value);return s;}
 if(action==='detail'){go('detail');s.detailResult=['win','draw','cancelled'][Number(value)]||'win';s.detailOpponent=['Minh An','Bảo Anh','Hải Nam'][Number(value)]||'Minh An';s.role='host';return s;}
 if(action==='login'){go(s.returnTo||'lobby');s.returnTo=null;s.notice='Đăng nhập thành công.';return s;}
 if(action==='register'){go('login');s.notice='Tạo tài khoản thành công. Bạn có thể đăng nhập.';return s;}
 if(action==='logout'){if(s.screen==='match'){s.notice='Hãy kết thúc trận trước khi đăng xuất.';return s;}go('login');return s;}
 if(action==='view'){s.view=value;s.modal=null;return s;}
 if(action==='retry'){s.view='normal';s.notice='Đã tải lại dữ liệu mới nhất.';return s;}
 if(action==='expire'){s.returnTo=s.screen;stopCall();s.screen='login';s.modal=null;s.view='normal';s.notice='Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục.';return s;}
 if(action==='filter'){s.filter=s.filter==='all'?'fast':'all';return s;}
 if(action==='create'){go('room');Object.assign(s,{role:'host',guestPresent:false,ready:false,otherReady:false,roomName:'Khai cuộc',opponent:'Minh An'});return s;}
 if(action==='join'){go('room');Object.assign(s,{role:'guest',guestPresent:true,ready:false,otherReady:true,opponent:value||'Minh An',roomName:({'Minh An':'Thư giãn','Bảo Anh':'Cờ cùng bạn','Hải Nam':'Khai cuộc'})[value]||'Khai cuộc'});return s;}
 if(action==='opponent'){if(s.screen==='room'){s.guestPresent=true;s.otherReady=true;s.notice='Đối thủ đã vào phòng và sẵn sàng.';}return s;}
 if(action==='ready'){if(s.screen==='room'&&s.view==='normal')s.ready=!s.ready;return s;}
 if(action==='start'||action==='server-start'){
  const allowed=action==='start'?canStart(s):(s.screen==='room'&&s.role==='guest'&&s.guestPresent&&s.ready&&s.otherReady&&s.view==='normal');
  if(allowed){go('match');Object.assign(s,{paused:false,recovery:'disconnect',budget:60,moved:false,selected:false,offer:false});}else s.notice='Chỉ chủ phòng được bắt đầu khi cả hai người sẵn sàng và online.';
  return s;
 }
 if(action==='leave'||action==='close'){
  if(s.screen==='match'){s.notice='Không thể rời hoặc đóng phòng khi trận đang diễn ra.';return s;}
  if(action==='close'&&s.role!=='host'){s.notice='Chỉ chủ phòng có quyền đóng phòng.';return s;}
  if(s.screen==='room'||s.screen==='result')s.modal=action;return s;
 }
 if(action==='resign'){if(s.screen==='match'&&s.view!=='offline')s.modal='resign';return s;}
 if(action==='confirm'){
  const m=s.modal;s.modal=null;
  if(m==='resign'){go('result');s.result='loss';s.offer=false;s.paused=false;}
  else if(m==='close'||m==='leave'){go(s.pendingNav||'lobby');s.pendingNav=null;s.notice=m==='close'||s.role==='host'?'Phòng đã đóng. Kết quả đã lưu vẫn còn trong lịch sử.':'Bạn đã rời phòng.';}
  return s;
 }
 if(action==='cancel'){s.modal=null;s.pendingNav=null;return s;}
 if(action==='select'){if(s.screen==='match'&&s.role==='host'&&!s.paused&&s.view==='normal'&&!s.moved)s.selected=!s.selected;return s;}
 if(action==='move'){if(s.screen==='match'&&s.role==='host'&&!s.paused&&s.view==='normal'&&s.selected&&!s.moved){s.moved=true;s.selected=false;s.offer=false;s.notice='Đã xác nhận nước đi. Đến lượt đối thủ.';}return s;}
 if(action==='offer'){if(s.screen==='match'&&!s.paused&&s.view==='normal'&&!s.offer){s.offer=true;s.offerFrom='self';s.notice='Đã gửi đề nghị hòa. Đồng hồ trận vẫn chạy.';}return s;}
 if(action==='incoming-draw'){if(s.screen==='match'&&!s.paused&&s.view==='normal'){s.offer=true;s.offerFrom='opponent';}return s;}
 if(action==='accept'){if(s.screen==='match'&&s.offer&&s.offerFrom==='opponent'&&!s.paused){go('result');s.result='draw';s.offer=false;}return s;}
 if(action==='decline'){s.offer=false;s.notice='Đã từ chối đề nghị hòa.';return s;}
 if(action==='draw-expired'){s.offer=false;s.notice='Đề nghị hòa đã hết hiệu lực.';return s;}
 if(action==='disconnect'){
  if(['preview','calling','incoming','connected'].includes(s.call))Object.assign(s,{call:'reconnecting',mic:false,camera:false});
  if(s.screen==='match'){s.paused=true;s.view='normal';s.offer=false;s.selected=false;s.recovery=value==='serverRestart'?'serverRestart':'disconnect';s.notice='Trận tạm dừng. Nước đi và đồng hồ được giữ lại.';}
  else{s.view='offline';s.ready=false;s.otherReady=false;}
  return s;
 }
 if(action==='reconnect'){
  if(s.screen==='match'&&s.paused){s.budget=Math.max(0,s.budget-15);if(s.budget===0){go('result');s.result=s.recovery==='serverRestart'?'cancelled':'loss';}else{s.paused=false;s.notice='Đã nối lại và đồng bộ. Ngân sách phục hồi còn '+s.budget+' giây.';}}
  else{s.view='normal';s.ready=false;s.otherReady=false;s.notice='Đã nối lại. Vui lòng xác nhận sẵn sàng lại.';}
  return s;
 }
 if(action==='both-timeout'){if(s.screen==='match'){go('result');s.result='cancelled';s.paused=false;s.offer=false;}return s;}
 if(action==='finish'){if(s.screen==='match'){go('result');s.result=value||'win';s.paused=false;s.offer=false;}return s;}
 if(action==='again'){go('room');Object.assign(s,{role:'host',guestPresent:false,ready:false,otherReady:false});s.notice='Đã tạo phòng mới cho trận tiếp theo.';return s;}
 if(action==='chat'){const msg=String(value).trim();if((s.screen==='room'||s.screen==='match')&&s.view==='normal'&&msg.length>0&&msg.length<=500)s.messages.push(msg);else s.notice='Tin nhắn cần từ 1 đến 500 ký tự và có kết nối.';return s;}
 if(action==='copy'){s.notice='Mã phòng: GCT8K2';return s;}
 return s;
}
