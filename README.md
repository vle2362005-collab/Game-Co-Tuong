# Game Cờ Tướng

Đồ án cuối kỳ môn **Quản lý dự án với Jira**: xây dựng web chơi cờ Tướng, đồng thời thực hành lập kế hoạch, chia Sprint, phân công và theo dõi tiến độ bằng Jira; quản lý mã nguồn, kiểm thử và review qua GitHub Pull Request.

## Chức năng chính

- Đăng ký, đăng nhập và xác thực JWT.
- Tạo/tham gia phòng, sẵn sàng và chơi hai người theo thời gian thực.
- Kiểm tra luật, lượt đi, chiếu, đầu hàng và đề nghị hòa.
- Chơi với máy bằng Minimax và Alpha-Beta ở ba mức độ.
- Chat, gọi video, phục hồi kết nối, lưu lịch sử và xem lại từng nước.

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Frontend | Vue 3, TypeScript, Vite |
| Backend | ASP.NET Core, Entity Framework Core, SignalR |
| Dữ liệu và kiểm thử | SQL Server; SQLite cho local/test; xUnit, Vitest |

## Cài đặt và chạy

Cài **.NET SDK 10.0.403**, **Node.js 24.22.1**, **npm 11.x**.

```powershell
git clone --branch main https://github.com/vle2362005-collab/Game-Co-Tuong.git
cd Game-Co-Tuong
dotnet restore GameCoTuong.sln --locked-mode
npm.cmd --prefix src/frontend ci
Copy-Item src/frontend/.env.example src/frontend/.env.local
```

Cấu hình database, migration và khóa JWT theo [hướng dẫn chạy](docs/release/runbook.md). SQL Server 2014 cần bổ sung cấu hình EF tương thích mức 120 và kiểm tra trên máy thực tế. Giữ connection string và khóa riêng trong cấu hình local, ngoài Git.

Mở hai cửa sổ PowerShell tại thư mục dự án:

```powershell
# Cửa sổ 1: backend
dotnet run --project src/backend/GameCoTuong.Api --no-launch-profile --urls http://localhost:5000

# Cửa sổ 2: frontend
npm.cmd --prefix src/frontend run dev
```

Truy cập **http://localhost:5173**. Đăng ký hai tài khoản → tạo/tham gia phòng → cả hai sẵn sàng → chủ phòng bắt đầu. Kiểm tra toàn bộ bằng `./scripts/verify.ps1` trên PowerShell hoặc `bash scripts/verify.sh` trên Bash.

## Cấu trúc và tài liệu

- `src/frontend`: giao diện, REST/SignalR client, video và replay.
- `src/backend`: API, Domain, Application, Infrastructure và Engine luật/AI.
- `tests/backend`, `src/frontend/tests`: kiểm thử tự động; `docs`: tài liệu dự án.

[Yêu cầu](docs/requirements.md) · [Thiết kế](docs/release/analysis-design.md) · [CSDL/ERD](docs/database/erd.md) · [Kiểm thử](docs/release/verification.md) · [Kịch bản demo](docs/release/demo.md)

## Quản lý với Jira và GitHub

[Jira GCT](https://vle2362005.atlassian.net/jira/software/c/projects/GCT/boards/36/backlog) quản lý Epic, Story, Task và Sprint. Mỗi công việc gắn mã GCT với nhánh, commit và PR; thẻ Jira có mô tả ngắn cùng liên kết minh chứng. PR vào `develop`, trưởng nhóm duyệt và merge; `main` nhận bản ổn định qua PR. Xem [quy trình đóng góp](CONTRIBUTING.md).

## Phạm vi demo

SQLite local tắt worker phục hồi/timeout/cleanup tự động. Camera cần HTTPS hoặc localhost; cuộc gọi khác mạng cần kiểm tra TURN. SQL Server thực tế, LAN và video hai thiết bị cần nghiệm thu riêng. Phân xử lặp thế WXF đầy đủ chưa hỗ trợ; xem [giới hạn luật](docs/repetition-scope.md).
