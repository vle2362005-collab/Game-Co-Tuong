> Cập nhật 26/10/2026: ruleset [25888788](https://github.com/vle2362005-collab/Game-Co-Tuong/settings/rules/25888788) đã Active cho main/develop, được xác minh lại qua API. Chỉ user trưởng nhóm bypass qua PR. Các đoạn mô tả chưa áp dụng bên dưới là baseline trước khi cấu hình; không còn là trạng thái hiện tại. Chưa thử bằng tài khoản thành viên.

# Game Cờ Tướng

Vue/TypeScript và ASP.NET Core: tài khoản, phòng hai người, luật cờ, SignalR, chat/video, phục hồi, PvE Minimax/Alpha-Beta, lịch sử và xem lại từng nước.

## Chạy và bàn giao

Cài **.NET SDK 10.0.403**, **Node 24.22.1**, **npm 11.x**. Clone repo, cập nhật `develop`, checkout đúng commit cần kiểm tra.

- [Cài đặt, SQL Server/JWT/CORS/HTTPS/TURN, sử dụng và rollback](docs/release/runbook.md).
- [Kết quả kiểm thử, p50/p95 và phần chưa nghiệm thu](docs/release/verification.md).
- [Phân tích/thiết kế](docs/release/analysis-design.md), [kịch bản bảo vệ](docs/release/demo.md), [bàn giao](docs/release/handover.md).

```sh
# Kiểm tra toàn bộ (hoặc ./scripts/verify.ps1 trên PowerShell)
bash scripts/verify.sh
# Đóng gói API + web + SQL + manifest (hoặc ./scripts/package-demo.ps1)
bash scripts/package-demo.sh
# Smoke test bản publish qua HTTP, DB/khóa/tài khoản tạm
node scripts/smoke-demo.mjs
```

Để chơi: làm theo runbook cấu hình DB/JWT rồi chạy API tại `http://localhost:5000`, frontend `npm --prefix src/frontend run dev` tại `http://localhost:5173`. Hai tài khoản tự đăng ký → tạo/join phòng → ready/start. Trong sảnh có chơi máy; từ lịch sử có xem lại chỉ đọc với nút đầu/lùi/tiến/cuối.

SQL Server là cấu hình demo có worker phục hồi tự động. SQLite tạm phục vụ kiểm thử/local, worker tự động bị tắt. Camera cần HTTPS hoặc localhost; cuộc gọi khác mạng cần xác minh TURN thực. Không có mật khẩu/credential dùng chung trong repo.

## Mã nguồn và tài liệu

| Thư mục | Nội dung |
|---|---|
| src/frontend | Vue, API/SignalR client, bàn cờ, video và replay |
| src/backend/GameCoTuong.Api | REST/Hub, danh tính, log và lỗi |
| src/backend/GameCoTuong.Infrastructure | GameService, EF Core, migration và worker |
| src/backend/GameCoTuong.Engine | Bộ luật, bàn cờ, Minimax/Alpha-Beta |
| src/backend/GameCoTuong.Domain / Application | Entity/value object và hợp đồng |
| tests/backend, src/frontend/tests | Kiểm thử tự động |

[Phạm vi](docs/requirements.md), [luật](docs/rules.md), [ERD](docs/database/erd.md), [video và giới hạn](docs/sprint3.md), [giới hạn WXF](docs/repetition-scope.md), [CI](docs/ci.md).

Tách nhánh có mã Jira, mở PR vào `develop`, gắn PR/code/kiểm thử trong description. Trưởng nhóm review và merge; `main` nhận bản ổn định qua PR. [Quy trình](CONTRIBUTING.md), [Jira](docs/jira-workflow.md), [bảo vệ nhánh](.github/rulesets/README.md).
