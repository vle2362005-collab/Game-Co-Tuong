# Sprint 4 — phục hồi, lịch sử và kết thúc trận

Base: `develop` sau PR #53, #57 và #58. Tái sử dụng phần đã gộp; chỉ bổ sung lỗi và bằng chứng còn thiếu cho tám thẻ.

| Thẻ | Bàn giao và kiểm chứng |
|---|---|
| GCT-116 | Lặp nước/kết thúc không tạo bản ghi trùng; lịch sử, bàn cuối và quyền đọc giữ nguyên sau đóng thủ công hoặc cleanup. |
| GCT-78 | Restart cả trận đang chơi và trận đã mất mạng đều chờ xác thực lại; giữ nước/ngân sách/deadline, thiếu người thì hủy `serverRestart`, không trao thắng. |
| GCT-79 | Hai người cùng tham gia, bắt đầu lặp, bắt đầu/đóng, đóng/cleanup và reconnect/hết hạn không tạo ghế/trận trùng hoặc hồi sinh trận đã hủy. |
| GCT-80 | Log có mã truy vết/yêu cầu/phòng/trận/trạng thái; lọc nhiều token, secret có khoảng trắng, TURN credential và nội dung SDP/ICE. |
| GCT-110 | Panel tạm dừng, bên mất mạng và số giây theo thời gian máy chủ; khóa bàn, đồng bộ lại cùng trận; đồng hồ về 0 không tự chọn kết quả. |
| GCT-83 | Gợi ý từ luật server, đánh dấu chọn/nước cuối/chiếu, xóa theo snapshot mới; kiểm thử API/SignalR và render hai chiều bàn. |
| GCT-16 | Hủy xác nhận không gửi request; xác nhận gửi `confirmed=true`; giao diện có gửi/nhận/từ chối hòa và thời hạn. |
| GCT-87 | Chấp nhận/từ chối đúng offer ID; chặn đề nghị cũ/của mình/PvE; giữ kết quả duy nhất khi lệnh cạnh tranh. |

Cleanup chạy cùng worker phục hồi của cấu hình SQL Server một backend: phòng chờ không hoạt động 15 phút, phòng kết thúc 5 phút; bỏ qua trận đang chơi/tạm dừng và cửa sổ phục hồi. Chỉ đóng phòng, giải phóng ghế và phát `RoomStateChanged`, `LobbyChanged`, `CallEnded`; không xóa trận/nước/receipt. Ready, chat và rời phòng cập nhật thời điểm hoạt động.

## Kiểm thử

- .NET SDK 10.0.403, restore `--locked-mode`: đạt.
- Toàn bộ backend Release: **284/284 đạt** trên SQLite kiểm thử riêng.
- Frontend: **64/64 đạt**; lint, typecheck và build production đạt.
- Các ca mới nằm trong `GameFlowTests.cs`, `LoggingAndSanitizationTests.cs`, nhánh tự dọn của `GCT116_closed_room_preserves_match_result_moves_and_player_access`, và `src/frontend/tests/sprint4Controls.test.ts`.

```sh
dotnet test GameCoTuong.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
npm --prefix src/frontend run lint
npm --prefix src/frontend run build
npm --prefix src/frontend test
```

Không đổi schema/migration, SDK hay lockfile. Chưa chạy SQL Server thật trong lượt này; worker tự động được tắt ở môi trường Testing/SQLite, các thao tác phục hồi và cleanup được gọi trực tiếp trong kiểm thử tích hợp. Không bổ sung tuyên bố kiểm thử camera vật lý, hai mạng hay TURN thật; giới hạn video vẫn theo `docs/sprint3.md`.

Giữ Jira In Progress / `review-pending` tới khi trưởng nhóm duyệt và gộp vào `develop`.
