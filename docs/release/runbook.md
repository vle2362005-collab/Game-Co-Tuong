# Cài đặt, sử dụng và đóng gói — GCT-99 / GCT-18

## Dựng và kiểm tra

1. Cài .NET SDK **10.0.403**, Node **24.22.1**, npm **11.x**, SQL Server 2022 Developer cho bản demo có phục hồi tự động. Clone repo và checkout commit cần duyệt.
2. Chạy `bash scripts/verify.sh` hoặc `./scripts/verify.ps1`: build, test và báo cáo `artifacts/qa`. Khởi tạo tài khoản bằng Đăng ký; tài liệu không chứa mật khẩu demo cố định.
3. Backend: sao chép file cấu hình mẫu ở project API thành `appsettings.Local.json`. Điền `ConnectionStrings:GameDatabase`; cấp `Jwt__SigningKey` ngẫu nhiên ít nhất 32 byte qua biến môi trường, giữ ngoài Git. Đặt CORS `BrowserClient__AllowedOrigins__0` đúng origin frontend và `AllowedHosts` đúng hostname.
4. Chạy `dotnet tool restore`; tạo script DB bằng lệnh ở script package. Người quản trị kiểm tra và áp dụng `artifacts/demo/database/migrations.sql` trên SQL Server đã sao lưu. Script idempotent dùng lịch sử migration; không tự chạy SQL lên DB nhóm.
5. Frontend: sao chép `.env.example` sang `.env.local`, đặt `VITE_API_BASE_URL` đúng API trước khi build. Camera cần HTTPS hoặc localhost. TURN dùng credential ngắn hạn; chế độ `relay` và cách lưu minh chứng trong `docs/sprint3.md`.
6. Chạy `bash scripts/package-demo.sh` hoặc `./scripts/package-demo.ps1`. Đầu ra gồm API publish, web tĩnh, SQL, cấu hình mẫu, commit nguồn và manifest SHA-256. Sau commit, dựng lại để `WORKTREE_STATE` là `clean`.
7. Chạy `dotnet artifacts/demo/api/GameCoTuong.Api.dll --urls http://localhost:5000`. Serve web bằng host tĩnh có fallback mọi route về `index.html`; dùng HTTPS/reverse proxy hỗ trợ WebSocket khi truy cập từ thiết bị khác. Với Vite dev: `npm --prefix src/frontend run dev`.
8. Kiểm tra `/health` (chỉ tiến trình), đăng ký/đăng nhập (kiểm tra DB/JWT), tạo/join phòng, đánh nước, đầu hàng và đọc lịch sử. Chạy `node scripts/smoke-demo.mjs` để thử bản publish trên SQLite tạm, tự tạo tài khoản và khóa ngẫu nhiên; không chạm DB nhóm. Liveness không chứng minh DB/TURN hoạt động.

## Thao tác người chơi

Hai cửa sổ trình duyệt đăng nhập hai tài khoản → tạo/join mã phòng → cả hai Sẵn sàng → chủ phòng Bắt đầu. Chọn quân rồi điểm gợi ý; nước sai bị chặn. Mất mạng sẽ khóa bàn và hiện hạn nối lại. Đầu hàng cần xác nhận; hòa cần đối thủ chấp nhận trong 30 giây. Từ lịch sử chọn **Xem lại ván**, dùng đầu/lùi/tiến/cuối; màn hình này chỉ đọc. Trong sảnh chọn Dễ/Vừa/Khó để chơi máy. Video cần Gọi/Nhận trước khi xin quyền; lỗi thiết bị không thay kết quả cờ.

## Lỗi thường gặp và vận hành

| Hiện tượng | Xử lý |
|---|---|
| Sai SDK/Node | Cài version ở `global.json` / `.nvmrc`, không sửa cấu hình chung. |
| API chạy nhưng đăng nhập lỗi DB | Kiểm tra connection string, quyền SQL và migration. |
| 401 / reload mất phiên | Đăng nhập lại; token được giữ trong bộ nhớ. |
| CORS hoặc WebSocket lỗi | Kiểm tra origin chính xác, scheme/cổng, reverse proxy và API URL khi build. |
| Replay báo thiếu dữ liệu | Giữ nguyên lịch sử, kiểm tra số nước và snapshot; không tự sửa/quyết định nước. |
| Camera/khác mạng không kết nối | Kiểm tra HTTPS, quyền thiết bị và TURN; xem giới hạn trong báo cáo nghiệm thu. |

Trước triển khai: dùng SSMS **Tasks → Back Up** tạo bản sao DB, kiểm tra restore vào DB riêng; giữ bộ package trước và manifest. Khi cần rollback: dừng API, chạy lại package trước; nếu schema không tương thích, quản trị viên phục hồi bản DB đã kiểm chứng rồi kiểm tra đăng nhập/phòng/lịch sử. Không tự xóa hoặc ghi đè DB trong script package.

Chạy nhanh độc lập bằng SQLite có thể dùng `Database__Provider=Sqlite`, `ConnectionStrings__GameDatabase=Data Source=<đường dẫn riêng>`, cùng JWT ngẫu nhiên. Đây là chế độ kiểm tra cục bộ: worker restart/timeout/cleanup tự động bị tắt; không thay thế nghiệm thu SQL Server.
