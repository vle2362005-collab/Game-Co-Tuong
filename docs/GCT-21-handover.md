# GCT-21 — Bàn giao khởi tạo dự án

26/10/2026 · [Jira GCT-21](https://vle2362005.atlassian.net/browse/GCT-21) · Nhánh `feature/GCT-21-repository`, PR vào `develop`.

## Đối chiếu yêu cầu

| Tiêu chí của thẻ | Đầu ra |
|---|---|
| Cấu trúc Vue, ASP.NET Core, bộ luật, kiểm thử, tài liệu | `src/frontend`, năm project `src/backend`, `tests/backend`, `src/frontend/tests`, `docs` |
| Phiên bản công cụ và phụ thuộc chung | `global.json`, `.nvmrc`, `Directory.Packages.props`, `package.json`, lockfile NuGet/npm |
| Cấu hình mẫu, .gitignore | `.env.example`, `appsettings.Local.example.json`, `.gitignore`, `.editorconfig`, `.gitattributes` |
| Main, develop và hướng dẫn chạy | `develop` tạo từ main hiện tại; PR feature vào develop; README, development.md và verify.sh/ps1 |
| Bản sao mới build được frontend/backend | Clone mới không có build output, chạy toàn bộ verify.sh thành công; Git không thay đổi file nguồn/lockfile sau kiểm tra |
| Không có bí mật thật trong mẫu | Windows Integrated Security không có mật khẩu; JWT SigningKey trống; biến VITE chỉ có URL API; file riêng bị ignore |

## Kết quả thực tế

Kiểm tra bằng .NET SDK 10.0.403 / runtime 10.0.14, Node 24.22.1, npm 11.9.0 trên Linux. Đã clone nhánh nguồn vào thư mục trống và chạy nguyên script `scripts/verify.sh`, không sao chép bin, obj, node_modules hay dist sang bản clone.

| Kiểm tra | Kết quả |
|---|---|
| NuGet restore theo lockfile | Pass, đủ 6 project, không sửa lockfile |
| Backend Release build | Pass, 0 warning, 0 error |
| Backend xUnit | 10 pass, 0 fail, 0 skip |
| npm ci + vue-tsc + Vite build | Pass, tạo bundle production |
| Frontend Vitest | 4 pass, 0 fail |
| Khởi động HTTP thực tế | `/health` trả 200 và Healthy; frontend trả 200; origin localhost:5173 nhận đúng CORS |
| File nguồn và cấu hình | Generated output/local config bị ignore; mẫu được theo dõi; Git sạch sau verify ở bản clone |

Backend tests kiểm tra health khi chưa có DB/JWT, origin hợp lệ/bị từ chối, cấu hình CORS sai và endpoint nghiệp vụ chưa triển khai trả 404. Frontend tests kiểm tra URL/cancellation signal, HTTP lỗi, payload sai và mất kết nối không được báo thành công.

Các lệnh MSBuild dùng `-m:1` và tắt shared compiler để chạy ổn định trong môi trường kiểm tra hạn chế IPC. Không vô hiệu hóa kiểm thử hoặc bỏ qua warning để có kết quả pass. Script PowerShell có cùng chuỗi lệnh và kiểm tra exit code từng bước; chưa chạy trực tiếp trên Windows. Chưa có kiểm tra giao diện bằng trình duyệt thật trong lần bàn giao này.

## Phần tiếp theo

GCT-21 cung cấp bộ khung chạy được. Domain/Engine/Application/Infrastructure hiện có project và ranh giới tham chiếu, chưa có xử lý luật, tài khoản, DB, phòng, trận hoặc SignalR nghiệp vụ. Các phần đó thuộc GCT-26 trở đi. CI do GCT-23, bảo vệ nhánh/quyền duyệt do GCT-22; không tự đánh dấu các thẻ đó hoàn thành.

GCT-15/GCT-20 đang ở các nhánh feature đã được merge trước, chưa nằm trong main lúc tạo bộ khung. Không sao chép lại các tệp đó vào GCT-21; README trỏ tới commit tài liệu đã bàn giao. Trưởng nhóm tích hợp tài liệu và duyệt PR nền tảng trước khi yêu cầu thành viên cập nhật develop, rồi mới tách nhánh làm việc.

Giữ GCT-21 In Progress/chờ review đến khi trưởng nhóm xác nhận và merge PR. Không tự merge, không thay đổi quyền duyệt của trưởng nhóm.
