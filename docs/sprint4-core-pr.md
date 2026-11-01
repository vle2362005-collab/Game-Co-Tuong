Hoàn thiện và nghiệm thu 9 thẻ đầu Sprint S4 đến GCT-82: tài khoản, phòng chờ, bộ luật, nước đi trực tuyến và đồng bộ bàn cờ. Giao diện đánh dấu nước vừa đi, gợi ý nước hợp lệ/cảnh báo chiếu từ snapshot máy chủ; bỏ sự kiện trùng/cũ/sai trận, đọc lại khi thiếu dữ liệu và dọn listener/timer khi rời trang.

Jira: GCT-6, GCT-31, GCT-103, GCT-47, GCT-10, GCT-61, GCT-9, GCT-115, GCT-82.

Kiểm tra: 238 backend Release, 50 frontend; lint/typecheck/build đạt. Browser S4 đạt 7 nhóm luồng, gồm capture, event loss/duplicate, lỗi SQLite thực tế và chiếu bí; browser regression S3 đạt 7 nhóm luồng, gồm chat/video với thiết bị giả lập.

Cùng một version chứa quân, lượt, chiếu, legalMoves và lastMove. Thay đổi trạng thái đề nghị hòa cũng tăng version. API từ chối mã quân rỗng bằng 400; chưa lưu nước thành công thì không phát sự kiện xác nhận.

File chính và cách chạy: [docs/sprint4-core.md](docs/sprint4-core.md). Base: develop sau PR #49. Chỉ cập nhật 9 thẻ đầu; trưởng nhóm review và merge.
