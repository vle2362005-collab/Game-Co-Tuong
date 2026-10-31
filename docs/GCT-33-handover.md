# Báo Cáo Bàn Giao Kỹ Thuật: GCT-33 - Hiển Thị Màn Hình Đăng Ký Và Đăng Nhập

> **Task ID**: `GCT-33`  
> **Nhánh**: `GCT-33-auth-forms`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `BR-01`, `BR-02`, `BR-12`, `GCT-20` Contracts  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Cung cấp giao diện đăng ký tài khoản mới và đăng nhập hoàn chỉnh với hệ thống nhãn, thông báo lỗi chi tiết từng trường, kết nối API thực tế, cơ chế chống gửi yêu cầu trùng lặp khi bấm liên tục và tuân thủ tuyệt đối an toàn mật khẩu (không ghi log).

### Yêu cầu đã thực hiện:

1. **Giao diện Đăng ký & Đăng nhập (Auth Forms)**:
   - Tích hợp 2 tab chuyển đổi mượt mà giữa **Đăng nhập** và **Đăng ký tài khoản** trong `LoginView.vue` kèm route `/login` và `/register`.
   - Form Đăng ký hỗ trợ đầy đủ các trường: Tên đăng nhập (`username`), Biệt danh hiển thị (`displayName`), Địa chỉ Email (`email`), Mật khẩu (`password`), và Xác nhận mật khẩu (`confirmPassword`).
   - Form Đăng nhập hỗ trợ: Tên đăng nhập (`username`) và Mật khẩu (`password`).
   - Nhãn rõ ràng kèm dấu sao bắt buộc (`*`), placeholder trực quan, thuộc tính `autocomplete` tiêu chuẩn.

2. **Kiểm tra dữ liệu & Thông báo lỗi chi tiết từng trường (Field-level Validation)**:
   - Kiểm tra phía client trước khi gửi: Báo lỗi khi thiếu dữ liệu, tên đăng nhập không hợp lệ (3-30 ký tự, ký tự đặc biệt), email sai định dạng, mật khẩu ngắn (< 6 ký tự), hoặc mật khẩu xác nhận không khớp.
   - Bóc tách và hiển thị lỗi từ API: Xử lý envelope lỗi `ApiErrorEnvelope` và từ điển `fieldErrors` của backend (ví dụ: `USERNAME_TAKEN`, `EMAIL_TAKEN`, `VALIDATION_ERROR`, `INVALID_CREDENTIALS`).
   - Lỗi được hiển thị ngay bên dưới ô nhập tương ứng (`.field-error`) kèm viền đỏ nổi bật và `role="alert"`.

3. **Khóa nút gửi & Chống gửi trùng lặp (Submission Lock / Debounce)**:
   - Khóa nút gửi (`disabled`) khi đang thực thi yêu cầu, hiển thị spinner và văn bản trạng thái (`Đang đăng nhập…`, `Đang tạo tài khoản…`).
   - Cơ chế bảo vệ 2 tầng: Cờ `localSubmitting` tại giao diện và cờ `isSubmitting` tại store `useAuth` ngăn chặn triệt để mọi hành vi bấm nút liên tục.
   - Tự động mở khóa nút ngay khi có phản hồi lỗi từ server hoặc lỗi mạng để người dùng có thể chỉnh sửa và thử lại ngay lập tức.

4. **Kết nối API thật & Chuyển trang**:
   - Gọi endpoint thật `POST /api/v1/auth/login` và `POST /api/v1/auth/register`.
   - Đăng ký thành công sẽ tự động đăng nhập và chuyển tiếp thẳng vào Sảnh chờ (`/lobby` hoặc trang đích theo query `redirect`).
   - Đăng nhập thành công lưu phiên (token + user) và chuyển vào Sảnh chờ.

5. **Bảo mật thông tin (BR-12)**:
   - Tuyệt đối không ghi mật khẩu thô vào console log hay nhật ký hệ thống.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/frontend/src/api/auth.ts` | Bổ sung hàm `registerApi`, lớp `AuthApiError` và bóc tách `fieldErrors` từ API |
| 2 | `src/frontend/src/composables/useAuth.ts` | Thêm hàm `register`, quản lý `fieldErrors`, khóa gửi kép `isSubmitting` |
| 3 | `src/frontend/src/components/LoginView.vue` | Màn hình xác thực với 2 tab Đăng nhập / Đăng ký, nhãn, lỗi từng trường và khóa nút |
| 4 | `src/frontend/src/router/index.ts` | Đăng ký route `/register` và điều hướng người đã đăng nhập sang `/lobby` |
| 5 | `src/frontend/src/App.vue` | Kết nối sự kiện `@register`, truyền `fieldErrors` xuống component con qua Router Shell |
| 6 | `src/frontend/tests/authForms.test.ts` | 7 kịch bản kiểm thử tự động cho đăng nhập, đăng ký, lỗi từng trường, chống double click, và bảo mật log |
| 7 | `docs/GCT-33-handover.md` | Tài liệu bàn giao kỹ thuật |

---

## 3. Kết Quả Kiểm Thử

- **Frontend Tests (Vitest)**: **32 / 32 Passed** (bao gồm 7 bài kiểm thử mới cho auth forms).
- **Frontend Linter (ESLint)**: **0 errors, 0 warnings**.
- **Frontend Build (Vite + Vue-TSC)**: **54 modules transformed**, build production thành công 100%.
- **Backend Tests (.NET)**: **113 / 113 Passed** (toàn bộ kiểm thử hệ thống đạt 100%).
