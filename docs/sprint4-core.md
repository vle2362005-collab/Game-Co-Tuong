# S4 — Nghiệm thu các thẻ đầu đến GCT-82

Phạm vi đúng 9 thẻ theo thứ tự Sprint: **GCT-6, GCT-31, GCT-103, GCT-47, GCT-10, GCT-61, GCT-9, GCT-115, GCT-82**. Base là `develop` sau PR #49. Nhánh: `feature/GCT-82-s4-core-acceptance`.

## Thay đổi

Snapshot của máy chủ bổ sung `legalMoves` và `lastMove` (quân đi/ăn, tọa độ, requestId, ply). Giao diện dùng cùng snapshot để vẽ quân, lượt, cảnh báo chiếu, gợi ý nước hợp lệ và đánh dấu nước vừa đi. Gợi ý không thay thế việc kiểm tra nước trên máy chủ.

Client bỏ sự kiện sai trận, trùng hoặc cũ. Khi thiếu phiên bản, client đọc lại API và giữ bàn đã xác nhận nếu đọc lỗi. Đăng ký nhận sự kiện trước khi tải dữ liệu, đọc lại sau khi gia nhập nhóm; kiểm tra mỗi 15 giây và khi cửa sổ được focus để khôi phục cả sự kiện cuối bị mất. Rời màn hình gỡ listener/timer và bỏ kết quả đọc đến muộn. Đề nghị hòa tăng version khi thay đổi trạng thái để không bị coi là sự kiện trùng.

## Đối chiếu thẻ

| Thẻ | Nội dung nghiệm thu | Minh chứng chính |
|---|---|---|
| GCT-6 | Đăng ký, đăng nhập và phản hồi lỗi | `CoreApiAcceptanceTests.GCT31_...`, browser |
| GCT-31 | Trùng/đồng thời, token sai/hết hạn/thu hồi, phản hồi và log | `CoreApiAcceptanceTests.cs` |
| GCT-103 | Tạo, tìm, tham gia, rời/đóng phòng | `CoreApiAcceptanceTests.GCT47_...`, browser |
| GCT-47 | Hai tài khoản tới phòng chờ; ghế và nút theo vai trò | `sprint4-core-browser-check.cjs` |
| GCT-10 | Nước hợp lệ, cảnh báo/gợi ý thoát chiếu, kết thúc khi hết nước | `GameService.MatchView`, `GameView.vue`, browser |
| GCT-61 | Bảy loại quân ở cả hai màu, vật cản, tự chiếu, chiếu đôi, bí/hết nước | `CompleteRulesTests.cs` — 88 ca mới |
| GCT-9 | Đi quân/ăn quân và xem cùng bàn, lượt, nước vừa đi | `GameView.vue`, `XiangqiBoard.vue`, browser |
| GCT-115 | Sai lượt/người/phiên bản/tọa độ, lặp/đồng thời, lỗi ghi | `CoreApiAcceptanceTests.GCT115_...`, browser trigger |
| GCT-82 | Đồng bộ snapshot, bỏ trùng/cũ, khôi phục khi thiếu, gỡ listener | `useMatchSync.ts`, `matchSync.test.ts`, browser fault injection |

Các chức năng tài khoản/phòng đã có trong S3 được kiểm chứng bằng API thực tế và CSDL tạm, không tạo thêm bản triển khai song song.

## Kết quả

- Backend Release: **238/238 đạt**, không skip; gồm 94 ca mới (88 bộ luật, 6 API).
- Frontend: **50/50 đạt**, gồm 7 ca mới cho đồng bộ; lint/typecheck/production build đạt.
- Browser S4: **7 nhóm luồng đạt**, có hai lần ăn quân, bỏ sự kiện SignalR thật ở biên WebSocket, sự kiện trùng/cũ/sai trận, quay lại màn hình, thoát chiếu và chiếu bí.
- Lỗi ghi được tạo bằng SQLite trigger trong CSDL tạm: không có sự kiện thành công, cả hai bàn không đổi; gửi lại cùng requestId chỉ lưu một lần.
- Browser S3 regression: **7 nhóm luồng đạt**, gồm chat, video/âm thanh bằng thiết bị giả lập, bật/tắt mic/camera và kết thúc; giao diện 390px không tràn ngang.

Thế chiếu và chiếu bí trong browser được nạp vào **CSDL kiểm thử tạm**. Script không có endpoint seed/debug trong ứng dụng. Các bộ kiểm thử không truy cập CSDL thật của nhóm. Video được thử trên localhost với thiết bị giả lập; cấu hình khác mạng vẫn thuộc các thẻ video S4 phía sau.

## Chạy

Cách mở ứng dụng theo [hướng dẫn S3](sprint3.md). Kiểm thử:

```sh
dotnet test GameCoTuong.sln -c Release
npm --prefix src/frontend run lint
npm --prefix src/frontend test
npm --prefix src/frontend run build
node scripts/sprint4-core-browser-check.cjs
```

Browser script cần Node 24, Playwright và Chromium; tự khởi động API Release/Vite, tạo tài khoản và SQLite tạm, rồi dừng các tiến trình đã tạo. Có thể chỉ định `DOTNET_PATH`, `PLAYWRIGHT_MODULE`, `CHROMIUM_PATH`; ảnh/JSON kết quả vào `artifacts/sprint4-core` và không commit.

Bàn giao bằng PR vào `develop`, để trưởng nhóm review/merge. Các thẻ giữ In Progress trong thời gian chờ duyệt. Không sửa lịch, thứ tự hoặc các thẻ sau GCT-82.
