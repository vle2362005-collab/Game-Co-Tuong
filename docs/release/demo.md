# Kịch bản và minh chứng — GCT-101

Chuẩn bị hai cửa sổ hoặc hai thiết bị, hai tài khoản tự đăng ký; SQL Server đã migrate, cùng commit từ manifest. Chạy trước `scripts/verify.*`, mở sẵn PR/Checks và các tài liệu ở thư mục này. Không dùng tài khoản/mật khẩu thật trong ảnh hoặc video bảo vệ.

| Bước | Thao tác và kết quả cần thấy | Minh chứng |
|---|---|---|
| 1 | Đăng nhập; chủ tạo phòng, khách nhập mã; cả hai ready rồi start. | GCT-94; ảnh phòng hai người. |
| 2 | Chọn quân/gợi ý; thử nước sai, đi đúng lượt, ăn quân và thế chiếu mẫu. | GCT-61/83/94/115; `CompleteRulesTests` và `CoreApiAcceptanceTests`. |
| 3 | Ngắt mạng một bên; bàn khóa, hiện hạn; nối lại cùng trận. | GCT-78/110 và `GameFlowTests`; không chờ hết hạn rồi tự kết luận thắng. |
| 4 | Gửi hòa, đối thủ từ chối; gửi lại và chấp nhận, hoặc hủy/xác nhận đầu hàng ở ván khác. | GCT-16/87, `sprint4Controls.test.ts`. |
| 5 | Đóng phòng, mở lịch sử, chọn Xem lại; đầu/lùi/tiến/cuối khớp lượt và quân ăn. | GCT-114/92/116; `replay.test.ts`. |
| 6 | Từ sảnh chơi máy Dễ/Vừa/Khó; mở bảng p50/p95, giải thích Minimax/Alpha-Beta. | GCT-12/71/72/90/97; `performance.json`. |
| 7 | Chat; Gọi/Nhận video, mic/camera/kết thúc khi môi trường đã xác nhận. | GCT-85 và tài liệu Sprint 3; nếu chưa có TURN/hai mạng, trình bày giới hạn và test giả lập, không nói đã nghiệm thu cuộc gọi thật. |
| 8 | Mở PR/Checks, hướng dẫn cài đặt, manifest và giới hạn hiện tại; thành viên giải thích code phần mình theo Jira/commit. | `verification.md`, `analysis-design.md`, `handover.md`. |

Demo dự phòng: giữ package đã build và DB demo đã backup/restore thử trước buổi bảo vệ; nếu mạng/TURN lỗi, dùng chức năng cờ/local và mở báo cáo tự động, công khai phần chưa kiểm tra. Hình/video chỉ được gọi là minh chứng thực tế sau khi nhóm tự ghi đúng phiên bản, thiết bị và môi trường; PR này không tạo video nghiệm thu giả.
