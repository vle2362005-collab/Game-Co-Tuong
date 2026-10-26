> Cập nhật 26/10/2026: ruleset [25888788](https://github.com/vle2362005-collab/Game-Co-Tuong/settings/rules/25888788) đã Active cho main/develop, được xác minh lại qua API. Chỉ user trưởng nhóm bypass qua PR. Các đoạn mô tả chưa áp dụng bên dưới là baseline trước khi cấu hình; không còn là trạng thái hiện tại. Chưa thử bằng tài khoản thành viên.

# Game Cờ Tướng

Bộ khung phát triển chung cho nhóm: Vue + TypeScript và ASP.NET Core. API, bộ luật, dữ liệu và kiểm thử nằm trong các project riêng. Hiện chạy được trang khởi tạo và API `/health`; chức năng chơi cờ sẽ được triển khai ở các task tiếp theo.

## Chạy nhanh sau khi clone

Cài **.NET SDK 10.0.403**, **Node.js 24.22.1** và **npm 11.x**. Dùng VS Code hoặc terminal; lệnh dưới chạy được trong PowerShell và Bash. Cần Internet ở lần restore đầu.

Từ thư mục gốc repo, terminal thứ nhất:

```sh
dotnet restore GameCoTuong.sln --locked-mode -m:1
dotnet build GameCoTuong.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet run --project src/backend/GameCoTuong.Api -c Release --no-build --no-restore --launch-profile http
```

Terminal thứ hai, cũng từ thư mục gốc:

```sh
cd src/frontend
npm ci
npm run dev
```

Mở **http://localhost:5173**, bấm **Kiểm tra máy chủ**. API chạy tại **http://localhost:5000/health**. Không cần SQL Server, tài khoản hay JWT key để chạy bộ khung này. Không phải sao chép cấu hình mẫu để chạy mặc định.

## Cấu trúc

| Đường dẫn | Trách nhiệm |
|---|---|
| `src/frontend` | Vue + TypeScript, trang khởi tạo và kiểm tra kết nối |
| `src/backend/GameCoTuong.Api` | Điểm khởi động HTTP, cấu hình, health và CORS |
| `src/backend/GameCoTuong.Application` | Vị trí cho use case và interface lưu trữ |
| `src/backend/GameCoTuong.Domain` | Vị trí cho entity/value object độc lập |
| `src/backend/GameCoTuong.Engine` | Vị trí cho bộ luật thuần, chỉ tham chiếu Domain |
| `src/backend/GameCoTuong.Infrastructure` | Vị trí cho EF Core, adapter dữ liệu và outbox |
| `tests/backend` | Kiểm thử khởi động API và CORS bằng xUnit |
| `src/frontend/tests` | Kiểm thử giao tiếp health bằng Vitest |
| `docs` | Phạm vi, luật và hướng dẫn phát triển |

## Kiểm tra trước khi mở PR

Từ thư mục gốc:

```sh
dotnet restore GameCoTuong.sln --locked-mode -m:1
dotnet build GameCoTuong.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test GameCoTuong.sln -c Release --no-build --no-restore -m:1
npm --prefix src/frontend ci
npm --prefix src/frontend run build
npm --prefix src/frontend test
```

Hoặc chạy `./scripts/verify.sh` trên Bash, `./scripts/verify.ps1` trên PowerShell. Xem [hướng dẫn phát triển](docs/development.md) để đổi cổng, cấu hình, cập nhật lockfile và tạo nhánh. Xem [bàn giao GCT-21](docs/GCT-21-handover.md) để đối chiếu tiêu chí Jira.

## Tài liệu và quy trình nhóm

- [GCT-19 — phạm vi và nghiệm thu](docs/requirements.md), [luật và giới hạn](docs/rules.md).
- GCT-15 và GCT-20 đã có ở nhánh `feature/GCT-15-project-foundation`; tại thời điểm tạo GCT-21, các tài liệu đó chưa được tích hợp vào `main`/`develop`. Xem [kiến trúc GCT-20](https://github.com/vle2362005-collab/Game-Co-Tuong/blob/81f65ca67af35adff8c0dbb4d82e7953a3b9b4e0/docs/architecture.md) và [hợp đồng](https://github.com/vle2362005-collab/Game-Co-Tuong/blob/81f65ca67af35adff8c0dbb4d82e7953a3b9b4e0/docs/contracts.md) tại commit đã bàn giao.
- `main`: bản ổn định; `develop`: nơi nhận thay đổi qua PR; `feature/GCT-<mã>-<nội-dung>`: từng công việc. Sau khi PR GCT-21 được merge, thành viên cập nhật `develop` rồi mới tách nhánh.
- Trưởng nhóm duyệt và merge. Xem [cách làm việc và mở PR](CONTRIBUTING.md), [quy ước Jira](docs/jira-workflow.md) và [rà soát GCT-22](docs/GCT-22-audit.md). [Bộ cấu hình bảo vệ nhánh](.github/rulesets/README.md) đã chuẩn bị nhưng chưa áp dụng trên GitHub. GCT-23 bổ sung CI.
