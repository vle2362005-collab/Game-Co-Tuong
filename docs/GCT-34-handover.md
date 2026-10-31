# Báo Cáo Bàn Giao Kỹ Thuật: GCT-34 - Quản Lý Phiên Trên Giao Diện

> **Task ID**: `GCT-34`  
> **Nhánh**: `GCT-34-auth-state`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `FR-02`, `BR-03`, `BR-11`, `BR-12`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Thiết lập chính sách quản lý phiên bảo mật tuyệt đối trên giao diện: Lưu giữ Access Token hoàn toàn trong bộ nhớ (In-memory token), chặn lưu trữ token và mật khẩu trong `localStorage`, gắn token vào các yêu cầu HTTP và kết nối SignalR, bảo vệ các màn hình riêng tư qua Navigation Guards, xử lý điều hướng chính xác về màn hình đích sau đăng nhập và chống vòng lặp chuyển trang.

### Yêu cầu đã thực hiện:

1. **Quản lý Access Token trong bộ nhớ (In-memory Auth State)**:
   - Access Token và thông tin người dùng được quản lý hoàn toàn trong bộ nhớ ứng dụng (`ref<string | null>(null)`).
   - Tuyệt đối không lưu token hoặc mật khẩu trong `localStorage` hay `sessionStorage`, xóa bỏ mọi mã ghi vào `localStorage` từ các phiên bản trước.
   - Khi ứng dụng tải lại trang (F5/Reload): Phiên trong bộ nhớ tự động được thiết lập lại về `null`, hệ thống yêu cầu đăng nhập lại an toàn ("tải lại trang yêu cầu đăng nhập lại").

2. **Gắn Token vào API Client và SignalR**:
   - `ApiClient`: Tự động trích xuất token trong bộ nhớ qua `globalTokenGetter` và đính kèm vào header `Authorization: Bearer <token>` của mọi yêu cầu.
   - `SignalRClient` (`src/frontend/src/api/signalr.ts`): Gắn access token từ bộ nhớ vào tham số kết nối (`/hubs/game/negotiate?negotiateVersion=1&access_token=<token>`) và header xác thực.

3. **Chặn màn hình riêng & Điều hướng đúng trang đích**:
   - Navigation Guard (`router.beforeEach`) kiểm tra trạng thái xác thực trong bộ nhớ (`useAuth().isAuthenticated.value`).
   - Nếu chưa đăng nhập, mọi thao tác truy cập màn hình riêng (`/lobby`, `/rooms/:id`, `/game/:id`, `/history`) đều bị chặn và chuyển hướng về `/login?redirect=<đường_dẫn_gốc>`.
   - Sau khi đăng nhập thành công, giao diện đọc tham số `redirect` và chuyển tiếp chính xác về màn hình người dùng muốn mở; mặc định chuyển về `/lobby` nếu không có tham số chuyển hướng.

4. **Xóa trạng thái và đóng kết nối khi hết phiên / đăng xuất**:
   - Khi người dùng đăng xuất (`performLogout`) hoặc hết phiên (`handleSessionExpired` do token hết hạn hoặc nhận mã lỗi 401):
     - Xóa sạch token, user và trận đấu trong bộ nhớ.
     - Đóng kết nối SignalR ngay lập tức (`signalR.disconnect()`).
     - Điều hướng an toàn về `/login` với cờ thông báo `sessionExpired`.
     - Cơ chế chống lặp chuyển trang (`isHandlingExpiry` guard và kiểm tra đường dẫn hiện tại) ngăn chặn tuyệt đối các vòng lặp điều hướng vô hạn.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/frontend/src/composables/useAuth.ts` | Quản lý token thuần bộ nhớ (in-memory), kết nối SignalR, loại bỏ localStorage |
| 2 | `src/frontend/src/api/client.ts` | Bổ sung `setApiAuthHandlers` tự động đọc token in-memory và xử lý 401 tập trung |
| 3 | `src/frontend/src/api/signalr.ts` | Khởi tạo kết nối SignalR với token in-memory và phương thức đóng kết nối an toàn |
| 4 | `src/frontend/src/router/index.ts` | Cập nhật `checkAuthToken` đọc trạng thái bộ nhớ, guard chặn route riêng và chống lặp |
| 5 | `src/frontend/tests/logout.test.ts` | Cập nhật assertion đảm bảo localStorage luôn `null` theo chính sách GCT-34 |
| 6 | `src/frontend/tests/authState.test.ts` | 7 kịch bản kiểm thử tự động cho in-memory token, SignalR, redirect và reload |
| 7 | `docs/GCT-34-handover.md` | Tài liệu bàn giao kỹ thuật |

---

## 3. Kết Quả Kiểm Thử

- **Frontend Tests (Vitest)**: **39 / 39 Passed** (toàn bộ 6 test suites bao gồm 7 test case mới trong `authState.test.ts` pass 100%).
- **Frontend Linter (ESLint)**: **0 errors, 0 warnings**.
- **Frontend Build (Vite + Vue-TSC)**: **55 modules transformed**, build production thành công 100%.
- **Backend Tests (.NET)**: **113 / 113 Passed** (toàn bộ kiểm thử hệ thống đạt 100%).
