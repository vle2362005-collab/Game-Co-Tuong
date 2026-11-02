# Phân tích và thiết kế — GCT-100

## Sản phẩm thực tế

Web cờ Tướng hai người: tài khoản/JWT, sảnh và phòng hai ghế, ready/start, nước đi có version/requestId, chat/video, phục hồi kết nối, đầu hàng/hòa và lịch sử/phát lại. PvE dùng cùng luật và lưu trận, người Đỏ/máy Đen. Phạm vi/ca nghiệm thu: [requirements](../requirements.md); ERD và khóa/index: [database/erd](../database/erd.md).

## Kiến trúc và dữ liệu

Vue/TypeScript gọi REST và SignalR. `Api/Endpoints` / `Hubs` kiểm tra danh tính rồi dùng `Infrastructure/Game/GameService`; service ghi qua EF Core `GameDbContext` trong transaction serializable. `Application/Game` chứa hợp đồng; `Domain` chứa Room/Membership/Match/Move/Player/Receipt; `Engine` chứa Board, luật và AI thuần C#. Không mô tả một tầng repository hoặc outbox hoàn chỉnh nếu code đang truy cập DbContext trực tiếp.

Phòng: waiting → playing → finished → closed; trận có playing/paused/finished/cancelled. Hạn phục hồi lấy từ server, ngân sách tổng 60 giây/người; restart cần xác thực lại, thiếu người thì hủy. Cleanup đóng phòng và giải phóng ghế, giữ match/moves/receipt. Bản demo dùng một backend; không cam kết mở rộng nhiều node chỉ từ semaphore trong tiến trình.

## Luật và thuật toán

- `XiangqiRules`: sinh nước theo từng quân, cung/sông/cản chân Mã/cản mắt Tượng/ngòi Pháo; thử bàn sau nước và loại tự chiếu/lộ Tướng. Không còn nước hợp lệ thì thua theo bộ luật demo. Server quyết định, client chỉ hiển thị gợi ý.
- Đánh giá Minimax: `E = Σ giá trị quân bên mình − Σ giá trị quân đối thủ`; thêm 100 cho Tốt qua sông và thưởng gần trung tâm `4 − |4 − cột|` cho quân không phải Tướng. Giá trị cụ thể nằm trong `Engine/AI/Minimax.cs`; thế thắng dùng điểm mate.
- Alpha-Beta cập nhật biên và bỏ nhánh không cải thiện kết quả; ưu tiên nước chiếu/ăn quân. Tìm sâu dần 1..3, giữ lời giải ở độ sâu hoàn tất; ngân sách Dễ/Vừa/Khó 300/900/1500 ms. Lượt máy kiểm tra lại version/bàn/trạng thái trước khi commit để không áp dụng kết quả cũ.
- Replay duyệt ply liên tục từ bàn ban đầu, đối chiếu quân nguồn/đích/ăn quân và từng snapshot, rồi so bàn cuối và tổng số nước. Không tự thêm nước hoặc gọi engine/POST khi xem lại.

## Kiểm chứng và đóng góp

[Kết quả/hạn chế](verification.md), [số đo](performance.json), [hướng dẫn](runbook.md), [kịch bản](demo.md). Nguồn phân tích là code các đường dẫn trên và `docs/rules.md`, `docs/database/erd.md`; không suy diễn chức năng từ trạng thái Jira.

Đóng góp đối chiếu bằng assignee Jira cùng commit/PR, không gán tên thành viên chưa xác minh. Lịch sử base `7fb3b66` (`git shortlog -sn origin/develop`) có tác giả Git lnqv23 (38 commit), trantri1206 (19), Admin (12), TienNam245 (2); số commit không đại diện khối lượng/điểm đóng góp. PR #53: lịch sử/phòng/reconnect; #57: AI; #58–59: video và kiểm thử; #60: giới hạn luật lặp; #62: phục hồi, cleanup và điều khiển trận; PR này: replay, tổng hợp QA/hiệu năng và bàn giao.

Chưa có nghiệm thu SQL Server/LAN/TURN thực ở lượt này và chưa hỗ trợ phân xử WXF đầy đủ. Các giới hạn là việc còn lại, không được ghi là đã đạt.
