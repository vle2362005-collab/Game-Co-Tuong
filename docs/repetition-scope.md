# GCT-62 — Phạm vi luật lặp thế

Bản `gct-core-1` xử lý luật đi 7 quân, hai Tướng đối mặt, tự chiếu, chiếu bí và hết nước. Hết nước hợp lệ là thua, kể cả không bị chiếu.

**Chưa tự phân xử lặp thế, trường chiếu hoặc trường tróc.** Không tự tuyên hòa khi bàn cờ xuất hiện ba lần. Đây là đặc tả phạm vi của GCT-62, không phải tuyên bố đã triển khai đầy đủ luật thi đấu WXF.

Nguồn: [World Xiangqi Federation — World Xiangqi Rules 2018, chương 19–20](https://www.wxf-xiangqi.org/images/wxf-rules/2018_World_XiangQi_Rules_English2018.pdf). Luật phân biệt tính chất chuỗi nước của cả hai bên; không thể thay bằng đếm số lần lặp theo luật cờ vua.

## Đối chiếu bản demo — GCT-112 (02/11/2026)

Các mẫu dưới đây được chạy qua luồng `GameService.Move` với nước hợp lệ, lưu nước và snapshot vào SQLite kiểm thử. Khóa thế dùng loại quân, màu, tọa độ và bên đi; không dùng ID quân. Kết quả cho biết hành vi của demo, **không** phải kết luận phân xử theo WXF.

| Ví dụ kiểm thử phạm vi | Kết quả thực tế |
|---|---|
| Hai bên di chuyển Xe qua lại, tái lập cùng vị trí và lượt ba lần | 8 nước hợp lệ; cùng khóa thế xuất hiện 3 lần (kể cả lượt đi); trận vẫn `playing`, không tự tuyên hòa. |
| Đỏ lặp chu kỳ Xe chiếu Tướng Đen | Hai chu kỳ; mỗi nước chiếu của Đỏ và nước đáp của Đen đều hợp lệ. Trận vẫn tiếp tục; chưa phát hiện/phân xử trường chiếu dai. |
| Đỏ lặp chu kỳ Xe tấn công cùng một Mã | Hai chu kỳ; sau mỗi nước Xe, Mã vẫn là mục tiêu có thể ăn và Mã thoát được. Trận vẫn tiếp tục; chưa phân loại trường tróc dai hoặc áp dụng hình phạt. |
| Hai bên đồng ý hòa | Luồng `draw-agreement` riêng vẫn được hỗ trợ; không phải kết quả tự sinh từ lặp thế. |

Kiểm thử hồi quy: `GameFlowTests.GCT112_three_occurrences_of_same_position_do_not_auto_draw`, `GameFlowTests.GCT112_repeated_check_sequence_is_legal_but_not_adjudicated` và `GameFlowTests.GCT112_repeated_attack_on_same_piece_is_not_classified_as_chase`.

Màn hình ván cờ nêu rõ các giới hạn trên. Không áp quy tắc hòa ba lần của cờ vua; hiện engine chỉ tự kết thúc do chiếu bí/bí nước hoặc các kết thúc khác đã triển khai. Dữ liệu nước đi lưu snapshot và thứ tự, nhưng demo chưa giữ bộ đếm lịch sử thế để phân xử chu kỳ. Khi mở rộng cần khóa thế cờ theo **loại quân + màu + vị trí + lượt**, phân loại chiếu/đuổi quân và kiểm tra chu kỳ theo chuẩn đã chọn; không dựa vào ID quân hoặc chỉ so hai JSON có thứ tự phần tử khác nhau.

### Kiểm chứng

- `dotnet test tests\backend\GameCoTuong.Foundation.Tests\GameCoTuong.Foundation.Tests.csproj --no-restore --filter FullyQualifiedName~GCT112` — đạt, 3/3 (SDK .NET 10.0.403).
- `dotnet test tests\backend\GameCoTuong.Foundation.Tests\GameCoTuong.Foundation.Tests.csproj --no-restore --filter FullyQualifiedName~GameFlowTests` — đạt, 29/29.
- `npm.cmd --prefix src\frontend test` — đạt, 54/54.
- `npm.cmd --prefix src\frontend run lint`, `npm.cmd --prefix src\frontend run typecheck` và `npm.cmd --prefix src\frontend run build` — đều đạt.
