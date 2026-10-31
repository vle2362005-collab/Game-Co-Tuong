# Báo Cáo Bàn Giao Kỹ Thuật: GCT-28 - Kiểm Tra Đăng Nhập Và Cấp Phiên Truy Cập

> **Task ID**: `GCT-28`  
> **Nhánh**: `feature/GCT-28-login-jwt`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `AC-01`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Xác thực danh tính người dùng trước khi tham gia vào hệ thống Game Cờ Tướng, bảo vệ an toàn thông tin đăng nhập và cấp phiên làm việc bằng JSON Web Token (JWT) có chữ ký mật mã, thời hạn rõ ràng.

### Yêu cầu đã thực hiện:
1. **Kiểm tra thông tin đăng nhập**:
   - Kiểm tra định dạng đầu vào (`requestId`, `username`, `password`) bằng bộ kiểm tra thuần C# không phụ thuộc regex hay reflection động nhằm tuân thủ bảo mật Smart App Control / WDAC.
   - Chuẩn hóa tên đăng nhập không phân biệt hoa thường (`Trim().ToLowerInvariant()`) khớp với dữ liệu đã được lưu trữ trong cơ sở dữ liệu (`NormalizedUsername`).
   - Đối chiếu mật khẩu nhập vào với mật khẩu đã băm trong database qua thuật toán PBKDF2-SHA256 (100.000 vòng lặp).
   - Chống tấn công timing attack qua việc thực thi băm giả định (dummy verification) khi tài khoản không tồn tại, đảm bảo thời gian phản hồi đồng nhất.
2. **Cấp phiên JWT hợp lệ**:
   - Tuân thủ tiêu chuẩn RFC 7519 và RFC 7515 (HS256).
   - Payload chứa đầy đủ danh tính: `sub` (User ID), `name` (Username), `displayName` (Tên hiển thị), `jti` (Mã định danh token duy nhất), `iss` (Issuer), `aud` (Audience), `iat` (Thời điểm cấp), `exp` (Thời điểm hết hạn).
   - Ký bằng khóa đối xứng HMAC-SHA256 an toàn.
   - Cấu trúc phiên làm việc trả về `AuthSession` khớp chính xác hợp đồng `GCT-20`:
     - `accessToken`: Chuỗi JWT Base64Url
     - `tokenType`: `"Bearer"`
     - `expiresAt`: Thời điểm hết hạn (ISO 8601 UTC)
     - `user`: Thông tin người dùng công khai (`id`, `username`, `displayName`)
3. **Xử lý lỗi đăng nhập thống nhất**:
   - Sai tên đăng nhập hoặc sai mật khẩu đều trả về mã lỗi duy nhất: `401 Unauthorized`, `code = "INVALID_CREDENTIALS"`, thông điệp `"Tên đăng nhập hoặc mật khẩu không chính xác."`, `retryable = false`.
   - Ngăn chặn triệt để hành vi dò quét danh sách tài khoản (user enumeration).
   - Phản hồi tuyệt đối không chứa mật khẩu thô hoặc mã băm.
4. **Bảo vệ xác thực phiên (Token Verification)**:
   - Xây dựng endpoint bảo vệ `GET /api/v1/users/me` (và alias `/api/users/me`).
   - Kiểm tra chữ ký bằng so sánh thời gian hằng số `CryptographicOperations.FixedTimeEquals` chống tấn công sửa đổi token.
   - Từ chối token bị can thiệp chữ ký, token hết hạn (`exp`), hoặc token không có Bearer header với mã `401 UNAUTHORIZED`.
   - Cung cấp endpoint thu hồi/đăng xuất `POST /api/v1/auth/logout`.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/backend/GameCoTuong.Application/Auth/Models/LoginRequest.cs` | DTO yêu cầu đăng nhập chứa `requestId`, `username`, `password` |
| 2 | `src/backend/GameCoTuong.Application/Auth/Models/AuthSessionDto.cs` | DTO phiên làm việc trả về `accessToken`, `tokenType`, `expiresAt`, `user` |
| 3 | `src/backend/GameCoTuong.Application/Auth/Models/LoginResult.cs` | Kết quả xử lý nghiệp vụ đăng nhập (thành công hoặc thất bại có mã lỗi) |
| 4 | `src/backend/GameCoTuong.Application/Auth/Validation/LoginRequestValidator.cs` | Kiểm tra dữ liệu đầu vào không dùng Regex/Dynamic IL |
| 5 | `src/backend/GameCoTuong.Application/Common/Interfaces/ITokenService.cs` | Interface cấp và xác thực token JWT |
| 6 | `src/backend/GameCoTuong.Application/Auth/Services/ILoginService.cs` | Interface dịch vụ đăng nhập người dùng |
| 7 | `src/backend/GameCoTuong.Infrastructure/Auth/JwtOptions.cs` | Cấu hình JWT (`Issuer`, `Audience`, `SigningKey`, `ExpirationMinutes`) |
| 8 | `src/backend/GameCoTuong.Infrastructure/Auth/JwtTokenService.cs` | Triển khai cấp & xác thực JWT HS256, kiểm tra chữ ký hằng số thời gian |
| 9 | `src/backend/GameCoTuong.Infrastructure/Auth/LoginService.cs` | Nghiệp vụ xác thực đăng nhập, chuẩn hóa username, kiểm tra mật khẩu PBKDF2 |
| 10 | `src/backend/GameCoTuong.Infrastructure/DependencyInjection.cs` | Đăng ký `JwtOptions`, `ITokenService`, `ILoginService` vào DI container |
| 11 | `src/backend/GameCoTuong.Api/Endpoints/AuthEndpoints.cs` | Map các endpoint `POST /api/v1/auth/login`, `POST /api/v1/auth/logout` |
| 12 | `src/backend/GameCoTuong.Api/Endpoints/UserEndpoints.cs` | Map endpoint xác thực `GET /api/v1/users/me` với Bearer token |
| 13 | `src/backend/GameCoTuong.Api/Program.cs` | Kích hoạt `MapAuthEndpoints()` và `MapUserEndpoints()` |
| 14 | `tests/backend/GameCoTuong.Foundation.Tests/LoginAndTokenTests.cs` | Suite 21 kịch bản kiểm thử toàn diện cho login, JWT, token tampering, expiration |

---

## 3. Kết Quả Kiểm Thử

Toàn bộ quy trình xác minh tự động `verify.ps1` đã hoàn thành thành công 100%:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 93, Skipped: 0, Total: 93, Duration: 1 s - GameCoTuong.Foundation.Tests.dll (net10.0)

✓ tests/health.test.ts (4 tests) 26ms
Test Files  1 passed (1)
Tests       4 passed (4)
Vite build: 13 modules transformed, 0 errors.
ESLint: 0 errors, 0 warnings.
```

### Các nhóm kiểm thử chính:
- **Kiểm thử dữ liệu đầu vào**: Chặn request null, thiếu request ID, username ngắn/dài/trống, password ngắn/trống.
- **Kiểm thử JWT**: Cấp token đúng định dạng 3 phần base64url, xác thực claims (`sub`, `name`, `displayName`, `jti`, `iss`, `aud`), từ chối token bị sửa payload, từ chối token bị sửa signature, từ chối token hết hạn, từ chối token ký bởi khóa khác.
- **Kiểm thử xác thực đăng nhập**: Từ chối user không tồn tại bằng 401 INVALID_CREDENTIALS, từ chối sai mật khẩu bằng 401 INVALID_CREDENTIALS, đăng nhập thành công với mật khẩu đúng, cho phép đăng nhập tên tài khoản không phân biệt hoa thường.
- **Kiểm thử bảo mật**: Không trả về mật khẩu hay passwordHash trong bất kỳ response nào.
- **Kiểm thử phiên làm việc**: `GET /api/v1/users/me` trả về thông tin người dùng khi có token hợp lệ; từ chối khi thiếu token hoặc token giả mạo.
- **Kiểm thử đăng xuất**: `POST /api/v1/auth/logout` phản hồi `acknowledged: true`.
