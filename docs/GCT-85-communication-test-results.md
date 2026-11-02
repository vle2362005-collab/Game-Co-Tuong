# GCT-85 — Kiểm tra trò chuyện và gọi video

## Môi trường và kết quả

| Ca kiểm tra | Môi trường | Kết quả |
|---|---|---|
| Chat được định tuyến đúng room; người ngoài không gửi/đọc được | Backend xUnit, `AuthorizationAndRoomAccessTests` | Đạt; sự kiện chat chỉ phát vào group của room đích, outsider bị chặn. |
| Nội dung HTML và input lỗi | Backend xUnit, `GameFlowTests.Chat_is_idempotent_limited_and_members_only` | Đạt; `<script>literal text</script>` được giữ nguyên như nội dung, tin rỗng và quá 500 ký tự trả `VALIDATION_ERROR`. Browser check trước đó xác nhận HTML hiển thị như văn bản, không thực thi. |
| Gửi chat quá nhanh | Backend xUnit, SQLite tạm | Đạt; tin thứ sáu trong cửa sổ 10 giây trả `RATE_LIMITED`. |
| Chat lỗi không đổi trạng thái/kết quả trận hoặc chặn nước hợp lệ | Backend xUnit, SQLite tạm | Đạt; sau `RATE_LIMITED`, Đỏ vẫn đi được; Đen đầu hàng và kết quả vẫn là Đỏ thắng do đầu hàng. |
| Người dùng cho phép thiết bị; từ chối quyền | Frontend Vitest, media/peer giả lập | Đạt; cho phép mở media và trả lời offer; từ chối quyền hiện thông báo và cho phép thử lại. |
| Không có thiết bị hoặc thiết bị đang bận | Frontend Vitest, `NotFoundError`/`NotReadableError` giả lập | Đạt; báo lỗi tương ứng, dừng thiết lập cuộc gọi và không giữ stream. |
| Peer connection lỗi, đối phương kết thúc, kết thúc cục bộ hoặc quyền trả về sau khi hủy | Frontend Vitest, peer/media giả lập | Đạt; báo lỗi kết nối, đóng peer và dừng tất cả track; hangup từ xa và local dọn state/media. |
| Thiết bị thực, hai mạng khác nhau và TURN relay | Chưa có thiết bị/mạng thứ hai hoặc TURN endpoint/credential được cấp cho lần chạy này | Chưa kiểm tra. Không dùng kiểm thử localhost/loopback để khẳng định cuộc gọi xuyên mạng. |
| Mất kết nối rồi nối lại và rời phòng trên thiết bị thực | Không chạy browser E2E trong lần kiểm tra này | Chưa kiểm tra lại. Browser E2E localhost với thiết bị giả lập đã được ghi riêng trong [kết quả Sprint 3](sprint3-verification.md). |

## Lệnh đã chạy

- `dotnet test tests\backend\GameCoTuong.Foundation.Tests\GameCoTuong.Foundation.Tests.csproj --no-restore --filter "FullyQualifiedName~GameHub_SendRoomMessage_isolates_event_to_target_room_and_blocks_outsider|FullyQualifiedName~GameFlowTests.Chat"` — đạt, 3/3.
- `dotnet test tests\backend\GameCoTuong.Foundation.Tests\GameCoTuong.Foundation.Tests.csproj --no-restore --filter "FullyQualifiedName~GameFlowTests"` — đạt, 27/27.
- `npm.cmd --prefix src\frontend test -- tests/videoCall.test.ts` — đạt, 12/12.
- `npm.cmd --prefix src\frontend test` — đạt, 58/58.
- `npm.cmd --prefix src\frontend run lint` — đạt.
- `npm.cmd --prefix src\frontend run typecheck` — đạt.
- `npm.cmd --prefix src\frontend run build` — đạt.

Các lỗi media và peer trong unit test được giả lập; không phải kết quả thử camera vật lý. Cần ghi lại hệ điều hành/trình duyệt, loại thiết bị, hai mạng, TURN provider/region, trạng thái kết nối và ICE candidate pair từ `RTCPeerConnection.getStats()` khi tổ chức được lượt kiểm tra thực địa. Không lưu TURN credential, token hoặc SDP nhạy cảm vào log/repository.
