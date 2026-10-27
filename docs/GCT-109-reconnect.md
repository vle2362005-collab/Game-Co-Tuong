# GCT-109 — Kết nối lại và tiếp tục trận đấu

## Luồng

- SignalR hub: `GET /hubs/match`; client gọi `ReconnectMatch(matchId)` sau khi kết nối.
- Hub chỉ nhận người dùng đã xác thực. ID người dùng lấy từ claim `NameIdentifier` hoặc `sub`, sau đó phải khớp với một `MatchPlayer` của trận được yêu cầu. Không truyền `userId` từ trình duyệt.
- Nếu thành công, phản hồi `MatchReconnectState` chứa màu của người dùng, trạng thái lượt đi, số nước, bàn cờ đã lưu và thời gian còn lại của từng người.
- Client lắng nghe `MatchPaused`, `PlayerReconnected` và `MatchEnded`. `MatchPaused` bao gồm snapshot hiện tại và đếm ngược phía máy chủ.
- Vue được cung cấp dưới dạng `MatchReconnectPanel`; ứng dụng gọi truyền `matchId` và `accessTokenFactory` từ cơ chế đăng nhập của ứng dụng.

## Thời hạn và kết quả

Mỗi người bắt đầu với 60.000 ms. Khi người chơi rớt mạng, máy chủ lưu `DisconnectedAt` và `DeadlineAt`. Khi quay lại, thời gian đã dùng được trừ khỏi `RecoveryRemainingMs`; lần rớt tiếp theo chỉ được dùng phần còn lại, không đặt lại 60 giây. Thời hạn lấy từ đồng hồ máy chủ và được lưu trong SQL, không dựa vào đồng hồ trình duyệt.

Các cập nhật trạng thái dùng transaction `Serializable` để tránh việc yêu cầu nối lại và bộ xử lý hết hạn ghi đè kết quả của nhau. Khi cả hai deadline hết hạn, trận được đặt `cancelled`, `Winner` để trống và `ResultReason = both_disconnected`. Nếu một người hết hạn trong khi đối thủ đã kết nối, người còn lại thắng do `disconnect_timeout`. Khi đối thủ vẫn đang trong thời gian nối lại, máy chủ chờ kết quả của đối thủ thay vì tự gán thắng.

Các trường `CurrentBoardJson`, `Version`, `MoveNumber`, `SideToMove`, deadline và thời gian còn lại đã tồn tại trong migration ban đầu; không cần migration mới cho task này. Khi tạm dừng, đồng hồ lượt hiện tại được đóng băng và tiếp tục khi cả hai người trở lại.

## Tích hợp và giới hạn hiện tại

- Hub/worker chỉ được đăng ký khi có cấu hình `ConnectionStrings:GameDatabase`; cần chạy migration hiện có và dùng SQL Server.
- Project hiện chưa cấu hình JWT/session authentication hoặc luồng tạo/chơi trận. Host tích hợp phải đăng ký authentication scheme trước khi gọi `UseAuthentication`, cung cấp claim định danh GUID như trên, và tạo `Match`/`MatchPlayer` với `CurrentBoardJson` hợp lệ. Không gửi token qua query string do ứng dụng tự tạo; dùng `accessTokenFactory`.
- `MatchReconnectPanel` là component để ứng dụng hiện có mount, chưa gắn vào `App.vue` vì scaffold chưa có màn hình đăng nhập/chọn trận hay nguồn token. Cung cấp token qua `accessTokenFactory`; SignalR có thể truyền token bằng tham số `access_token` khi dùng WebSocket/SSE, vì vậy chỉ cho phép đọc tham số này tại đường dẫn hub, dùng HTTPS và không ghi query string vào log.
- `MatchConnectionRegistry` hiện theo dõi nhiều tab trên một tiến trình API. Nếu triển khai nhiều instance, cần thay presence này bằng registry phân tán/SignalR backplane trước khi scale-out.
- Thành phần này khôi phục snapshot, màu quân và lượt đi; không triển khai luật đi quân hoặc gửi nước đi mới.
