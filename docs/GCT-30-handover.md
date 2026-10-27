# Báo Cáo Bàn Giao Kỹ Thuật: GCT-30 - Lấy Hồ Sơ Và Xử Lý Đăng Xuất

> **Task ID**: `GCT-30`  
> **Nhánh**: `GCT-30-session-policy`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `BR-03`, `BR-11`, `BR-12`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Cung cấp API lấy hồ sơ cá nhân bảo mật dựa trên danh tính phiên đăng nhập, thực hiện chính sách thu hồi phiên (JWT revocation policy) và dọn dẹp trạng thái phòng/trận đấu đúng quy định khi người dùng đăng xuất.

### Yêu cầu đã thực hiện:
1. **Lấy hồ sơ theo danh tính đăng nhập (`GET /api/v1/users/me`)**:
   - Trích xuất định danh người dùng trực tiếp từ claims (`sub`) của JWT đã xác thực.
   - Tuyệt đối không cho phép đổi hoặc giả mạo hồ sơ bằng query parameter hoặc payload client (ví dụ `?userId=...` bị bỏ qua).
   - Bảo mật thông tin: không trả về `email`, `password`, hay `passwordHash` theo tiêu chuẩn `FR-01`.
2. **Thu hồi token và chính sách phiên khi đăng xuất (`POST /api/v1/auth/logout`)**:
   - Ghi nhận `jti` của token vào bảng `RevokedTokens` trong cơ sở dữ liệu (`RevokedAt`, `ExpiresAt`).
   - Kiểm tra `RevokedTokens` trên `UserEndpoints` và `JwtAuthenticationMiddleware`: mọi request hoặc kết nối SignalR sử dụng lại token đã thu hồi đều bị chặn ngay lập tức với mã `401 UNAUTHORIZED`.
   - Phản hồi kèm thông điệp chính sách phiên: *"JWT đã được thu hồi trong cơ sở dữ liệu. Trường hợp không có bảng thu hồi (stateless JWT), token cũ vẫn có hiệu lực cho đến thời điểm hết hạn (exp)."*
3. **Dọn dẹp phòng và trận đấu đúng trạng thái khi đăng xuất**:
   - Đánh dấu thành viên đã rời phòng: `LeftAt = DateTimeOffset.UtcNow`, `IsConnected = false`.
   - Nếu đang trong trận cờ `playing`, xử lý đầu hàng (`ResultReason = "resignation"`, đối thủ thắng, trận đấu chuyển sang `finished`).
   - Nếu là chủ phòng ở trạng thái `waiting`, chuyển trạng thái phòng sang `closed` (`ClosedAt != null`).
4. **Xác nhận đầu hàng phía giao diện**:
   - Hộp thoại `SurrenderModal` bắt buộc xác nhận đầu hàng trước khi đăng xuất trong trận.
   - Hủy đầu hàng giữ nguyên ván cờ, không đăng xuất, không đổi trang.
   - Xác nhận đầu hàng kích hoạt dọn dẹp phiên và điều hướng về trang đăng nhập.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/backend/GameCoTuong.Application/Auth/Models/LogoutResponseDto.cs` | DTO kết quả đăng xuất kèm chính sách thu hồi phiên |
| 2 | `src/backend/GameCoTuong.Api/Endpoints/AuthEndpoints.cs` | Cập nhật `HandleLogoutAsync` ghi nhận `RevokedTokens`, dọn phòng/trận |
| 3 | `src/backend/GameCoTuong.Api/Endpoints/UserEndpoints.cs` | Kiểm tra token thu hồi trong `HandleGetMeAsync` và chống giả danh qua query |
| 4 | `src/backend/GameCoTuong.Api/Middleware/JwtAuthenticationMiddleware.cs` | Chặn kết nối API/SignalR với token đã bị thu hồi |
| 5 | `src/frontend/src/components/SurrenderModal.vue` | Bổ sung ghi chú chính sách JWT revocation trong hộp thoại đầu hàng |
| 6 | `tests/backend/GameCoTuong.Foundation.Tests/AuthorizationAndRoomAccessTests.cs` | Bổ sung 4 kịch bản kiểm thử tích hợp chuyên sâu cho GCT-30 |
| 7 | `docs/GCT-30-handover.md` | Báo cáo bàn giao kỹ thuật |

---

## 3. Kết Quả Kiểm Thử

- **Backend Tests**: **113 / 113 Passed** (thêm 4 bài kiểm thử mới cho GCT-30 bao phủ chống đổi hồ sơ qua `userId`, thu hồi token, đóng phòng waiting cho host, và đầu hàng khi đang chơi).
- **Frontend Tests**: **11 / 11 Passed** (toàn bộ kiểm thử vitest pass).
- **Linter & Build**: ESLint 0 errors, 0 warnings; Vite build 0 errors; .NET build Release 0 errors.
