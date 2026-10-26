import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {initialState} from './model.mjs';
import {staticSvg} from './render.mjs';
const dir=path.dirname(fileURLToPath(import.meta.url));
export const scenarios=[
 ['01-login','Đăng nhập',{screen:'login'}],
 ['02-register','Đăng ký',{screen:'register'}],
 ['03-lobby','Sảnh chơi',{screen:'lobby'}],
 ['04-host-waiting','Chủ phòng · chưa sẵn sàng',{screen:'room',role:'host',guestPresent:true}],
 ['05-host-ready','Chủ phòng · đủ điều kiện Bắt đầu',{screen:'room',role:'host',guestPresent:true,ready:true,otherReady:true}],
 ['06-guest-ready','Khách · chờ chủ phòng Bắt đầu',{screen:'room',role:'guest',guestPresent:true,ready:true,otherReady:true}],
 ['07-match','Trận đang chơi',{screen:'match'}],
 ['08-move-confirmed','Nước đi đã xác nhận',{screen:'match',moved:true}],
 ['09-reconnect','Mất kết nối · trận tạm dừng',{screen:'match',paused:true,budget:45}],
 ['10-restart','Máy chủ phục hồi trận',{screen:'match',paused:true,recovery:'serverRestart',budget:45}],
 ['11-result-win','Kết quả thắng',{screen:'result',result:'win'}],
 ['12-result-draw','Kết quả hòa',{screen:'result',result:'draw'}],
 ['13-result-cancelled','Trận hủy · không tính hòa',{screen:'result',result:'cancelled'}],
 ['14-history','Lịch sử trận',{screen:'history'}],
 ['15-history-empty','Lịch sử rỗng',{screen:'history',view:'empty'}],
 ['16-lobby-empty','Sảnh rỗng',{screen:'lobby',view:'empty'}],
 ['17-loading','Đang tải',{screen:'lobby',view:'loading'}],
 ['18-error','Lỗi và thao tác khôi phục',{screen:'lobby',view:'error'}],
 ['19-room-offline','Phòng chờ mất kết nối',{screen:'room',view:'offline',guestPresent:true}],
 ['20-incoming-draw','Nhận đề nghị hòa',{screen:'match',offer:true,offerFrom:'opponent'}],
 ['21-confirm-resign','Xác nhận đầu hàng',{screen:'match',modal:'resign'}],
 ['22-confirm-close','Xác nhận đóng phòng',{screen:'room',role:'host',modal:'close',guestPresent:true}],
 ['23-login-error','Đăng nhập lỗi',{screen:'login',notice:'Thông tin chưa hợp lệ. Kiểm tra và thử lại.'}],
 ['24-session-expired','Phiên đăng nhập hết hạn',{screen:'login',returnTo:'match',notice:'Phiên đã hết hạn. Đăng nhập lại để tiếp tục.'}],
 ['25-room-empty-seat','Phòng chờ · ghế Đen trống',{screen:'room',role:'host',guestPresent:false}],
 ['26-confirm-leave','Khách xác nhận rời phòng',{screen:'room',role:'guest',modal:'leave',guestPresent:true}],
 ['27-history-detail','Chi tiết trận đã lưu',{screen:'detail',detailResult:'win'}]
];
const out=path.join(dir,'screens');fs.mkdirSync(out,{recursive:true});
for(const [id,,overrides] of scenarios)fs.writeFileSync(path.join(out,id+'.svg'),staticSvg(initialState(overrides)));
fs.writeFileSync(path.join(dir,'manifest.json'),JSON.stringify({issue:'GCT-24',version:1,kind:'interactive-design-prototype',screens:scenarios.map(([id,title,state])=>({id,title,state,svg:'screens/'+id+'.svg'}))},null,2)+'\n');
const code=(await Promise.all(['model.mjs','render.mjs','client.js'].map(p=>fs.promises.readFile(path.join(dir,p),'utf8')))).map(s=>s.replace(/^export /gm,'')).join('\n');
const options=scenarios.map(([id,title])=>`<option value="${id}">${title}</option>`).join('');
const html=`<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>GCT-24 · Cờ Tướng · Bản mẫu tương tác</title><style>
*{box-sizing:border-box}body{margin:0;background:#ecefe9;color:#191c1a;font-family:Inter,'Segoe UI',sans-serif}.review{position:sticky;top:0;z-index:5;display:flex;align-items:center;flex-wrap:wrap;gap:10px;padding:12px 24px;background:#191c1a;color:#fff;border-bottom:1px solid #303c32}.review strong{font-size:13px;margin-right:auto}.review small{font-size:11px;color:#c9d3c7}.review select,.review button{font:12px inherit;font-family:inherit;font-size:12px;border-radius:8px;border:1px solid #59645c;padding:8px 10px;color:white;background:#29362d;cursor:pointer}.review select{max-width:300px}.review button:hover{background:#3c5140}.review button:focus-visible,.review select:focus-visible{outline:3px solid #99d8a7;outline-offset:2px}.eventbar{display:flex;flex-wrap:wrap;gap:8px;padding:10px 24px;background:#fff;border-bottom:1px solid #dce1da;font-size:12px}.eventbar button{padding:7px 11px;border:1px solid #d0d8ce;border-radius:8px;background:#f7f7f4;cursor:pointer}.eventbar span{color:#68726b;align-self:center;margin-right:8px}#canvas{max-width:1440px;margin:20px auto;background:#f7f7f4;box-shadow:0 16px 70px #1c2e1712}#canvas>svg{display:block;width:100%;height:auto}.scroll{overflow-x:auto}#announcer{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}.instructions{max-width:1392px;margin:0 auto 24px;padding:0 24px;color:#68726b;font-size:12px;line-height:1.6}@media(max-width:900px){#canvas{width:1440px;margin:0;transform-origin:top left}.scroll{width:100%;} .review{position:relative;padding:12px}.review strong{width:100%}.eventbar{padding:10px 12px}.instructions{padding:12px;margin:0}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto!important}}
</style></head><body><header class="review"><strong>GCT-24 · Bản mẫu tương tác</strong><small>Dữ liệu minh họa · Chưa kết nối backend</small><label><span class="sr-only">Chọn kịch bản </span><select id="scenario" aria-label="Chọn kịch bản">${options}</select></label><button id="reset">Bắt đầu lại</button><button id="export">Tải SVG màn hình</button></header><div class="eventbar"><span>Sự kiện giả lập để review:</span><button data-event="opponent">Đối thủ vào &amp; sẵn sàng</button><button data-event="server-start">Chủ phòng bắt đầu</button><button data-event="disconnect">Mất kết nối</button><button data-event="disconnect" data-value="serverRestart">Khởi động lại máy chủ</button><button data-event="incoming-draw">Đối thủ đề nghị hòa</button><button data-event="finish" data-value="win">Đối thủ đầu hàng</button><button data-event="both-timeout">Cả hai hết hạn nối lại</button><button data-event="expire">Phiên hết hạn</button></div><main class="scroll"><div id="canvas"></div></main><p class="instructions">Luồng chính: Đăng nhập → Tạo phòng → giả lập đối thủ vào → Tôi đã sẵn sàng → Bắt đầu. Bản mẫu cho thử một nước Tốt; GCT-24 thiết kế thao tác, bộ luật và kết nối API triển khai ở các thẻ tiếp theo. Màn hình thiết kế rộng 1440px; trên màn hình nhỏ có thể cuộn ngang. SVG gồm chữ và hình vector để nhóm tiếp tục chỉnh sửa.</p><div id="announcer" role="status" aria-live="polite"></div><script>const SCENARIOS=${JSON.stringify(scenarios)};\n${code}</script></body></html>`;
fs.writeFileSync(path.join(dir,'prototype.html'),html);
console.log(JSON.stringify({screens:scenarios.length,prototype:'prototype.html',externalRequests:0}));
