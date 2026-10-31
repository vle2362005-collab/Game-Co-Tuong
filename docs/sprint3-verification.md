# Kết quả kiểm thử Sprint 3 — 31/10/2026

- Backend Release: **144/144 đạt**, không skip. Bao gồm đăng ký/JWT/phân quyền, luật 7 quân, tự chiếu, chiếu bí/hết nước, Minimax thắng ngay, tranh ghế, bắt đầu/gửi nước lặp, rollback khi ghi lỗi và dựng lại lịch sử.
- Frontend: **43/43 đạt**; lint, kiểm tra TypeScript và production build đạt. Có ca quyền camera đến chậm sau khi hủy, ICE chờ remote description, từ chối cuộc gọi, dọn track và ánh xạ 90 tọa độ ở hai hướng.
- `dotnet restore --locked-mode`: đạt.
- Browser: **7 nhóm luồng đạt** qua `scripts/sprint3-browser-check.cjs`. Hai tab giữ hai tài khoản riêng trong bộ nhớ; tạo/tham gia/sẵn sàng/bắt đầu, SVG 32 quân, chat literal HTML, nước sai và nước đúng cả hai hướng, video/âm thanh, mic/camera/kết thúc, màn hình 390px và đầu hàng. Không có page error.

Video thực sự thiết lập RTCPeerConnection và nhận khung hình cùng audio track ở phía đối thủ, dùng **thiết bị giả lập**, chạy trong môi trường localhost với loopback ICE. Đây không phải kiểm thử camera vật lý hoặc kết nối giữa hai mạng Internet khác nhau. HTTPS/TURN và các giới hạn triển khai xem [hướng dẫn](sprint3.md).

Nguồn kiểm thử: `XiangqiRulesTests.cs`, `GameFlowTests.cs`, `AuthorizationAndRoomAccessTests.cs`, `videoCall.test.ts`, `sprint3-browser-check.cjs`. Ảnh và JSON kết quả browser ở `artifacts/sprint3/` tại workspace; không chứa token và không đưa vào commit.

Jira đã đối chiếu: Sprint 49 có 50 thẻ, giữ nguyên thẻ đầu GCT-23 và thứ tự. 49 mô tả đã rút gọn; trạng thái 49 thẻ còn lại là In Progress, chờ push/CI/review. Chưa có PR mới, chưa merge develop, chưa kết thúc Sprint.
