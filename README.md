# Game Cờ Tướng

Dự án nhóm dùng Vue, ASP.NET Core, EF Core, SQL Server và SignalR. Thư mục hiện tại chứa bộ yêu cầu và thiết kế; ứng dụng chạy được do các task triển khai tiếp theo thực hiện.

## Tài liệu theo Jira

| Task | Đầu ra |
|---|---|
| [GCT-19](https://vle2362005.atlassian.net/browse/GCT-19) — Phạm vi và luật | [Yêu cầu, V1/V2 và nghiệm thu](docs/requirements.md), [luật và giới hạn](docs/rules.md) |
| [GCT-15](https://vle2362005.atlassian.net/browse/GCT-15) — Vòng đời | [Sơ đồ và bảng điều kiện](docs/lifecycle.md), [sơ đồ phòng](docs/diagrams/room-lifecycle.mmd), [sơ đồ trận](docs/diagrams/match-lifecycle.mmd) |
| [GCT-20](https://vle2362005.atlassian.net/browse/GCT-20) — Kiến trúc/API | [Kiến trúc](docs/architecture.md), [hợp đồng REST/SignalR](docs/contracts.md), [bàn giao và kiểm tra](docs/GCT-20-handover.md) |

V1 gồm PvP, tài khoản, phòng hai người, luật cơ bản, chat, phục hồi, đầu hàng/hòa và lịch sử. AI Minimax/Alpha-Beta, WebRTC và replay thuộc V2 theo Jira. Chưa tuyên bố áp dụng đầy đủ phân xử lặp thế WXF.

## Hợp đồng để frontend/backend dùng chung

- [openapi.json](contracts/openapi.json): 23 thao tác HTTP, OpenAPI 3.1, đã nhúng schema để nhập trực tiếp.
- [schemas.json](contracts/schemas.json): 67 mô hình nguồn, JSON Schema Draft 2020-12.
- [signalr.json](contracts/signalr.json): 6 phương thức Hub và 3 loại sự kiện.
- [examples.json](contracts/examples.json): 34 mẫu hợp lệ, 28 mẫu cố ý sai.

## Kiểm tra trên máy thành viên

Cần Python 3.10 trở lên. Từ thư mục gốc repo trong PowerShell hoặc terminal:

```sh
python -m pip install -r requirements-contracts.txt
python scripts/validate_contracts.py
python -m unittest discover -s tests -v
```

Khi sửa schemas.json, chạy thêm `python scripts/validate_contracts.py --write-openapi`, cập nhật ví dụ/tài liệu rồi kiểm tra lại. Đã có 12 ca hồi quy cho hợp đồng; đây chưa phải kiểm thử backend hay bộ luật cờ.

## Nhánh và review

`feature/GCT-19-requirements` → `feature/GCT-15-project-foundation` → `feature/GCT-20-contracts` là chuỗi phụ thuộc. Duyệt theo thứ tự **GCT-19, GCT-15, GCT-20**; sau khi merge phần trước, chuyển base của PR tiếp theo về main và kiểm tra diff. Xem lưu ý squash/rebase trong tài liệu bàn giao.

Nhánh, commit và PR mang mã Jira. Trưởng nhóm `@vle2362005-collab` là CODEOWNER hiện có. Chưa merge hoặc xác nhận nghiệm thu thì giữ Jira In Progress; không coi việc kéo Done là bằng chứng hoàn thành. Khung ứng dụng, nhánh develop và phiên bản công cụ do GCT-21 thực hiện.
