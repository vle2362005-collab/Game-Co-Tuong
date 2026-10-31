# Báo Cáo Bàn Giao Kỹ Thuật: GCT-35 - Thiết Lập Kết Nối SignalR Có Xác Thực

> **Task ID**: `GCT-35`  
> **Nhánh**: `GCT-35-signalr-hub`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `FR-02`, `FR-03`, `FR-05`, `FR-13`, `BR-12`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Thiết lập kết nối thời gian thực SignalR Hub có xác thực JWT, phân tách độc lập nhóm Sảnh (`lobby`) và nhóm Phòng (`room-{id}`), kiểm tra quyền thành viên trước khi gia nhập nhóm và bảo đảm tính cô lập sự kiện (Room Event Isolation - sự kiện phòng này tuyệt đối không truyền sang phòng khác).

### Yêu cầu đã thực hiện:

1. **Kết nối Hub có xác thực JWT**:
   - `GameHub` tại endpoint `/hubs/game` yêu cầu xác thực JWT qua header `Authorization: Bearer <token>` hoặc query parameter `access_token` (cho WebSocket/SSE).
   - `JwtAuthenticationMiddleware` và `GameHub.OnConnectedAsync` chặn ngay lập tức các kết nối thiếu token, token sai hoặc token đã bị thu hồi với mã phản hồi `401 UNAUTHORIZED`.
   - Hỗ trợ kết nối an toàn từ các địa chỉ được cho phép (CORS `AllowedOrigins` như `http://localhost:5173`).

2. **Phân tách nhóm Sảnh (`lobby`) và nhóm Phòng (`room-{id}`)**:
   - **Nhóm Sảnh**: Quản lý qua `JoinLobby()` và `LeaveLobby()`, định tuyến các thông báo trạng thái phòng tổng thể (`RoomStateChanged`).
   - **Nhóm Phòng**: Quản lý qua `JoinRoom(Guid roomId)` và `LeaveRoom(Guid roomId)`, định danh nhóm theo tiền tố chuẩn hóa `room-{roomId}`.

3. **Kiểm tra quyền thành viên trước khi gia nhập nhóm & Mã lỗi chuẩn hóa**:
   - Trước khi thêm connection vào nhóm phòng (`Groups.AddToGroupAsync`), Hub gọi `IRoomAuthorizationService.AuthorizeRoomMemberAsync(roomId, userId)` để kiểm tra tư cách thành viên.
   - Nếu là người ngoài phòng (outsider) hoặc thành viên đã rời phòng, Hub chặn đứng và ném ngoại lệ `HubException("FORBIDDEN: ...")`.
   - Chuẩn hóa mã lỗi SignalR: `UNAUTHORIZED`, `FORBIDDEN`, `VALIDATION_ERROR`.

4. **Cô lập sự kiện giữa các phòng (Room Isolation)**:
   - Các sự kiện ván cờ, tin nhắn chat (`RoomMessageReceived`), cập nhật nước đi (`MoveAccepted`), tín hiệu video (`VideoSignalReceived`), và sự kiện bắt đầu/đóng phòng (`RoomStarted`, `RoomClosed`) được phát qua `Clients.Group(GetRoomGroupName(roomId))`.
   - Sự kiện của phòng này được cô lập hoàn toàn, không thể rò rỉ sang các phòng khác hay nhóm sảnh.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/backend/GameCoTuong.Api/Hubs/GameHub.cs` | Bổ sung quản lý nhóm sảnh/phòng, kiểm tra quyền thành viên và cô lập sự kiện phòng |
| 2 | `tests/backend/GameCoTuong.Foundation.Tests/AuthorizationAndRoomAccessTests.cs` | Bổ sung 5 kịch bản kiểm thử SignalR Hub: CORS, chặn thiếu token, JoinLobby, JoinRoom (member vs outsider), và cô lập tin nhắn phòng |
| 3 | `docs/GCT-35-handover.md` | Tài liệu bàn giao kỹ thuật |

---

## 3. Kết Quả Kiểm Thử

- **Backend Tests (.NET)**: **118 / 118 Passed** (thêm 5 bài kiểm thử mới cho SignalR Hub, nâng tổng số test backend lên 118 pass 100%).
- **Frontend Tests (Vitest)**: **39 / 39 Passed** (toàn bộ 6 test suites pass 100%).
- **Frontend Linter (ESLint)**: **0 errors, 0 warnings**.
- **Frontend Build (Vite + Vue-TSC)**: **55 modules transformed**, build production thành công 100%.
