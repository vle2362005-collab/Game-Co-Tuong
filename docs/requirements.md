# GCT-19 — Phạm vi và tiêu chí nghiệm thu

Ngày: 26/10/2026 · [Jira GCT-19](https://vle2362005.atlassian.net/browse/GCT-19) · Nhánh `feature/GCT-19-requirements`.

Đây là bản yêu cầu để trưởng nhóm review và dùng làm đầu vào triển khai. Việc có tài liệu không có nghĩa các chức năng đã chạy hoặc nhóm đã nghiệm thu. Phạm vi bám theo mô tả và Version trên Jira tại ngày trên; chưa thay đổi phân công, hạn nộp hay Version.

## 1. Phạm vi theo phiên bản

| ID | Chức năng | Mốc | Task triển khai / điều kiện nghiệm thu |
|---|---|---|---|
| FR-01 | Tài khoản: username, email, mật khẩu, đăng nhập/đăng xuất | Bắt buộc V1 | GCT-27–35: chuẩn hóa và unique username/email; băm mật khẩu; chặn phiên sai/hết hạn; không trả mật khẩu, hash hoặc email qua hồ sơ công khai |
| FR-02 | Tạo, tìm bằng mã sáu ký tự, tham gia phòng hai người | Bắt buộc V1 | GCT-36–39, GCT-103: một người một phòng chưa đóng; tranh ghế cuối chỉ một người thành công; mã phòng không trùng phòng hoạt động |
| FR-03 | Sẵn sàng, bắt đầu, rời và đóng phòng | Bắt buộc V1 | GCT-40,44–47: đủ hai người online và ready; chỉ chủ được Start; host Đỏ, guest Đen; Start lặp tạo đúng một trận |
| FR-04 | Bàn cờ, bảy loại quân, bộ luật độc lập | Bắt buộc V1 | GCT-41–43,49–61: 90 giao điểm, 32 quân ban đầu; Đỏ đi trước; chặn sai lượt, tự chiếu và hai Tướng đối mặt; chiếu bí/bí nước đều thua |
| FR-05 | Máy chủ xác nhận và đồng bộ nước đi | Bắt buộc V1 | GCT-73–74,79,81–83,89: requestId chống lặp, version chống ghi trên bàn cũ; không báo thành công trước commit; chỉ người chơi đúng lượt được đi |
| FR-06 | Mất kết nối và nối lại | Bắt buộc V1 | GCT-48,75–76,109–110: tạm dừng đồng hồ trận; tổng ngân sách nối lại mỗi người 60 giây/trận; nối nhiều lần không reset; đúng tài khoản trở lại đúng ghế |
| FR-07 | Dọn phòng và phục hồi backend | Bắt buộc V1 | GCT-77–78: chờ không hoạt động 15 phút, phòng kết thúc tối đa 5 phút; bảo vệ thời gian phục hồi; restart giữ nước đã xác nhận và chờ xác thực lại, không tự xử thắng |
| FR-08 | Chat văn bản trong phòng | Bắt buộc V1 | GCT-63–64,111: đúng thành viên, đúng phòng; 1–500 ký tự sau trim; lưu và tải bù bằng sequence; hiển thị như text |
| FR-09 | Kết quả, đầu hàng và thỏa thuận hòa | Bắt buộc V1 | GCT-73,86–88: kết quả đúng một lần; hòa cần đối thủ chấp thuận offerId trong 30 giây; nước mới/từ chối/hết hạn hủy đề nghị |
| FR-10 | Xem lịch sử của chính người chơi | Bắt buộc V1 | GCT-86,91,108,116: còn bàn cuối, nước, đối thủ, kết quả/lý do sau đóng phòng; không cho đọc trận của người khác |
| FR-11 | Kiểm thử tích hợp, bảo mật và sự cố | Bắt buộc V1 | GCT-93–96,104–107: có dữ liệu đầu vào, kết quả thực và bằng chứng; không dùng trạng thái Done thay cho kiểm thử |
| FR-12 | Chơi với máy: Minimax + Alpha-Beta | Mở rộng V2 | GCT-68–72,90,113: dùng chung luật và sinh nước; giới hạn suy nghĩ, đi đúng luật, có mức khó và lưu kết quả; hợp đồng PvE bổ sung ở task tương ứng |
| FR-13 | Gọi video/audio WebRTC | Mở rộng V2 | GCT-65–67,84–85: xin quyền thiết bị, signaling đúng phòng, ICE/TURN; lỗi gọi không làm hỏng trận |
| FR-14 | Phát lại trận | Mở rộng V2 | GCT-92,114: tái tạo từ bàn ban đầu + chuỗi nước đã lưu, không sửa trận thật |
| FR-15 | Đặc tả luật lặp thế | Mở rộng V2 | GCT-62: ghi trường chiếu/trường tróc và ví dụ; chưa tuyên bố thực thi đầy đủ WXF |
| FR-16 | Đóng gói, báo cáo và bảo vệ | V2 theo Jira hiện có | GCT-97–102: số liệu thực, build tái lập, hướng dẫn, tag/PR và hạn chế; V1 vẫn phải chạy được trước khi nghiệm thu |

AI/video là phần mở rộng đã có trong kế hoạch, không phải chức năng đã làm xong hoặc bị loại khỏi đồ án. Không có yêu cầu hiện tại cho Elo, xếp hạng, khán giả, undo/redo, đăng nhập mạng xã hội hay nhiều backend cùng hoạt động; muốn thêm phải có task và hợp đồng riêng.

## 2. Quyết định nghiệp vụ dùng chung

| Mã | Quyết định |
|---|---|
| BR-01 | Mỗi phòng có một trận. Chơi lại tạo phòng mới; không reset trận cũ. Thành viên lịch sử của trận độc lập ghế hiện tại của phòng. |
| BR-02 | Room có `waiting/playing/finished/closed`. “Sẵn sàng” là điều kiện của waiting; Match có `playing/paused/finished/cancelled`. Chi tiết chuyển trạng thái ở đầu ra GCT-15. |
| BR-03 | Trước trận: mất toàn bộ kết nối hợp lệ thì reset ready của cả hai. Trong 60 giây liên tục có thể xác thực lại; quá hạn guest mất ghế, host làm đóng phòng. |
| BR-04 | Trong trận: lần mất mạng đầu tiên đóng băng đồng hồ chơi và hủy đề nghị hòa. Chỉ thời gian người đó offline mới trừ ngân sách 60 giây. Hai bên online lại trong ngân sách thì tiếp tục, giữ bàn, lượt và phần đồng hồ còn lại. |
| BR-05 | Hạn nối lại dùng giờ server; `now >= deadline` là hết hạn. Một bên hết ngân sách khi đối thủ online hợp lệ thì thua. Nếu cả hai offline, chờ tới hạn còn lại: cả hai hết thì hủy; bên còn ngân sách quay lại đúng hạn thắng. Người quay lại trễ không phục hồi quyền chơi. |
| BR-06 | Hết đồng hồ trước khi xử lý ngắt kết nối vẫn thua do hết giờ. Khi đã paused, đồng hồ không chạy và không chấp nhận Move. Xử lý cùng trận trong transaction để chỉ chốt một kết quả. |
| BR-07 | Một user có nhiều tab/kết nối: chỉ offline khi không còn kết nối đã xác thực và gắn đúng trận; đóng kết nối cũ không làm mất presence của kết nối mới. |
| BR-08 | Restart một backend: nạp snapshot, moves và ngân sách còn lại vào paused. Người đã offline giữ deadline cũ; người trước đó online nhận cửa sổ từ thời điểm khôi phục bằng ngân sách còn lại. Cả hai xác thực lại đúng hạn thì tiếp tục; một bên không phục hồi thì hủy `serverRestart`, không trao thắng tự động. |
| BR-09 | Đồng hồ sau crash dùng checkpoint cuối đã commit, không tự đoán thời điểm crash hay trừ thời gian backend không hoạt động. Đây là giới hạn độ chính xác của bản demo; phải lưu clock/checkpoint trong cùng transaction nước đi. |
| BR-10 | Đề nghị hòa chỉ khi playing PvP; tối đa một đề nghị hiệu lực, 30 giây, đồng hồ vẫn chạy. Đối thủ mới được chấp nhận/từ chối; Move, pause, kết thúc hoặc hết hạn làm mất hiệu lực. |
| BR-11 | Phòng chờ không hoạt động 15 phút được đóng, trừ khi còn cửa sổ phục hồi; phòng finished đóng sau tối đa 5 phút hoặc khi chủ đóng/rời. Dọn phòng không hard-delete trận, nước, receipt hay kết quả. |
| BR-12 | Log có requestId, userId, roomId/matchId và lý do; không chứa mật khẩu, token hoặc connection string. Các thao tác vượt quyền không sửa dữ liệu. |

BR-05, BR-08 và BR-09 làm rõ cách xử lý biên cho GCT-76/78, cần trưởng nhóm kiểm tra khi duyệt thiết kế. Không thay quy tắc mất mạng bằng “mỗi lần được thêm 60 giây”, không tự hủy ngay mọi trận lúc restart.

## 3. Nghiệm thu có thể chạy lại

Các ca sau là **kế hoạch nghiệm thu**, chưa phải kết quả chạy backend. GCT-93/94/95/96 ghi bằng chứng thực khi có ứng dụng.

| Ca | Thao tác | Kết quả phải quan sát được |
|---|---|---|
| AC-01 | Đăng ký đồng thời cùng username hoặc email khác hoa/thường | Chỉ một tài khoản; phản hồi không chứa password/hash |
| AC-02 | Ba user tranh ghế cuối | Phòng có đúng hai người; người thua cuộc nhận ROOM_FULL |
| AC-03 | Guest Start; host Start khi một bên chưa ready/offline | Từ chối, không tạo match |
| AC-04 | Cùng requestId Start hai lần; hai Start khác ID cạnh tranh | Một trận duy nhất, liên kết room-match nhất quán |
| AC-05 | Đi sai quân/lượt, ra ngoài bàn, tự chiếu, lộ Tướng | Không tăng ply, không sửa bàn; có mã lỗi |
| AC-06 | Gửi lại nước đã chấp nhận bằng HTTP rồi Hub | Trả receipt cũ; không thêm nước/event mới |
| AC-07 | Version cũ hoặc bỏ mất sự kiện cuối | Client khóa thao tác, lấy snapshot/cursor rồi đối chiếu |
| AC-08 | Offline 20 giây, về, offline tiếp | Còn 40 giây cho lần sau; clock playing chỉ chạy khi cả hai đã phục hồi |
| AC-09 | Reconnect tại `deadline-1ms` và tại `deadline` | Lần đầu phục hồi; lần sau đã hết hạn, không chiếm lại ghế trận |
| AC-10 | Hai bên offline, lần lượt hết ngân sách | Không tự chọn thắng; hủy cả hai khi hết, giữ lịch sử |
| AC-11 | Restart sau nước đã nhận phản hồi thành công | Nước vẫn có; paused với ngân sách cũ; hai người xác thực lại mới tiếp tục |
| AC-12 | Chỉ một người trở lại sau restart | Hết cửa sổ thì cancelled/serverRestart, winner=null |
| AC-13 | Nhận hòa từ chính người gửi, offer cũ, hoặc sau 30 giây | Từ chối; không tạo kết quả hòa |
| AC-14 | Đối thủ nhận hòa đúng hạn đồng thời với Move/Resign | Transaction thắng quyết định một trạng thái; tác vụ còn lại không ghi kết quả thứ hai |
| AC-15 | Đóng phòng và chạy cleanup lại nhiều lần | Không mất match/moves; người tham gia vẫn đọc được lịch sử |
| AC-16 | DB lỗi trước commit hoặc crash sau commit trước broadcast | Không báo thành công giả; retry/snapshot khôi phục được dữ liệu đã commit |

## 4. Cách dùng và điều kiện đóng GCT-19

GCT-15 dùng BR-01–11 để vẽ lifecycle; GCT-20 dùng các FR/BR để thiết kế DTO, API và sự kiện. GCT-25/26,42,73–79 triển khai theo tài liệu được review. Đổi hành vi phải sửa yêu cầu, lifecycle, contracts và ca nghiệm thu trong cùng PR.

Đầu ra GCT-19 gồm tài liệu này và `rules.md`. Đủ điều kiện review khi có phân loại V1/V2, điều kiện bắt đầu/kết thúc/phục hồi, nguồn luật và ca nghiệm thu. Chỉ trưởng nhóm quyết định nghiệm thu/merge; không ghi “nhóm đã thống nhất” khi chưa có review.
