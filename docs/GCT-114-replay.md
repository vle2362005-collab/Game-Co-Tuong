# GCT-114 — Xem lại trận theo từng nước đi

Từ lịch sử cá nhân, người chơi mở ván đã kết thúc ở `/replay/{matchId}`. Màn hình gọi `GET /api/v1/matches/{matchId}/history`, dựng vị trí đầu từ `InitialBoardJson` rồi áp dụng chuỗi nước đã lưu theo thứ tự. Khi có snapshot theo nước, client đối chiếu snapshot đó với nước đã áp dụng; cuối cùng đối chiếu bàn phát lại với `CurrentBoardJson`. Có thể về đầu, lùi một nước, tiến một nước và tới cuối; giao diện hiển thị lượt hiện tại, nước vừa đi và quân bị ăn.

Replay là chế độ chỉ đọc: màn hình không kết nối SignalR và không gọi API gửi nước, đầu hàng, đề nghị hòa hay reconnect. Endpoint chi tiết chỉ cho người đã tham gia trận. Trước khi hiển thị, client kiểm tra ply/lượt, tái tạo từ nước đã lưu, đối chiếu snapshot theo nước nếu có và đối chiếu vị trí cuối với `CurrentBoardJson`; dữ liệu sai hoặc thiếu để tái tạo được báo lỗi thay vì hiển thị thành công giả.

## Kiểm chứng

- `dotnet test tests\backend\GameCoTuong.Foundation.Tests\GameCoTuong.Foundation.Tests.csproj --no-restore --filter FullyQualifiedName~GCT114` — đạt, 1/1. Xác nhận nước ăn quân/snapshot trả cho thành viên, người ngoài bị từ chối, đọc không đổi bàn, kết quả, version hay nước đã lưu.
- `npm.cmd --prefix src\frontend test -- tests/replay.test.ts tests/router.test.ts` — đạt, 13/13. Bao gồm vị trí đầu/cuối, bắt quân, lượt sau mỗi nước, dữ liệu sai, thiếu snapshot và yêu cầu đăng nhập.
- `npm.cmd --prefix src\frontend test` — đạt, 61/61.
- `npm.cmd --prefix src\frontend run typecheck`, `npm.cmd --prefix src\frontend run lint` và `npm.cmd --prefix src\frontend run build` — đều đạt.
- Toàn bộ backend Foundation: 278/279; test ngoài phạm vi `XiangqiRulesTests.Minimax_stops_when_cancelled_without_changing_the_board` không ném cancellation khi chạy cả suite. Chạy riêng test đó đạt 1/1; GCT-114 đạt khi chạy riêng.
