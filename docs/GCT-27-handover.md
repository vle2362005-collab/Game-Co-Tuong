# GCT-27 — Bàn giao Đăng ký tài khoản và kiểm tra thông tin

27/10/2026 • [Jira GCT-27](https://vle2362005.atlassian.net/browse/GCT-27) • Nhánh `GCT-27-register`.

## 1. Đối chiếu tiêu chí hoàn thành

| Tiêu chí của thẻ | Đầu ra thực tế |
|---|---|
| **Kiểm tra tên đăng nhập, email và mật khẩu** | `RegisterRequestValidator` kiểm tra nghiêm ngặt: `username` 3-30 ký tự định dạng `^[a-zA-Z0-9_]+$`, `displayName` 1-50 ký tự, `password` 8-128 ký tự, `email` chuẩn định dạng tối đa 254 ký tự, `requestId` hợp lệ. |
| **Chuẩn hóa tên, email và chặn trùng kể cả khác hoa thường** | `RegistrationService` tự động chuẩn hóa `NormalizedUsername` và `NormalizedEmail` bằng `Trim().ToLowerInvariant()`. Kiểm tra trùng lặp trước khi ghi và kích hoạt lỗi `409 USERNAME_TAKEN`, `409 EMAIL_TAKEN`. |
| **Băm mật khẩu an toàn trước khi lưu** | `PasswordHasher` áp dụng tiêu chuẩn mã hóa PBKDF2 với SHA-256, 100.000 vòng lặp (iterations) và 16-byte cryptographic salt ngẫu nhiên. So sánh mật khẩu bằng `CryptographicOperations.FixedTimeEquals` chống tấn công timing attack. |
| **Tạo đúng một tài khoản kể cả gửi đồng thời (AC-01)** | Kết hợp tầng logic và ràng buộc Unique Index trong CSDL SQL Server (`UQ_Users_NormalizedUsername`, `UQ_Users_NormalizedEmail`). Bắt ngoại lệ tranh chấp `DbUpdateException` để đảm bảo khi gửi đồng thời nhiều request cùng lúc, đúng 1 tài khoản được tạo và các request cạnh tranh nhận mã lỗi 409. |
| **Dữ liệu sai có thông báo; bảo mật phản hồi** | Lỗi dữ liệu trả về `400 VALIDATION_ERROR` kèm danh sách `fieldErrors` chi tiết. Phản hồi thành công `201 Created` trả về `UserPublic` gồm (`id`, `username`, `displayName`); **tuyệt đối không chứa `password`, `passwordHash` hay `email`**. |

## 2. Kết quả kiểm tra xác thực

Kiểm tra toàn bộ quy trình bằng `scripts/verify.ps1`:

| Hạng mục kiểm tra | Kết quả |
|---|---|
| NuGet restore theo lockfile (`--locked-mode`) | Pass, phục hồi sạch các dependency theo lockfile |
| Backend Release build | Pass, 0 warning, 0 error |
| Backend xUnit tests | 36 pass, 0 fail, 0 skip (bao gồm 22 test cases mới chuyên biệt cho hashing, validation, normalization, unique, concurrency và HTTP endpoints) |
| Frontend npm ci + vue-tsc + Vite build | Pass, tạo production bundle sạch (0 lỗ hổng) |
| Frontend Vitest | 4 pass, 0 fail |

## 3. Các thành phần mã nguồn chính

- `src/backend/GameCoTuong.Application/Auth/Models/RegisterRequest.cs`: Record request đăng ký khớp JSON Schema GCT-20.
- `src/backend/GameCoTuong.Application/Auth/Models/UserPublicDto.cs`: Record thông tin công khai của người dùng (không lộ thông tin nhạy cảm).
- `src/backend/GameCoTuong.Application/Auth/Models/RegistrationResult.cs`: Kết quả xử lý nghiệp vụ đăng ký tài khoản.
- `src/backend/GameCoTuong.Application/Auth/Validation/RegisterRequestValidator.cs`: Bộ kiểm tra tính hợp lệ dữ liệu đăng ký thuần C#.
- `src/backend/GameCoTuong.Application/Common/Interfaces/IPasswordHasher.cs`: Interface trừu tượng hóa dịch vụ băm mật khẩu.
- `src/backend/GameCoTuong.Application/Auth/Services/IRegistrationService.cs`: Interface nghiệp vụ đăng ký tài khoản.
- `src/backend/GameCoTuong.Infrastructure/Auth/PasswordHasher.cs`: Triển khai băm mật khẩu an toàn PBKDF2-SHA256 100.000 iterations.
- `src/backend/GameCoTuong.Infrastructure/Auth/RegistrationService.cs`: Triển khai đăng ký tài khoản, chuẩn hóa và xử lý concurrency.
- `src/backend/GameCoTuong.Api/Endpoints/AuthEndpoints.cs`: Định tuyến HTTP Minimal API cho `POST /api/v1/auth/register`.
- `tests/backend/GameCoTuong.Foundation.Tests/RegistrationTests.cs`: Bộ kiểm thử toàn diện 22 test cases cho toàn bộ quy trình.
