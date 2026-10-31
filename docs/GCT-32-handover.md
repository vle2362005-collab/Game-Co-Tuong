# Báo Cáo Bàn Giao Kỹ Thuật: GCT-32 - Tạo Khung Giao Diện Và Kết Nối API

> **Task ID**: `GCT-32`  
> **Nhánh**: `GCT-32-vue-shell`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `FR-02`, `FR-03`, `FR-11`, `FR-13`, `BR-12`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Xây dựng kiến trúc khung giao diện dùng chung (Application Shell) cho hệ thống Game Cờ Tướng, cấu hình hệ thống định tuyến Vue Router 4, quản lý trạng thái ứng dụng phản ứng và xây dựng API client thống nhất có cơ chế cấu hình linh hoạt, quản lý thời gian chờ (timeout), bóc tách mã lỗi chuẩn và tự động thử lại (retry with exponential backoff).

### Yêu cầu đã thực hiện:

1. **Cấu hình Vue Router 4 & Hệ thống đường dẫn điều hướng**:
   - `/login`: Màn hình đăng nhập tài khoản.
   - `/lobby`: Sảnh chờ người chơi (yêu cầu xác thực `requiresAuth`).
   - `/rooms/:id` & alias `/room/:id`: Màn hình phòng cờ, hỗ trợ chọn phe Tiên (Đỏ) / Hậu (Đen) và bắt đầu trận đấu.
   - `/game/:id` & alias `/match/:id`: Màn hình ván đấu cờ tướng với bàn cờ và nút đầu hàng.
   - `/history`: Màn hình lịch sử thi đấu, hiển thị kết quả các ván đấu, đối thủ, số nước cờ và bộ lọc theo trạng thái (Thắng, Thua, Hòa).
   - `/`: Chuyển hướng thông minh dựa trên trạng thái phiên (vào `/lobby` nếu đã đăng nhập, vào `/login` nếu chưa).
   - Bắt lỗi đường dẫn không hợp lệ (`/:pathMatch(.*)*`) tự động điều hướng an toàn về trang chủ.
   - Navigation Guards kiểm soát phiên: Chặn người dùng chưa xác thực vào các trang yêu cầu đăng nhập và chuyển hướng kèm tham số `redirect`; ngăn người đã đăng nhập vào lại `/login`.

2. **API Client thông nhất (`ApiClient`)**:
   - Đọc địa chỉ API linh hoạt từ biến môi trường `VITE_API_BASE_URL` hoặc fallback mặc định `http://localhost:5000`.
   - Cơ chế tự động đính kèm `Authorization: Bearer <token>` và các header tiêu chuẩn (`Content-Type: application/json`, `Accept: application/json`).
   - Quản lý thời gian chờ (Timeout) với `AbortSignal`, báo lỗi `TIMEOUT` kịp thời, không làm treo ứng dụng.
   - Xử lý lỗi chuẩn hóa: Phân tích envelope lỗi `ApiErrorEnvelope` (`{ error: { code, message, details } }`), trích xuất đúng mã lỗi kỹ thuật.
   - Cơ chế thử lại (Retry & Exponential Backoff): Tự động thử lại khi gặp sự cố mạng (network failure) hoặc lỗi máy chủ (5xx) với độ trễ tăng dần; tuyệt đối không retry vô ích với lỗi client 4xx (400, 401, 404).
   - Xử lý phiên hết hạn (401 Unauthorized): Tự động kích hoạt callback `onUnauthorized` để dọn phiên và ngắt kết nối an toàn.

3. **Khung giao diện dùng chung (App Shell Layout)**:
   - Header điều hướng (`AppHeader.vue`) hiển thị thương hiệu, thanh chuyển trang (Sảnh chờ, Lịch sử), chỉ báo trạng thái trận đấu và nút đăng xuất.
   - Shell chính (`App.vue`) tích hợp `<router-view>` hiển thị các màn hình con mượt mà, đồng bộ với store xác thực và modal xác nhận đầu hàng `SurrenderModal`.
   - Thiết kế giao diện mang đậm phong cách truyền thống Cờ Tướng (màu đỏ thắm hoàng gia, ngọc bích sẫm, giấy bồi cổ, viền tinh tế, chuẩn hóa typography và responsive trên thiết bị di động).

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/frontend/src/router/index.ts` | Cấu hình Vue Router 4, định nghĩa các route, alias và auth navigation guards |
| 2 | `src/frontend/src/api/client.ts` | API Client thống nhất với cơ chế timeout, retry, backoff và error parsing |
| 3 | `src/frontend/src/components/RoomView.vue` | Màn hình phòng cờ, hiển thị thông tin phòng, chọn phe Đỏ/Đen và vào trận |
| 4 | `src/frontend/src/components/HistoryView.vue` | Màn hình lịch sử thi đấu, hỗ trợ lọc kết quả và thử lại khi tải lỗi |
| 5 | `src/frontend/src/components/AppHeader.vue` | Bổ sung thanh điều hướng chuyển trang giữa Sảnh chờ và Lịch sử |
| 6 | `src/frontend/src/components/LobbyView.vue` | Thêm phím tắt điều hướng nhanh đến phòng cờ và xem lịch sử thi đấu |
| 7 | `src/frontend/src/composables/useAuth.ts` | Bổ sung singleton `useAuth` kết nối router và quản lý phiên toàn cục |
| 8 | `src/frontend/src/App.vue` | Cấu trúc App Shell tích hợp `<router-view>` dùng chung cho toàn bộ màn hình |
| 9 | `src/frontend/src/main.ts` | Đăng ký Vue Router vào Vue Application instance |
| 10 | `src/frontend/tests/router.test.ts` | 6 kịch bản kiểm thử tự động cho router, alias, auth guards và redirect query |
| 11 | `src/frontend/tests/apiClient.test.ts` | 8 kịch bản kiểm thử tự động cho ApiClient (timeout, retry, 4xx, 5xx, header, auth) |
| 12 | `docs/GCT-32-handover.md` | Tài liệu bàn giao kỹ thuật |

---

## 3. Kết Quả Kiểm Thử

- **Frontend Tests (Vitest)**: **25 / 25 Passed** (thêm 14 bài kiểm thử mới cho Router và ApiClient, 11 bài kiểm thử cũ của health và logout tiếp tục pass 100%).
- **Frontend Linter (ESLint)**: **0 errors, 0 warnings**.
- **Frontend Build (Vite + Vue-TSC)**: **54 modules transformed**, build production thành công 100%.
- **Backend Tests (.NET)**: **113 / 113 Passed** (không xảy ra xung đột hay suy thoái code).
