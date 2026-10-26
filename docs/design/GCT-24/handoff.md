# GCT-24 → GCT-32: bàn giao triển khai

Đây là đặc tả thao tác cho Vue, không phải frontend nghiệp vụ đã tích hợp. Mẫu HTML sử dụng SVG và state minh họa để review hình thức/quyền; không đưa thanh giả lập hay xác thực giả vào giao diện thật.

## Luồng sử dụng

1. Đăng ký → đăng nhập → sảnh; hết phiên yêu cầu đăng nhập rồi lấy lại trạng thái có quyền xem.
2. Sảnh → tạo phòng (host Đỏ) hoặc tham gia (guest Đen) → phòng chờ.
3. Mỗi người tự xác nhận sẵn sàng. Chủ Bắt đầu khi snapshot cho biết đủ hai người ready/online; response thành công mới vào trận.
4. Đỏ đi trước. Khách Đen xem bàn Đỏ dưới/Đen trên trong bản thiết kế này và được báo đúng bên/lượt của mình. Mọi quyền đi quân vẫn do server xác minh.
5. Nước đi, hòa, đầu hàng hoặc hết giờ có thể kết thúc trận. Hiển thị kết quả từ dữ liệu server, không suy winner từ sideToMove hoặc màu người xem.
6. Xem lịch sử/chi tiết sau trận; đóng phòng không xóa lịch sử. Chơi tiếp phải rời/đóng phòng cũ rồi tạo phòng mới, không Start lại cùng room.

## Trạng thái phía frontend

- Giữ riêng auth, room, match, chat cursor và request đang chờ. Dữ liệu mẫu `model.mjs` không thay thế API client.
- Disable nút đang gửi và không tạo requestId mới khi retry cùng ý định. Không xác nhận nước đi, thắng/thua hoặc kết quả lưu chỉ dựa vào hiệu ứng UI.
- Theo GCT-20, phiên bản room và match độc lập. Khi gap hoặc VERSION_CONFLICT: tải lại snapshot và lịch sử cần thiết rồi mới mở thao tác.
- Đăng ký handler trước Subscribe, buffer sự kiện trong lúc sync, áp dụng snapshot rồi xử lý buffer đúng version. Subscribe lại sau reconnect; nối transport thành công chưa đủ để mở bàn.
- Ngân sách nối lại/deadline do server trả về. Không reset về 60s sau mỗi lần reconnect; UI demo trừ 15s mỗi lần chỉ để minh họa nguyên tắc.
- `paused` khóa Move và Draw, giữ snapshot/clock checkpoint. Đầu hàng/chat cần kết nối và quyền hợp lệ; khi chính client offline thì khóa gửi.
- Offer hòa có hạn 30s và clock vẫn chạy. Nước mới, pause, từ chối hoặc hết hạn làm offer cũ mất hiệu lực.

## Phản hồi lỗi

| Nhóm lỗi | Giao diện và cách tiếp tục |
|---|---|
| Xác thực/hết phiên | Thông báo gần form; đăng nhập lại, sau đó lấy snapshot có quyền xem |
| Phòng đầy/đã đóng/không tồn tại | Giữ thông báo rõ ràng; quay về sảnh, tải lại danh sách hoặc nhập mã khác |
| Không đủ điều kiện Start | Giữ phòng chờ, tải readiness/presence mới; chỉ host thấy Bắt đầu |
| Không có quyền | Không đổi dữ liệu; quay về màn hình có quyền xem hoặc sảnh |
| Sai lượt/nước đi không hợp lệ | Giữ bàn đã xác nhận, bỏ chọn, hiển thị lý do; cho chọn lại |
| Version lệch/trận đã kết thúc | Lấy snapshot; mở lại thao tác hoặc chuyển kết quả theo server |
| Mất mạng/timeout | Hiển thị trạng thái reconnect và thao tác Thử lại; không gửi chồng lệnh |
| Lỗi máy chủ/lưu kết quả | Giữ thông báo chờ/thử lại; không tự công bố “đã lưu” khi chưa có xác nhận |

## Các trường hợp review trước khi nghiệm thu tích hợp

- Host/guest có quyền khác nhau; bấm Bắt đầu hai lần không tạo hai trận.
- Hai người cùng đi hoặc reconnect cạnh tranh timeout nhận cùng trạng thái server; không đảo kết quả từ suy đoán client.
- Rời/Đóng khi waiting có xác nhận; khi playing dùng Đầu hàng. Cancel dialog giữ nguyên trạng thái.
- Phòng trống, lịch sử rỗng, dữ liệu đang tải, lỗi, hết phiên đều có đường ra.
- Tải bù moves/chat theo cursor; dữ liệu đã lưu đọc được sau khi đóng phòng.
- Tích hợp accessibility và responsive thực: focus dialog, nhãn input, keyboard, thông báo live, cỡ chữ, tương phản và vùng bấm trên thiết bị thật.

Mô hình đã có kiểm tra tự động để hỗ trợ review thiết kế; các ca tích hợp trên chưa được đánh dấu pass.
