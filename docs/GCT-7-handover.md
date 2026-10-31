# Báo Cáo Bàn Giao Kỹ Thuật: GCT-7 - Đăng Xuất Và Xử Lý Phiên Hết Hạn

> **Task ID**: `GCT-7`  
> **Nhánh**: `GCT-7-logout`  
> **Tiêu chuẩn nghiệp vụ**: `FR-01`, `BR-12`, `GCT-20`, `GCT-24` Handoff Specs  
> **Ngày thực hiện**: 27/10/2026  

---

## 1. Mục Tiêu & Phạm Vi Công Việc

Cung cấp cho người chơi khả năng kết thúc phiên làm việc an toàn, biết rõ khi nào phiên hết hạn cần đăng nhập lại, và ngăn ngừa hành vi vô tình thoát trận đấu khi chưa xác nhận đầu hàng.

### Yêu cầu đã thực hiện:
1. **Đăng xuất về màn hình đăng nhập**:
   - Thao tác đăng xuất trên thanh điều hướng (`AppHeader`) kích hoạt `requestLogout()`.
   - Gọi API `POST /api/v1/auth/logout` với Bearer token hiện tại để thu hồi phiên trên máy chủ.
   - Xóa bỏ toàn bộ dữ liệu phiên trên máy khách (`token`, `user`, `activeMatch` trong memory và `localStorage`).
   - Chuyển trạng thái giao diện về màn hình đăng nhập (`currentView = 'login'`).
   - Đảm bảo sau khi đăng xuất, giao diện tuyệt đối không tiếp tục lưu trữ hoặc sử dụng lại token cũ cho các request tiếp theo.
2. **Xử lý phiên hết hạn (Session Expiry & 401)**:
   - Khi token hết hạn hoặc máy chủ trả về mã `401 UNAUTHORIZED`, hệ thống kích hoạt `handleSessionExpired()`.
   - Ngắt toàn bộ kết nối thời gian thực, xóa thông tin phiên lưu trữ.
   - Hiển thị thông báo nổi bật trên màn hình đăng nhập: *"Phiên đăng nhập đã hết hạn. Kết nối đã được đóng an toàn. Vui lòng đăng nhập lại để tiếp tục chơi."*
   - Tích hợp cờ bảo vệ `isHandlingExpiry` chống lặp chuyển trang (prevent redirect loop).
3. **Bắt buộc xác nhận đầu hàng trước khi đăng xuất trong ván cờ**:
   - Nếu người chơi đang trong ván cờ (`isPlaying = true` / `activeMatch !== null`):
     - Bấm nút "Đăng xuất" sẽ **không** đăng xuất ngay, mà kích hoạt hộp thoại xác nhận `SurrenderModal`:
       *"Bạn đang trong ván cờ đang diễn ra. Nếu đăng xuất lúc này, bạn sẽ bị tính là đầu hàng và xử thua trận đấu. Bạn có chắc chắn muốn đầu hàng để kết thúc phiên và đăng xuất không?"*
     - Nếu chọn **"Hủy, tiếp tục ván cờ"**:
       - Đóng modal, giữ nguyên trận đấu, bàn cờ, kết nối và điểm số; không đăng xuất, không chuyển trang.
     - Nếu chọn **"Xác nhận đầu hàng & Đăng xuất"**:
       - Ghi nhận đầu hàng, dọn dẹp ván cờ, thu hồi token, xóa phiên và chuyển hướng về màn hình đăng nhập an toàn.

---

## 2. Chi Tiết Các Tệp Triển Khai

| STT | Tệp tin | Vai trò kiến trúc |
|:---:|---|---|
| 1 | `src/frontend/src/types/auth.ts` | Khai báo kiểu dữ liệu TypeScript cho `User`, `AuthSession`, `MatchInfo`, `AppView` |
| 2 | `src/frontend/src/api/auth.ts` | Các hàm gọi API HTTP `loginApi`, `logoutApi`, `fetchMeApi` |
| 3 | `src/frontend/src/composables/useAuth.ts` | Store quản lý trạng thái phiên, đăng xuất, xác nhận đầu hàng và xử lý phiên hết hạn |
| 4 | `src/frontend/src/components/SurrenderModal.vue` | Modal xác nhận đầu hàng trước khi đăng xuất (hỗ trợ phím ESC, focus trap, accessibility) |
| 5 | `src/frontend/src/components/LoginView.vue` | Giao diện đăng nhập kèm banner thông báo khi phiên hết hạn |
| 6 | `src/frontend/src/components/LobbyView.vue` | Giao diện sảnh chờ cho người dùng đã xác thực |
| 7 | `src/frontend/src/components/GameView.vue` | Giao diện bàn cờ Tướng thi đấu với nút đầu hàng và cảnh báo đăng xuất |
| 8 | `src/frontend/src/components/AppHeader.vue` | Thanh tiêu đề hiển thị trạng thái trận đấu, tên kỳ thủ và nút đăng xuất |
| 9 | `src/frontend/src/App.vue` | Tích hợp điều phối các view, modal đầu hàng và vòng đời phiên |
| 10 | `src/frontend/tests/logout.test.ts` | Bộ 7 kịch bản kiểm thử tự động toàn diện cho luồng đăng xuất và hết phiên |

---

## 3. Kết Quả Kiểm Thử

### Frontend Tests (Vitest):
```
 ✓ tests/health.test.ts (4 tests) 29ms
 ✓ tests/logout.test.ts (7 tests) 25ms

 Test Files  2 passed (2)
      Tests  11 passed (11)
```

### Frontend Build & Lint:
- `eslint . --max-warnings=0`: **0 errors, 0 warnings**.
- `vue-tsc --noEmit && vite build`: **31 modules transformed, 0 errors**.

### Backend Tests (Release):
- `dotnet test GameCoTuong.sln -c Release`: **Passed 109 / 109 tests**.
