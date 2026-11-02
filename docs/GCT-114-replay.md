# GCT-92 + GCT-114 — Tải dữ liệu và xem lại từng nước

GCT-92 (tải và dựng dữ liệu replay) cùng GCT-114 (điều khiển xem từng nước) được triển khai chung theo FR-14. Từ lịch sử cá nhân, người chơi mở ván đã kết thúc ở `/replay/{matchId}`. Màn hình gọi `GET /api/v1/matches/{matchId}/history`, dựng vị trí đầu từ `InitialBoardJson` rồi áp dụng chuỗi nước đã lưu theo thứ tự ply. Khi có snapshot theo nước, client đối chiếu snapshot đó với nước đã áp dụng; cuối cùng đối chiếu bàn phát lại với `CurrentBoardJson`. Có thể về đầu, lùi một nước, tiến một nước và tới cuối; giao diện hiển thị lượt hiện tại, nước vừa đi và quân bị ăn.

Replay là chế độ chỉ đọc: màn hình không kết nối SignalR và không gọi API gửi nước, đầu hàng, đề nghị hòa hay reconnect. Endpoint chi tiết chỉ cho người đã tham gia trận. Trước khi hiển thị, client kiểm tra ply/lượt, quân đi, tọa độ và quân bị ăn; tái tạo từ các nước đã lưu; đối chiếu snapshot theo nước nếu có; rồi đối chiếu vị trí cuối với `CurrentBoardJson`. Snapshot từng nước có thể vắng mặt vì trường DB đó không bắt buộc, nhưng bản ghi nước cần thiết không được tự suy đoán: thiếu nước/không khớp bàn cuối, hoặc dữ liệu sai để tái tạo được báo lỗi thay vì tự thêm nước hay quân.

## Kiểm chứng

- `dotnet test tests\backend\GameCoTuong.Foundation.Tests\GameCoTuong.Foundation.Tests.csproj --no-restore --filter FullyQualifiedName~GCT114` — đạt, 1/1. Xác nhận nước ăn quân/snapshot trả cho thành viên, người ngoài bị từ chối, đọc không đổi bàn, kết quả, version hay nước đã lưu.
- `npm.cmd --prefix src\frontend test -- tests/replay.test.ts tests/router.test.ts` — đạt, 14/14. Bao gồm vị trí đầu/cuối, bắt quân, lượt sau mỗi nước, dữ liệu sai/thiếu, snapshot tùy chọn và yêu cầu đăng nhập.
- `npm.cmd --prefix src\frontend test` — đạt, 62/62.
- `npm.cmd --prefix src\frontend run typecheck`, `npm.cmd --prefix src\frontend run lint` và `npm.cmd --prefix src\frontend run build` — đều đạt.
- Toàn bộ backend Foundation: 278/279; test ngoài phạm vi `XiangqiRulesTests.Minimax_stops_when_cancelled_without_changing_the_board` không ném cancellation khi chạy cả suite. Chạy riêng test đó đạt 1/1; GCT-114 đạt khi chạy riêng.
