Hoàn thiện luồng Sprint 3 từ đăng nhập đến hai người tạo phòng, sẵn sàng, chơi cờ, nhắn tin và gọi video. Thay các API chỉ xác nhận và kết nối negotiate bằng xử lý nghiệp vụ, giao dịch lưu trận/nước/kết quả và SignalR thực tế; tái sử dụng các nhánh tài khoản hiện có.

Jira: GCT-27, GCT-28, GCT-29, GCT-7, GCT-30, GCT-32, GCT-33, GCT-34, GCT-35, GCT-41, GCT-107, GCT-43, GCT-106, GCT-50, GCT-51, GCT-52, GCT-53, GCT-54, GCT-55, GCT-56, GCT-57, GCT-58, GCT-59, GCT-60, GCT-62, GCT-36, GCT-37, GCT-39, GCT-104, GCT-8, GCT-40, GCT-44, GCT-46, GCT-38, GCT-45, GCT-49, GCT-105, GCT-73, GCT-74, GCT-81, GCT-86, GCT-111, GCT-63, GCT-64, GCT-68, GCT-69, GCT-70, GCT-65, GCT-66.

Kiểm tra: 144 backend Release, 43 frontend; lint/typecheck/build và restore locked đạt. Browser kiểm tra bảy nhóm luồng qua hai tài khoản, gồm truyền video frames/audio tracks với thiết bị giả lập, mic/camera/kết thúc, đồng bộ nước và giao diện 390px.

Giới hạn: demo một tiến trình API; chưa phân xử đầy đủ lặp thế WXF; kết nối khác mạng cần cấu hình HTTPS/TURN theo GCT-67. SQL Server giữ cấu hình hiện có; kiểm thử mới dùng SQLite tạm, không sửa dữ liệu thật.

Chạy và file chính: [docs/sprint3.md](docs/sprint3.md). Kết quả: [docs/sprint3-verification.md](docs/sprint3-verification.md). Giữ nguyên GCT-23; chỉ chuyển các thẻ Done sau khi tích hợp và nghiệm thu.
