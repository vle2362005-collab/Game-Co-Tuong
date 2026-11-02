# Kết quả và nghiệm thu — GCT-94/95/96/97/17/98

Ngày 03/11/2026, base `7fb3b66` cộng thay đổi closeout của PR này. .NET 10.0.403 / runtime 10.0.14, Node 24.22.1, SQLite riêng; không sử dụng DB của nhóm.

| Thẻ / nhóm | Kết quả đã kiểm tra | Minh chứng |
|---|---|---|
| GCT-94 PvP | Hai tài khoản đăng ký/đăng nhập, phòng/ready/start, đi/ăn quân, pause/reconnect, đầu hàng, đóng phòng và lịch sử: đạt qua HTTP in-process. | `CoreApiAcceptanceTests.GCT94_pvp_capture_reconnect_finish_close_and_read_only_replay` |
| GCT-95 phân quyền/lạm dụng | Sai luật/lượt/version, lệnh trùng/đồng thời, giả danh, outsider API/Hub, chat rate limit và cô lập phòng: đạt tự động. | `CoreApiAcceptanceTests`, `AuthorizationAndRoomAccessTests`, `GameFlowTests` |
| GCT-96 sự cố | Lỗi lưu rollback, giữ nước đã commit, restart chờ xác thực, quá hạn/hai bên offline và reconnect/cleanup cạnh tranh: đạt tự động. | `GameFlowTests.Save_failure_rolls_back_move_board_and_turn`, nhóm Restart/Disconnect/GCT78/GCT79 |
| Replay GCT-114/92 | Tiến/lùi/đầu/cuối, ăn quân/lượt, chỉ đọc; từ chối dữ liệu thiếu/sai: đạt unit; quyền đọc và snapshot cuối đạt API. | `src/frontend/tests/replay.test.ts`, GCT-94/GCT-116 |
| Tài khoản/phòng/luật/AI/kết quả | Bộ hồi quy backend đạt **290/290**; frontend đạt **79/79**, lint/typecheck/build đạt. | [Tóm tắt máy đọc](verification.json), [bộ kiểm tra](../../scripts/verify.sh) |
| Video | Test media giả lập, lifecycle, lỗi quyền/thiết bị, cô lập tín hiệu: đạt trong bộ frontend/backend. | `videoCall.test.ts`, `docs/GCT-85-communication-test-results.md` |
| GCT-99 bản publish | Script Bash tạo 116 file gồm API/web/SQL idempotent/mẫu cấu hình/manifest; smoke HTTP bản publish đạt đăng nhập/phòng/nước lặp/kết thúc/lịch sử trên SQLite tạm. | [Smoke](smoke.json), `scripts/package-demo.sh`, `scripts/smoke-demo.mjs`; PowerShell được cung cấp, chưa chạy trên Windows trong lượt này. |
| GCT-98 nghiệm thu người dùng | Có ca và bằng chứng tự động; giảng viên/thành viên chưa ký nghiệm thu trên thiết bị thực trong lượt này. | Kịch bản ở [demo](demo.md) |

## GCT-97 — số liệu thực

[Raw JSON](performance.json): Ubuntu 24.04.3 x64, 8 CPU logic. 10 phòng trong một tiến trình, 100 request `GameService.Move` có queue/DbContext/JIT: **p50 109.96 ms, p95 170.28 ms**. Đo trong tiến trình với SQLite, không gồm HTTP/LAN/SignalR/browser; không dùng số này để kết luận hai máy cập nhật dưới 500 ms.

| AI (5 mẫu/mức) | p50 | p95 |
|---|---:|---:|
| Dễ | 48.58 ms | 53.65 ms |
| Vừa | 270.20 ms | 330.56 ms |
| Khó | 1500.85 ms | 1503.63 ms |

Đây là thời gian search trên cùng bàn ban đầu, không phải kiểm tra mọi thế cờ. Không biến ngưỡng tốc độ thành assertion dễ dao động. Tái đo: chạy `scripts/verify.*`, hoặc lọc `FullyQualifiedName~GCT97` và dùng `node scripts/extract-performance.mjs <trx> <json>`.

## Phần chưa nghiệm thu và hồi quy

- Chưa đo độ trễ LAN end-to-end trên hai thiết bị; chưa chạy SQL Server thật/backup-restore thật ở lượt này.
- Video hai mạng, TURN relay và camera vật lý chưa có bằng chứng mới. Không đánh dấu đạt thay bằng test giả lập.
- Phân xử đầy đủ lặp thế/trường chiếu/trường tróc WXF chưa hỗ trợ, xem `docs/repetition-scope.md`.
- Các lỗi đã sửa: đầu hàng thiếu `confirmed=true`, chấp nhận hòa gọi nhầm API, restart trong lúc disconnect có thể trao thắng nhầm; hồi quy ở `sprint4Controls.test.ts` và `GameFlowTests.GCT78_*`.
- Các mục chưa kiểm tra trên giữ trạng thái chờ môi trường/nghiệm thu; toàn bộ thẻ của PR chờ review/merge, không tự đổi Done.
