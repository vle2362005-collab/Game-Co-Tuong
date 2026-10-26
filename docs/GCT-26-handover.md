# GCT-26 — Bàn giao tạo bảng dữ liệu bằng EF Core

26/10/2026 · [Jira GCT-26](https://vle2362005.atlassian.net/browse/GCT-26) · Nhánh `GCT-26-ef-migrations`.

## 1. Đối chiếu tiêu chí hoàn thành

| Tiêu chí của thẻ | Đầu ra thực tế |
|---|---|
| Ánh xạ bảng, quan hệ và chỉ mục theo ERD | 10 entity trong `GameCoTuong.Domain/Entities`, cấu hình chi tiết Fluent API trong `GameDbContext` (`GameCoTuong.Infrastructure`). |
| Ràng buộc không xóa dây chuyền (Cascade) | Tất cả các Foreign Keys được cấu hình `DeleteBehavior.Restrict`, bảo vệ lịch sử trận đấu và nước đi khi phòng bị đóng hoặc thành viên rời. |
| Chỉ mục duy nhất và lọc | Khóa duy nhất cho `NormalizedUsername`, `NormalizedEmail`, `(MatchId, Ply)`, `(MatchId, RequestId)`, `(UserId, RequestId)`, `(RoomId, Sequence)`, và Filtered Index cho `Room.Code` (`Status <> 'closed'`). |
| Migration chạy được trên database trắng | Migration `InitialCreate` tạo sạch 10 bảng nghiệp vụ, các ràng buộc và chỉ mục. |
| Dữ liệu demo riêng, không chứa tài khoản thật | Seed 2 tài khoản mẫu `kythu_do` và `kythu_den` với mật khẩu giả lập cho môi trường test/dev. |
| Hướng dẫn tạo mới và nâng cấp CSDL | Tài liệu hướng dẫn sử dụng `dotnet ef database update` hoặc script migration. |

## 2. Kết quả kiểm tra xác thực

Kiểm tra toàn bộ quy trình bằng `scripts/verify.ps1`:

| Hạng mục kiểm tra | Kết quả |
|---|---|
| NuGet restore theo lockfile (`--locked-mode`) | Pass, 6 project phục hồi hợp lệ theo lockfile |
| Backend Release build | Pass, 0 warning, 0 error |
| Backend xUnit tests | 14 pass, 0 fail, 0 skip (bổ sung 4 unit tests kiểm tra model DbContext, metadata, foreign key behavior, unique indexes và demo seeds) |
| Frontend npm ci + vue-tsc + Vite build | Pass, tạo production bundle sạch |
| Frontend Vitest | 4 pass, 0 fail |

## 3. Hướng dẫn chạy Migration & Cập nhật CSDL

### Cách 1: Sử dụng .NET CLI (dotnet-ef)
Từ thư mục gốc dự án:
```powershell
dotnet ef database update --project src/backend/GameCoTuong.Infrastructure --startup-project src/backend/GameCoTuong.Api
```

### Cách 2: Sử dụng SQL Script đã tạo
Nếu không cài đặt công cụ `dotnet-ef`, có thể chạy trực tiếp file script T-SQL tương đương tại [docs/database/schema.sql](database/schema.sql):
```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i "docs/database/schema.sql"
```
