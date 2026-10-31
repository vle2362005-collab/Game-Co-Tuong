# Sprint 3 — Bản tích hợp cốt lõi

Giữ nguyên GCT-23. Tích hợp mã có sẵn của GCT-27/28/29/7/30/32/33/34/35; hoàn thiện các thẻ còn lại trong Sprint 3. Nhánh bàn giao: `feature/GCT-27-sprint3-completion`, base `develop`.

## Chạy nhanh

Cần SDK theo `global.json` và Node theo `.nvmrc`. Từ thư mục gốc:

```sh
dotnet restore GameCoTuong.sln --locked-mode
dotnet run --project src/backend/GameCoTuong.Api --no-launch-profile -- --Database:Provider=Sqlite --ConnectionStrings:GameDatabase="Data Source=game-local.db" --urls=http://localhost:5000
```

Terminal thứ hai:

```sh
cd src/frontend
npm ci
npm run dev
```

Mở `http://localhost:5173` bằng hai cửa sổ/phiên riêng, đăng ký hai tài khoản thử. Tạo phòng → nhập mã → hai bên Sẵn sàng → chủ phòng Bắt đầu. Token chỉ giữ trong bộ nhớ; F5 cần đăng nhập lại. Sảnh có nút Trở lại cho thành viên đang ở phòng. Không dùng tài khoản seed với password hash placeholder để đăng nhập.

SQLite dành cho demo và kiểm thử độc lập. Bỏ `Database:Provider=Sqlite` để dùng SQL Server theo cấu hình hiện có. Kiểm thử mới không truy cập CSDL SQL Server thật của nhóm. Bản hiện tại chạy một tiến trình API; khi triển khai nhiều tiến trình cần khóa/phân phối presence thay cho trạng thái trong bộ nhớ.

## Video

Trong phòng chờ hoặc ván cờ: Gọi video → đối thủ Nhận/Từ chối. Camera/micro chỉ được xin quyền sau thao tác Gọi hoặc Nhận. Có video hai bên, bật/tắt mic/camera, kết thúc, tự dọn stream khi rời màn hình, mất kết nối hoặc đăng xuất. Offer/Answer/ICE mang call ID và đúng người nhận; ICE đợi remote description, tín hiệu phiên cũ bị từ chối.

Camera cần HTTPS hoặc localhost. `VITE_ICE_SERVERS` nhận mảng `RTCIceServer` dạng JSON; STUN mặc định chỉ hỗ trợ tìm đường kết nối. Mạng có NAT/firewall hạn chế cần TURN và HTTPS, thuộc công việc GCT-67 của Sprint 4. Không tuyên bố đã kiểm thử mọi mạng Internet hoặc thiết bị vật lý.

## Mã nguồn và đối chiếu

| Thẻ | File chính |
|---|---|
| GCT-27/28/29/30/7/32/33/34/35 | `src/backend/GameCoTuong.Api/Endpoints/AuthEndpoints.cs`, `Hubs/GameHub.cs`, `src/frontend/src/composables/useAuth.ts`, `api/signalr.ts` |
| GCT-43/106/107 | `Engine/Model/GameState.cs`, `tests/backend/GameCoTuong.Foundation.Tests/XiangqiRulesTests.cs`, `GameFlowTests.cs`, `TestDatabase.cs` |
| GCT-50/51/52/53/54/55/56/57/58/59/60 | `src/backend/GameCoTuong.Engine/Rules/XiangqiRules.cs` |
| GCT-62 | [Đặc tả lặp thế](repetition-scope.md) |
| GCT-36/37/39/40/44/46/73/74/86/8 | `src/backend/GameCoTuong.Infrastructure/Game/GameService.cs`, API `RoomEndpoints.cs`, `MatchEndpoints.cs` |
| GCT-104 | `AuthorizationAndRoomAccessTests.cs`, `GameFlowTests.cs`, browser check |
| GCT-41/38/45/49/105/81 | `src/frontend/src/components/XiangqiBoard.vue`, `boardLayout.ts`, `LobbyView.vue`, `RoomView.vue`, `GameView.vue` |
| GCT-63/64/111 | `RoomChat.vue`, `GameHub.SendRoomMessage`, `GameService.Chat` |
| GCT-68/69/70 | `src/backend/GameCoTuong.Engine/AI/Minimax.cs`, `XiangqiRules.Generate` |
| GCT-65/66 | `GameHub.Signal`, `VideoCalls.cs`, `useVideoCall.ts`, `VideoCall.vue`, `videoCall.test.ts` |

## Kiểm tra

```sh
dotnet test GameCoTuong.sln
npm --prefix src/frontend run lint
npm --prefix src/frontend test
npm --prefix src/frontend run build
```

`scripts/sprint3-browser-check.cjs` khởi động API/Vite riêng, dùng hai tab có hai phiên đăng nhập riêng, tài khoản và SQLite tạm; kiểm tra phòng, SVG, nước sai/đúng, chat, video giả lập thiết bị, mic/camera, giao diện 390px và đầu hàng. Cần Playwright/Chromium để chạy. Có thể đặt `PLAYWRIGHT_MODULE`, `CHROMIUM_PATH`, `DOTNET_PATH`; kết quả và ảnh xuất vào `artifacts/sprint3` (không commit dữ liệu tài khoản hoặc token).

Các yêu cầu lặp được kiểm tra bằng request ID; trận/nước/lịch sử/kết quả lưu trong giao dịch trước khi gửi sự kiện. Đã có ca mô phỏng lỗi ghi CSDL để xác nhận rollback. Nếu truyền sự kiện lỗi sau commit, client phải đọc lại snapshot; chưa có worker outbox bảo đảm giao sự kiện qua mọi sự cố hệ thống.

Luồng bàn giao: kiểm tra → commit mang mã GCT → push nhánh → PR vào develop → CI/review → merge → gắn link minh chứng và Done. Không đánh dấu Done chỉ vì code đã có tại workspace.

Kết quả lần chạy đã nghiệm thu: [144 backend, 43 frontend và kiểm thử browser](sprint3-verification.md).
