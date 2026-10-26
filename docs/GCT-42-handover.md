# GCT-42 - Bàn giao cấu trúc dữ liệu bàn cờ và quân cờ (Engine Model)

26/10/2026 • [Jira GCT-42](https://vle2362005.atlassian.net/browse/GCT-42) • Nhánh `GCT-42-engine-model`.

## 1. Đối chiếu tiêu chí hoàn thành

| Tiêu chí của thẻ | Đầu ra thực tế |
|---|---|
| Cấu trúc dữ liệu Quân cờ (Piece) và Tọa độ (Position) | Enum `Side` (Red, Black), `PieceType` (7 loại quân), Value Object `Position` (Cột 0..8, Hàng 0..9 theo chuẩn WXF Xiangqi), `Piece` với định danh chuẩn `^(red|black)-[a-z]+-[1-5]$`. |
| Cấu trúc dữ liệu Bàn cờ (Board) và Cung tướng (BoardGeometry) | `Board` quản lý 90 giao điểm (9 cột x 10 hàng), tối đa 32 quân cờ, đúng 1 Tướng Đỏ và 1 Tướng Đen, tra cứu $O(1)$ theo tọa độ hoặc ID quân cờ. `BoardGeometry` định nghĩa phạm vi Cung tướng và Sông theo chuẩn WXF. |
| Thiết lập bàn cờ ban đầu (BoardInitializer) | `BoardInitializer.CreateInitialBoard()` khởi tạo chuẩn xác toàn bộ 32 quân cờ theo luật thi đấu Cờ Tướng WXF (Pháo hàng 2/7, Tốt hàng 3/6). |
| Trạng thái trận đấu (GameState) và Bất biến | `GameState` quản lý `Board`, `SideToMove`, `MoveNumber`, `Version`. Thao tác `ApplyMove` tạo trạng thái mới không làm thay đổi trạng thái cũ, hỗ trợ hoàn hảo cho rollback, replay và stream. |
| Tuần tự hóa JSON (BoardSerializer) | `BoardSerializer` serialize / deserialize mảng quân cờ dạng JSON khớp 100% với schema hợp đồng giao tiếp của GCT-20. |
| Kiểm thử đơn vị (Unit Tests) | Bổ sung 35 unit tests trong `EngineModelTests` bao phủ tọa độ, tính hợp lệ quân cờ, bất biến bàn cờ, di chuyển, bắt quân, serialize/deserialize JSON. |

## 2. Kết quả kiểm tra xác thực

Kiểm tra toàn bộ quy trình bằng `scripts/verify.ps1`:

| Hạng mục kiểm tra | Kết quả |
|---|---|
| NuGet restore theo lockfile (`--locked-mode`) | Pass, phục hồi sạch toàn bộ dependency theo lockfile |
| Backend Release build | Pass, 0 warning, 0 error |
| Backend xUnit tests | 49 pass, 0 fail, 0 skip (10 test smoke/config + 4 test database model + 35 test engine model) |
| Frontend npm ci + vue-tsc + Vite build | Pass, tạo production bundle sạch (0 lỗ hổng) |
| Frontend Vitest | 4 pass, 0 fail |

## 3. Các thành phần mã nguồn chính

- `src/backend/GameCoTuong.Domain/Enums/Side.cs`: Enum phe chơi (Red, Black).
- `src/backend/GameCoTuong.Domain/Enums/PieceType.cs`: Enum loại quân (General, Advisor, Elephant, Horse, Chariot, Cannon, Soldier).
- `src/backend/GameCoTuong.Domain/ValueObjects/Position.cs`: Value object tọa độ cột 0..8, hàng 0..9.
- `src/backend/GameCoTuong.Domain/ValueObjects/Piece.cs`: Value object quân cờ với ID hợp lệ, tọa độ và màu sắc.
- `src/backend/GameCoTuong.Engine/Model/BoardGeometry.cs`: Định nghĩa hình học bàn cờ (cung tướng Đen 0..2/3..5, cung tướng Đỏ 7..9/3..5, qua sông).
- `src/backend/GameCoTuong.Engine/Model/Board.cs`: Quản lý 90 điểm giao cắt và quân cờ, hỗ trợ `ApplyMove` bất biến.
- `src/backend/GameCoTuong.Engine/Model/BoardInitializer.cs`: Tạo 32 quân cờ ban đầu.
- `src/backend/GameCoTuong.Engine/Model/GameState.cs`: Trạng thái trận đấu (Board, SideToMove, MoveNumber, Version).
- `src/backend/GameCoTuong.Engine/Serialization/BoardSerializer.cs`: Tuần tự hóa JSON tương thích GCT-20.
- `tests/backend/GameCoTuong.Foundation.Tests/EngineModelTests.cs`: 35 unit test case toàn diện.
