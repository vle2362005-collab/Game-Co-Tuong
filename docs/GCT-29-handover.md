# Báo Cáo Bàn Giao Kỹ Thuật: GCT-29 - Kiểm Tra Quyền Truy Cập API Và Phòng

> **Task ID**: `GCT-29`  
> **Nhánh**: `GCT-29-authorize`  
> **Tiêu chuẩn nghiệp vụ**: `BR-12`, `FR-01..FR-03`, `FR-05`, `FR-13`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Thiết lập cơ chế kiểm tra quyền truy cập chặt chẽ cho toàn bộ các thao tác trên API và SignalR; chỉ cho phép người dùng thực hiện các thao tác đúng quyền của mình, chống giả danh và bảo vệ an toàn thông tin theo yêu cầu nghiệp vụ.

### Yêu cầu đã thực hiện:
1. **Kiểm tra JWT trên API và SignalR**:
   - Triển khai `JwtAuthenticationMiddleware` bắt buộc xác thực token cho các endpoint bảo vệ và các kết nối SignalR `/hubs/game`.
   - Hỗ trợ trích xuất JWT qua `Authorization: Bearer <token>` và qua query parameter `access_token` (cho WebSocket / SignalR client).
   - Kiểm tra chữ ký, thời hạn hiệu lực, và tính toàn vẹn của token; từ chối token thiếu, sai định dạng, bị can thiệp hoặc hết hạn bằng phản hồi chuẩn `401 UNAUTHORIZED`.
2. **Lấy danh tính từ phiên đã xác thực (Anti-Spoofing)**:
   - Xây dựng `ICurrentUserService` và `CurrentUserService` nhằm trích xuất danh tính (`UserId`, `Username`, `DisplayName`) trực tiếp từ `ClaimsPrincipal` của token JWT đã xác thực.
   - Tuyệt đối không chấp nhận `userId` do client gửi lên trong payload hoặc query để xác định người thực hiện hành động.
   - Ngăn chặn hoàn toàn việc người ngoài phòng gửi kèm mã người dùng khác nhằm giả danh (`SpoofedUserId`).
3. **Kiểm tra thành viên & quyền hạn phòng/trận đấu (`IRoomAuthorizationService`)**:
   - **Bắt đầu trận (`POST /api/v1/rooms/{roomId}/start` & SignalR `StartRoom`)**: Chỉ chủ phòng (`HostUserId`) đang là thành viên hoạt động mới được phép bắt đầu. Người ngoài hoặc khách (guest) bị chặn với mã `403 FORBIDDEN` (`NOT_ROOM_HOST`).
   - **Đóng phòng (`POST /api/v1/rooms/{roomId}/close` & SignalR `CloseRoom`)**: Chỉ chủ phòng mới có quyền đóng phòng; khách và người ngoài bị từ chối bằng `403 FORBIDDEN`.
   - **Đi quân (`POST /api/v1/matches/{matchId}/moves` & SignalR `SubmitMove`)**: Chỉ người chơi tham gia ván đấu (`MatchPlayer`) và đúng lượt đi (`SideToMove == player.Side`) mới được thực hiện nước đi; chặn người chơi sai lượt (`NOT_YOUR_TURN`), người ngoài phòng (`NOT_MATCH_PLAYER`), hoặc ván đấu chưa bắt đầu (`MATCH_NOT_PLAYING`).
   - **Gọi video (`POST /api/v1/rooms/{roomId}/video/signal` & SignalR `SendVideoSignal`)**: Chỉ thành viên hoạt động trong phòng (`LeftAt == null`) mới được gửi tín hiệu WebRTC; chặn người ngoài và thành viên đã rời phòng (`NOT_ROOM_MEMBER`).
4. **Bảo mật và chống ghi nhật ký token (BR-12)**:
   - Xây dựng tiện ích `LogSanitizer` làm sạch URL, headers và log messages:
     - Ẩn query param `access_token=[REDACTED]`.
     - Ẩn header `Authorization: Bearer [REDACTED]`.
     - Ẩn mọi chuỗi token JWT (`eyJ...`) trong nội dung log.
   - Đảm bảo token và mật khẩu không bao giờ xuất hiện trong nhật ký hệ thống.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/backend/GameCoTuong.Application/Common/Interfaces/ICurrentUserService.cs` | Interface lấy danh tính người dùng từ phiên đã xác thực |
| 2 | `src/backend/GameCoTuong.Application/Auth/Models/AuthorizationResult.cs` | Model kết quả ủy quyền (thành công hoặc mã/thông điệp lỗi) |
| 3 | `src/backend/GameCoTuong.Application/Auth/Services/IRoomAuthorizationService.cs` | Interface kiểm tra quyền chủ phòng, thành viên, lượt đi và video call |
| 4 | `src/backend/GameCoTuong.Application/Auth/Models/RoomActionRequests.cs` | DTO request cho start, close, move và video signal có tính kháng giả danh |
| 5 | `src/backend/GameCoTuong.Infrastructure/Auth/CurrentUserService.cs` | Triển khai trích xuất danh tính từ `IHttpContextAccessor` |
| 6 | `src/backend/GameCoTuong.Infrastructure/Auth/RoomAuthorizationService.cs` | Triển khai xác thực quyền hạn trên `GameDbContext` (`Rooms`, `Memberships`, `Matches`, `MatchPlayers`) |
| 7 | `src/backend/GameCoTuong.Infrastructure/DependencyInjection.cs` | Đăng ký `ICurrentUserService`, `IRoomAuthorizationService`, `IHttpContextAccessor` |
| 8 | `src/backend/GameCoTuong.Api/Middleware/LogSanitizer.cs` | Tiện ích làm sạch log, loại bỏ token và password theo `BR-12` |
| 9 | `src/backend/GameCoTuong.Api/Middleware/JwtAuthenticationMiddleware.cs` | Middleware xác thực JWT cho API và SignalR negotiation |
| 10 | `src/backend/GameCoTuong.Api/Hubs/GameHub.cs` | SignalR Hub quản lý phiên kết nối và ủy quyền phòng/ván đấu |
| 11 | `src/backend/GameCoTuong.Api/Endpoints/RoomEndpoints.cs` | Map các endpoint `/start`, `/close`, `/video/signal` được bảo vệ |
| 12 | `src/backend/GameCoTuong.Api/Endpoints/MatchEndpoints.cs` | Map endpoint `/moves` được bảo vệ và kiểm tra lượt đi |
| 13 | `src/backend/GameCoTuong.Api/Program.cs` | Cấu hình SignalR, JwtAuthenticationMiddleware, Hubs và Endpoints |
| 14 | `tests/backend/GameCoTuong.Foundation.Tests/AuthorizationAndRoomAccessTests.cs` | Suite 16 kịch bản kiểm thử toàn diện cho API, SignalR, anti-spoofing và log sanitization |

---

## 3. Kết Quả Kiểm Thử

Toàn bộ 109 bài kiểm thử backend và 4 bài kiểm thử frontend đều đã vượt qua 100%:

```
Passed!  - Failed: 0, Passed: 109, Skipped: 0, Total: 109, Duration: 5 s - GameCoTuong.Foundation.Tests.dll (Release net10.0)

✓ tests/health.test.ts (4 tests) 24ms
Test Files  1 passed (1)
Tests       4 passed (4)
Vite build: 13 modules transformed, 0 errors.
ESLint: 0 errors, 0 warnings.
```

### Các nhóm kiểm thử chính của GCT-29:
- **Kiểm thử thiếu token**: Endpoint `/start`, `/close`, `/moves`, `/video/signal` thiếu token trả về `401 UNAUTHORIZED`.
- **Kiểm thử token sai/hết hạn**: Token bị can thiệp chữ ký hoặc token hết hạn bị chặn với `401 UNAUTHORIZED`.
- **Kiểm thử SignalR Hub**: Kết nối không có token hoặc token sai bị chặn `401 UNAUTHORIZED`; kết nối với token hợp lệ qua header hoặc query `access_token` được chấp nhận.
- **Kiểm thử quyền chủ phòng**: Chủ phòng được bắt đầu/đóng phòng; khách và người ngoài bị từ chối với `403 FORBIDDEN`.
- **Kiểm thử chống giả danh (Anti-Spoofing)**: Kẻ tấn công gửi kèm `userId` của chủ phòng hoặc kỳ thủ trong body nhưng token là của kẻ tấn công thì hệ thống vẫn nhận diện đúng kẻ tấn công và từ chối hành động.
- **Kiểm thử lượt đi**: Đúng bên (`SideToMove == player.Side`) được đi quân; đi sai lượt bị chặn `NOT_YOUR_TURN`; người ngoài bị chặn `NOT_MATCH_PLAYER`.
- **Kiểm thử gọi video**: Thành viên hoạt động được gửi tín hiệu; người ngoài hoặc thành viên đã rời phòng bị chặn `NOT_ROOM_MEMBER`.
- **Kiểm thử SignalR Hub Authorization**: Các phương thức `StartRoom`, `CloseRoom`, `SubmitMove`, `SendVideoSignal` ném `HubException` với mã `FORBIDDEN` khi người gọi không đủ quyền.
- **Kiểm thử che giấu token (Log Sanitizer)**: URL, header và thông điệp log đều tự động thay thế token JWT bằng `[REDACTED]`.
