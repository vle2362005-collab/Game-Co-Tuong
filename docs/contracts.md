# GCT-20 — Hợp đồng API và SignalR v1

Nguồn dữ liệu chung: [`schemas.json`](../contracts/schemas.json). REST: [`openapi.json`](../contracts/openapi.json). Hub: [`signalr.json`](../contracts/signalr.json). Mẫu: [`examples.json`](../contracts/examples.json).

## 1. Quy ước dữ liệu

| Nội dung | Quy định |
|---|---|
| Tên thuộc tính | camelCase; enum chuỗi, phân biệt hoa/thường; thuộc tính thừa bị từ chối |
| ID | UUID dạng chuỗi; ví dụ chỉ dùng UUID giả |
| Thời gian | ISO 8601 UTC kết thúc bằng `Z`; serverTime của máy chủ là mốc đồng hồ |
| Version | Số nguyên từ 1, tối đa 2^53−1 để JavaScript không mất chính xác |
| Bàn cờ | 9 cột `col=0..8`, 10 hàng `row=0..9`; Đen hàng 0 phía trên, Đỏ hàng 9 phía dưới |
| Quân | `general`, `advisor`, `elephant`, `horse`, `chariot`, `cannon`, `soldier`; màu `red`/`black` |
| Vị trí ban đầu | Đen: hàng chính 0, pháo 2, tốt 3; Đỏ: hàng chính 9, pháo 7, tốt 6. Tốt tại cột 0,2,4,6,8; pháo tại 1,7 |
| Lượt | Đỏ đi trước. `moveNumber` là số nước đơn đã chấp nhận; `ply=moveNumber` của nước vừa lưu |
| Người gửi | Server lấy userId và sender từ phiên đăng nhập; không nhận userId từ lệnh đi quân/chat |
| Chuỗi nhập | Tên phòng, displayName và chat được trim, không được chỉ chứa khoảng trắng; chat là plain text, hiển thị bằng text binding |

Schema kiểm tra kiểu, giới hạn và trạng thái cơ bản. Service/engine vẫn kiểm tra ID quân duy nhất, ô không trùng, màu khớp ID, quyền, vị trí nguồn, lượt, deadline và toàn bộ luật. JSON Schema không thay thế các kiểm tra này. Quyết định nghiệp vụ nằm ở [requirements.md](requirements.md), các chuyển trạng thái ở [lifecycle.md](lifecycle.md).

Room giữ `id,code,name,hostUserId,status,version,players,timeControl,currentMatchId,createdAt`. `code` gồm sáu ký tự A–Z/0–9, normalize trim/uppercase, unique trong phòng chưa đóng. Tối đa hai ghế; host Đỏ, guest Đen. Mỗi người có `isReady,isConnected,reconnectDeadlineAt`; trước trận offline có deadline 60s, online/terminal đặt null. “Sẵn sàng” là waiting đủ hai người ready và online, không thêm enum room.

Match giữ `id,roomId,mode,status,version,moveNumber,players,board,sideToMove,clock,recovery,pendingDraw,ruleSetVersion,result,startedAt,endedAt`. `mode=pvp`, `ruleSetVersion=gct-core-1`, trạng thái `playing/paused/finished/cancelled`. `board` chỉ có quân còn trên bàn. Kết quả lấy từ result, không suy người thắng từ sideToMove. finished có thể là hòa (`winner=null,reason=agreement`); cancelled có winner=null với lý do bothDisconnected/serverRestart, không tính là hòa.

`Clock` lưu remainingMs tại `turnStartedAt`. Khi playing, UI tính bên tới lượt: `max(0, remainingMs - (ước_lượng_giờ_server - turnStartedAt))`; bên kia giữ nguyên. Move hợp lệ trừ thời gian, cộng increment, đổi lượt trong transaction. Pause/kết thúc chốt remainingMs và đặt turnStartedAt=null; resume đặt lại turnStartedAt=serverTime. UI không tự chốt thắng khi thấy 0.

`Recovery` luôn có `{reason,red,black}`. Mỗi bên gồm `{remainingMs,disconnectedAt,deadlineAt}`; ban đầu 60000/null/null. Offline mở `deadlineAt=disconnectedAt+remainingMs`; reconnect hợp lệ lưu `max(0,remainingMs-elapsed)`, xóa hai timestamp, không reset số dư. `reason=null` khi playing/terminal; paused dùng disconnect/serverRestart. Paused có ít nhất một bên chờ phục hồi, clock dừng và pendingDraw=null. Trong trận, UI dùng deadline trong match.recovery; deadline trong RoomPlayer chỉ dành trước trận.

`pendingDraw` là null hoặc `{id,offeredBy,createdAt,expiresAt}`. Hiệu lực 30 giây chỉ khi playing PvP; người khác offeredBy mới được accept/decline đúng offerId. Nước mới, pause, terminal, từ chối hoặc hết hạn xóa đề nghị. Clock vẫn chạy khi chờ hòa. Worker và handler đều kiểm deadline, không chỉ đợi worker xóa offer.

## 2. Request và response

Mọi POST có `requestId` UUID. Lệnh thay đổi room/match đã có còn phải gửi `expectedVersion` của **đúng aggregate đang sửa**. CreateRoom chưa có version; chat có sequence riêng nên không dùng expectedVersion. Các lệnh auth có requestId để tra log, không cache kết quả đăng nhập hay JWT trong command receipts.

Ví dụ đi Tốt Đỏ từ `(0,6)` đến `(0,5)`:

```json
{
  "requestId": "00000000-0000-4000-8000-000000000101",
  "expectedVersion": 1,
  "pieceId": "red-soldier-1",
  "from": {"col": 0, "row": 6},
  "to": {"col": 0, "row": 5}
}
```

Phản hồi thành công luôn là `{requestId,serverTime,data}`. Phản hồi lỗi là `{requestId,serverTime,error}`; không có cả data và error. GET được server gán requestId. Nếu requestId thiếu/sai định dạng, server sinh mã mới cho phản hồi VALIDATION_ERROR.

`MoveAcceptedResponse.data` có `{move,match}`. `move.matchVersion` phải bằng `match.version`, `move.ply` bằng `match.moveNumber`; `move.requestId` bằng requestId của lệnh. Frontend nhận snapshot chính thức để thay thế trạng thái tương ứng.

## 3. Danh mục REST

Tất cả đường dẫn dưới đây bắt đầu bằng `/api/v1`. Trừ register/login, tất cả yêu cầu Bearer JWT. GET trả 200; POST trả 200 trừ hai trường hợp 201 đã ghi rõ. Tham số ID là UUID. Mẫu request/response chi tiết nằm trong OpenAPI.

| Method | Đường dẫn | Request schema | Response schema / điều kiện |
|---|---|---|---|
| POST | `/auth/register` | RegisterRequest | 201 UserResponse; username/email unique sau normalize; email chỉ dùng cho tài khoản, không có trong UserPublic |
| POST | `/auth/login` | LoginRequest | AuthResponse; trả accessToken có expiresAt |
| POST | `/auth/logout` | CommandRequest | AckResponse; thu hồi jti đến khi hết hạn |
| GET | `/users/me` | — | UserResponse |
| GET | `/rooms` | cursor, limit | RoomsResponse; chỉ phòng waiting, không trả chat |
| POST | `/rooms` | CreateRoomRequest | 201 RoomResponse; tạo một ghế Đỏ, chưa ready |
| GET | `/rooms/{roomId}` | — | RoomSyncResponse; room và lastChatSequence |
| GET | `/rooms/by-code/{roomCode}` | mã sáu ký tự | RoomSyncResponse; chỉ phòng waiting, không có chat; mã không tồn tại/không còn mở trả NOT_FOUND |
| POST | `/rooms/{roomId}/join` | VersionedCommand | RoomResponse; phòng waiting còn ghế |
| POST | `/rooms/{roomId}/ready` | SetReadyRequest | RoomResponse; chỉ sửa readiness của người gọi |
| POST | `/rooms/{roomId}/start` | VersionedCommand | StartMatchResponse; chủ phòng, hai người ready và online |
| POST | `/rooms/{roomId}/leave` | VersionedCommand | RoomResponse; chỉ waiting/finished; chủ rời thì đóng |
| POST | `/rooms/{roomId}/close` | VersionedCommand | RoomResponse; chủ phòng, chỉ waiting/finished |
| GET | `/matches/{matchId}` | — | MatchResponse; chỉ hai người chơi của trận |
| GET | `/matches/{matchId}/moves` | afterPly, limit | MovesResponse; chỉ người chơi của trận |
| POST | `/matches/{matchId}/moves` | SubmitMoveRequest | MoveAcceptedResponse; đúng người, lượt, version và luật |
| POST | `/matches/{matchId}/resign` | VersionedCommand | MatchResponse; người chơi xin thua khi playing/paused, sau kiểm deadline đã đến |
| POST | `/matches/{matchId}/draw-offers` | VersionedCommand | MatchResponse; playing PvP, không có đề nghị đang hiệu lực |
| POST | `/matches/{matchId}/draw-offers/accept` | DrawReplyRequest | MatchResponse; đối thủ đồng ý offerId trước expiresAt |
| POST | `/matches/{matchId}/draw-offers/decline` | DrawReplyRequest | MatchResponse; đối thủ từ chối đúng offerId |
| GET | `/rooms/{roomId}/messages` | afterSequence, limit | ChatPageResponse; chỉ thành viên hiện tại |
| POST | `/rooms/{roomId}/messages` | SendChatRequest | ChatResponse; thành viên, phòng chưa closed |
| GET | `/me/matches` | cursor, limit | HistoryResponse; trận finished/cancelled của chính người gọi |

GET snapshot room cho phép người đã đăng nhập xem phòng waiting để biết version trước khi join; với playing/finished/closed chỉ thành viên hiện tại hoặc người từng tham gia trận tương ứng được xem. SubscribeRoom và chat chỉ cho thành viên hiện tại. Sau leave/close phải loại kết nối khỏi group; match history vẫn tra theo MatchPlayers đã lưu.

Room/history dùng cursor opaque, thứ tự ổn định: room `(createdAt DESC,id DESC)`, history `(endedAt DESC,id DESC)`. Cursor buộc với bộ lọc và thứ tự; không dùng offset làm cursor. Trang cuối nextCursor=null. Moves dùng `afterPly` tăng dần, chat dùng `afterSequence` tăng dần; trả con trỏ của phần tử cuối, hoặc giữ con trỏ đầu vào nếu trang rỗng. Khi hasMore=true, tiếp tục tải cho đến false. Đọc lại cùng cursor không được làm mất bản ghi.

## 4. Phương thức và sự kiện SignalR

Hub `/hubs/game`; người dùng được xác thực trước khi vào hub. Adapter phải kiểm tra thành viên cho từng invocation, không coi biết roomId là có quyền. Các phương thức dưới đây nhận **một object** và trả Promise của response success hoặc ErrorResponse. Lỗi handshake/kết nối nằm ngoài envelope này; client phải xử lý qua cơ chế kết nối của SignalR.

| Client → server | Request schema | Response | Tác dụng |
|---|---|---|---|
| SubscribeRoom | RoomSubscriptionRequest | RoomSyncResponse | Kiểm quyền, vào group `room:{roomId}`, rồi đọc snapshot và chat cursor |
| UnsubscribeRoom | RoomSubscriptionRequest | AckResponse | Rời group của kết nối hiện tại; **không** rời ghế trong phòng |
| SubscribeMatch | MatchSubscriptionRequest | MatchResponse | Kiểm quyền, vào group `match:{matchId}`, rồi đọc snapshot |
| UnsubscribeMatch | MatchSubscriptionRequest | AckResponse | Rời group của kết nối hiện tại; không xin thua |
| SubmitMove | HubMoveRequest | MoveAcceptedResponse | Như REST submitMove, thêm matchId trong body |
| SendChat | HubChatRequest | ChatResponse | Như REST sendChat, thêm roomId trong body |

`SubscribeRoom` gắn kết nối đã xác thực với membership; trước trận có thể khôi phục presence trong grace. `SubscribeMatch` gắn đúng user/trận và chạy bước reconnect dưới khóa: chỉ phục hồi quyền chơi nếu còn hạn; kiểm deadline trước khi resume, trả snapshot sau xử lý. RequestId của subscribe dùng để đối chiếu log, không cache receipt làm bỏ qua đăng ký group trên kết nối mới. Lặp Subscribe trên cùng kết nối không trừ ngân sách/tăng version lần nữa. Reconnect quá hạn trả RECONNECT_EXPIRED; GET snapshot/lịch sử vẫn cho người tham gia xem. Unsubscribe chỉ rời group, không bỏ ghế, không reset ngân sách; binding presence tồn tại đến disconnect hoặc mất quyền xác thực. HTTP GET đơn thuần không được coi là đã nối lại SignalR.

Frontend chọn một transport cho mỗi lệnh. Khi retry hoặc đổi transport sau timeout, phải dùng lại requestId và nội dung ban đầu. Controllers và hub cùng gọi một command handler để không lưu hai nước đi.

| Server → client | Group | Payload |
|---|---|---|
| RoomUpdated | room:{roomId} | Envelope + `{reason,room}` đầy đủ |
| MatchUpdated | match:{matchId} | Envelope + `{reason,match,move}`; move=null nếu không phải nước đi |
| ChatMessageCreated | room:{roomId} | Envelope + ChatMessage; version bằng message.sequence |

Envelope sự kiện gồm `eventId,schemaVersion=1,type,aggregateType,aggregateId,version,serverTime,causationId,data`. `causationId` là requestId gây ra sự kiện; timer/restart do server gây ra đặt null. `aggregateId` trùng room.id hoặc match.id; chat dùng roomId. RoomUpdated.version bằng room.version; MatchUpdated.version bằng match.version.

Room version và match version độc lập; chat sequence là luồng thứ ba. Readiness/presence đổi room tăng room.version; presence làm pause/resume hoặc sửa recovery cũng tăng match.version và phát MatchUpdated. Đề nghị hòa tạo/hủy/hết hạn cũng đổi match.version nhưng không tăng ply. Chat không tăng hai version này. Snapshot room + chat cursor đọc nhất quán.

Một thay đổi trạng thái tạo đúng **một** event cho mỗi aggregate thay đổi. Nước đi chiếu bí phát một MatchUpdated có snapshot finished và move cuối; không phát thêm MatchEnded cùng version. Start/kết thúc có thể tạo hai event: room và match. Hai event này có thể đến theo thứ tự bất kỳ; currentMatchId giúp UI subscribe đúng trận.

## 5. Chống gửi lặp và cạnh tranh

Quy tắc áp dụng cho các POST room/match/chat, gồm cả lệnh gửi qua hub:

1. Xác thực user; chuẩn hóa payload và operation + target trước khi tạo fingerprint. Với hai transport, loại các trường route khỏi body sau khi đã đưa target vào fingerprint để cùng ý định có cùng fingerprint.
2. Tra receipt bằng `(userId,requestId)` **trước khi** kiểm tra expectedVersion. Nếu đúng fingerprint, trả nguyên response thành công đã lưu, kể cả serverTime cũ. Không tăng version, không thêm move, không tạo event mới. Outbox cũ có thể được giao lại.
3. Nếu requestId đã có nhưng target/operation/payload khác, trả 409 REQUEST_ID_REUSED. Không “ghi đè” lệnh cũ.
4. Với yêu cầu mới, khóa aggregate trong transaction; kiểm quyền hiện tại, expectedVersion, trạng thái, lượt, đồng hồ và luật. Kiểm tra version xảy ra trước các quyết định trạng thái/lượt để caller biết cần sync.
5. Lưu thay đổi, version tăng đúng 1 cho mỗi aggregate thực sự đổi, receipt và outbox cùng transaction. SetReady cùng giá trị hoặc Join lại của đúng thành viên là no-op: receipt thành công, không tăng version/event. Unique key receipt chặn hai kết nối đồng thời; đụng unique key thì rollback và đọc receipt đã thắng.
6. Lỗi không có tác dụng phụ không tạo receipt thành công. Sau VERSION_CONFLICT, client đồng bộ và chỉ gửi **ý định được người dùng xác nhận lại**, với requestId mới. Không tự sửa expectedVersion để gửi lại nước đi cũ trên bàn khác.

V1 giữ receipts bền, không xóa theo TTL. Nếu sau này muốn dọn dữ liệu, phải thiết kế thời hạn retry/tombstone trước khi xóa; không để yêu cầu cũ được thực hiện lại. Một requestId dùng lại ở lệnh khác cũng bị từ chối vì khóa theo user, không chỉ theo endpoint.

Receipt trả lại có thể chứa snapshot cũ. Client không được ghi đè trạng thái version mới hơn bằng dữ liệu cũ chỉ vì response vừa tới.

## 6. Phát hiện thiếu sự kiện và đồng bộ lại

Client quản lý lastVersion riêng từng `(aggregateType,aggregateId)` và lastChatSequence riêng từng room. Lần subscribe đầu cũng dùng quy trình này:

1. Đăng ký handler sự kiện trước khi gọi Subscribe. Trong khi đang sync, buffer sự kiện; khóa các nút làm thay đổi room/match.
2. Server thêm kết nối vào group **trước**, rồi đọc snapshot mới nhất ở nguồn dữ liệu chính và trả về. Snapshot room và chat cursor đọc nhất quán.
3. Áp dụng snapshot version V. Với room, lưu room.version độc lập với lastChatSequence; nếu local chat chưa tới cursor snapshot thì tải GET messages từ cursor cũ, không nhảy qua tin chưa đọc. Với match, dùng board/clock từ snapshot; tải thêm moves nếu UI cần lịch sử.
4. Xét sự kiện trong buffer theo version của từng luồng: version <= V bỏ qua; version == V+1 áp dụng; version > V+1 đánh dấu gap và sync lại. Khi áp dụng, kiểm tra envelope version/ID khớp payload. Trước khi mở khóa phải xử lý hết buffer hiện tại.
5. Chat được nối bằng sequence và messageId, không thay cả danh sách. Khi gap, đọc từng trang GET messages cho đủ; nếu sự kiện mới đến trong lúc đọc, tiếp tục giữ buffer và nối phần chưa có. Giới hạn buffer 1.000 sự kiện; vượt giới hạn thì bỏ buffer và sync lại từ snapshot/cursor bền.
6. Nếu schemaVersion không hỗ trợ, không áp dụng payload; báo yêu cầu cập nhật client. Không reconnect vô hạn để cố đọc schema lạ.

Kích hoạt sync khi reconnect, tab trở lại foreground, nhận VERSION_CONFLICT hoặc phát hiện gap. `withAutomaticReconnect()` chỉ là cơ chế nối lại transport; frontend còn phải xử lý retry start ban đầu và **SubscribeRoom/SubscribeMatch lại** sau reconnect. Handler buffer phải tồn tại suốt quá trình này.

Để phát hiện **sự kiện cuối cùng bị mất** mà không có sự kiện sau để lộ gap, khi đang ở phòng/trận client đối chiếu snapshot mỗi 15 giây và ngay trước lệnh tiếp theo. Nếu version hoặc chat cursor lệch, thực hiện quy trình sync. Mất kết nối thì khóa lệnh cho đến khi đồng bộ xong. Receipt sau timeout chỉ được retry cùng requestId/payload; không tạo request mới trước khi biết kết quả của request cũ.

Client không cần endpoint phát lại outbox trong v1: room/match hồi phục bằng snapshot; danh sách moves và chat hồi phục bằng cursor từ dữ liệu bền. Sự kiện có full snapshot giúp giảm phụ thuộc vào chuỗi delta, nhưng vẫn cần sync khi có gap để khôi phục lịch sử.

## 7. Vòng đời phòng và trận

| Trạng thái đầu | Lệnh / điều kiện | Trạng thái sau và tác dụng |
|---|---|---|
| Chưa có room | CreateRoom, user chưa ở phòng mở | waiting; chủ Đỏ, chưa ready; version=1 |
| waiting | Join, còn ghế, user chưa ở phòng mở | waiting; thêm Đen, readiness cả hai reset false |
| waiting | SetReady, thành viên | waiting; chỉ đổi isReady người gọi |
| waiting | Start, chủ, đủ hai người ready + online | playing; tạo match playing version=1, Đỏ đi trước |
| waiting | Guest leave / mất kết nối quá grace | waiting; bỏ ghế guest, readiness cả hai false |
| waiting | Host leave / close / mất kết nối quá grace | closed; không tạo match |
| playing | SubmitMove hợp lệ | playing hoặc finished; lưu đúng một ply |
| playing | Mất kết nối hợp lệ cuối của người chơi | room vẫn playing, match paused; freeze clock, hủy offer; mở deadline từ số dư nối lại |
| paused | Hai người xác thực lại đúng hạn | match playing; giữ bàn/lượt và ngân sách đã dùng, clock tiếp tục |
| playing/paused | Resign hợp lệ; hoặc playing hết giờ/hòa/chiếu bí/bí nước | match finished và room finished; hòa agreement có winner=null |
| paused/disconnect | Một bên hết ngân sách, bên kia online hợp lệ | finished/disconnect; nếu cả hai offline thì chờ tới deadline còn lại |
| paused/disconnect | Cả hai hết ngân sách | cancelled/bothDisconnected và room finished |
| playing/paused đã lưu | Server startup | paused/serverRestart; phục hồi checkpoint, giữ ngân sách/deadline cũ; không tự hủy ngay |
| paused/serverRestart | Hai người phục hồi / một người quá hạn | Cả hai đúng hạn thì playing; thất bại thì cancelled/serverRestart, winner=null |
| finished | Guest leave | finished, bỏ ghế guest; giữ MatchPlayers để đọc lịch sử |
| finished | Host leave hoặc Close | closed; giữ match và moves |
| closed | Join/Ready/Start/Chat | 409 ROOM_CLOSED |

Không cho leave/close khi room playing, kể cả match paused: muốn bỏ trận dùng Resign. V1 một room một match; chơi lại tạo room mới. Join/ready/start không hợp lệ ở finished. Waiting inactivity 15 phút, finished tối đa 5 phút; không dọn cửa sổ reconnect còn hạn hoặc trận playing/paused. Waiting sau restart áp dụng grace 60s, không xóa tùy ý. Chi tiết và các mốc biên: lifecycle.md.

Khi losing side không còn nước hợp lệ, v1 xử thua cả chiếu bí và bí nước; reason phân biệt checkmate/stalemate. Không xử “ăn Tướng” như một nước thường: engine phát hiện thế kết thúc theo luật, không xóa Tướng rồi tiếp tục trận. Các luật này phải được kiểm thử ở task engine.

## 8. Mã lỗi ổn định

`message` dùng tiếng Việt dễ đọc, có thể đổi nội dung; client phân nhánh theo `code`. Khi lỗi có VERSION_CONFLICT, currentVersion và resyncUrl bắt buộc. Các lỗi khác dùng null cho hai trường này nếu không áp dụng; fieldErrors là object rỗng nếu không phải lỗi field.

| HTTP | Code | Hành động client |
|---|---|---|
| 400 | VALIDATION_ERROR | Sửa dữ liệu theo fieldErrors; chặn tọa độ/kiểu sai |
| 401 | UNAUTHORIZED | Đăng nhập lại, không retry mù |
| 401 | INVALID_CREDENTIALS | Báo sai tài khoản/mật khẩu bằng cùng một thông báo |
| 403 | FORBIDDEN | Báo thiếu quyền, khóa thao tác |
| 404 | NOT_FOUND | Quay về lobby/lịch sử phù hợp |
| 409 | USERNAME_TAKEN | Chọn username khác |
| 409 | EMAIL_TAKEN | Chọn email khác |
| 409 | ROOM_FULL | Tải lại danh sách phòng |
| 409 | ROOM_CLOSED | Rời giao diện phòng đang đóng |
| 409 | ROOM_NOT_READY | Tải snapshot readiness/presence |
| 409 | INVALID_STATE | Tải snapshot, kiểm tra trạng thái cho phép |
| 409 | ALREADY_IN_ROOM | Quay về phòng hiện tại hoặc rời phòng đó |
| 409 | VERSION_CONFLICT | Khóa thao tác, sync, không tự gửi lại ý định cũ |
| 409 | REQUEST_ID_REUSED | Báo lỗi client; không tự đổi requestId để thực hiện lại |
| 409 | NOT_YOUR_TURN | Tải snapshot, chờ đúng lượt |
| 409 | MATCH_ENDED | Tải kết quả, khóa bàn cờ |
| 409 | MATCH_PAUSED | Đồng bộ snapshot và chờ phục hồi; không gửi Move/Draw |
| 409 | DRAW_OFFER_PENDING | Hiển thị đề nghị hiện có, không tạo thêm |
| 409 | DRAW_OFFER_NOT_FOUND | Đồng bộ; offer sai/đã hủy không còn hiệu lực |
| 409 | DRAW_OFFER_EXPIRED | Đồng bộ, không tự chấp nhận offer cũ |
| 409 | RECONNECT_EXPIRED | Chỉ xem trạng thái/kết quả, không phục hồi quyền chơi |
| 422 | ILLEGAL_MOVE | Giữ bàn chính thức, hiển thị lý do luật |
| 429 | RATE_LIMITED | Chờ theo Retry-After cho HTTP, hoặc backoff khi gọi hub |
| 500 | INTERNAL_ERROR | Báo lỗi có requestId; sau timeout/lỗi chưa rõ kết quả, tra bằng retry cùng requestId và sync |

Chỉ RATE_LIMITED và INTERNAL_ERROR có thể mang retryable=true. Giá trị này không cho phép đổi requestId của lệnh chưa biết kết quả. Không trả stack trace, connection string hay chi tiết mật khẩu.

Ví dụ VERSION_CONFLICT đã kiểm chứng nằm ở case `version-conflict` trong examples.json. Nội dung `resyncUrl` chỉ là đường dẫn tương đối của chính API; client không lấy URL tùy ý từ dữ liệu người dùng để gọi.

## 9. Xác thực và tương thích

- Đề xuất access token 30 phút; v1 hết hạn thì đăng nhập lại, chưa có refresh token. Kiểm hạn token và thu hồi jti cả REST và hub; cấu hình đóng kết nối khi auth hết hạn, không cho socket cũ tiếp tục gửi lệnh.
- Dùng HTTPS; CORS chỉ các origin frontend đã cấu hình. Không đưa token vào log URL khi SignalR dùng query string cho transport trình duyệt. Giữ token trong bộ nhớ client cho bản đồ án; reload yêu cầu đăng nhập lại.
- Chat 1–500 ký tự sau trim; service kiểm whitespace, rate limit theo user và room. Username/email trim + lowercase trước unique; không áp dụng biến đổi riêng của nhà cung cấp email. Không trả passwordHash/email trong UserPublic.
- Tăng hợp đồng minor cho bổ sung được client cũ chấp nhận; vì schema đang strict additionalProperties=false, thêm field bắt buộc/field trên wire hoặc enum mới cần phối hợp cập nhật client, hoặc tạo `/api/v2` và schemaVersion mới. Không tự xem mọi thay đổi thêm field là tương thích.
- Chưa có máy chủ chạy để xác nhận performance, phân quyền thực tế, race hoặc reconnect; các ca tích hợp bắt buộc được ghi trong handover.
