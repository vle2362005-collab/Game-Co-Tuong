# GCT-62 — Phạm vi luật lặp thế

Bản `gct-core-1` xử lý luật đi 7 quân, hai Tướng đối mặt, tự chiếu, chiếu bí và hết nước. Hết nước hợp lệ là thua, kể cả không bị chiếu.

**Chưa tự phân xử lặp thế, trường chiếu hoặc trường tróc.** Không tự tuyên hòa khi bàn cờ xuất hiện ba lần. Đây là đặc tả phạm vi của GCT-62, không phải tuyên bố đã triển khai đầy đủ luật thi đấu WXF.

Nguồn: [World Xiangqi Federation — World Xiangqi Rules 2018, chương 19–20](https://www.wxf-xiangqi.org/images/wxf-rules/2018_World_XiangQi_Rules_English2018.pdf). Luật phân biệt tính chất chuỗi nước của cả hai bên; không thể thay bằng đếm số lần lặp theo luật cờ vua.

| Ví dụ kiểm thử phạm vi | Kết quả bản hiện tại |
|---|---|
| Hai bên di chuyển Xe qua lại, tái lập cùng vị trí và lượt ba lần | Trận tiếp tục; không tự tuyên hòa |
| Một bên lặp nước chiếu | Kiểm tra nước có hợp lệ; chưa xác định lỗi trường chiếu |
| Một bên liên tiếp đuổi một quân | Chưa phân loại trường tróc hoặc áp dụng hình phạt |
| Hai bên đồng ý hòa | Kết thúc với `draw-agreement` |

Dữ liệu đã lưu: `InitialBoardJson`, `CurrentBoardJson`, `RuleSetVersion`, thứ tự nước `Ply`, quân đi/ăn, tọa độ, bên đi và snapshot sau từng nước. Khi mở rộng cần khóa thế cờ theo **loại quân + màu + vị trí + lượt**, phân loại chiếu/đuổi quân và kiểm tra chu kỳ theo chuẩn đã chọn; không dựa vào ID quân hoặc chỉ so hai JSON có thứ tự phần tử khác nhau.
