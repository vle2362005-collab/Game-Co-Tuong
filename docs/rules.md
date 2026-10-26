# GCT-19 — Bộ luật tham chiếu và giới hạn

Rule set của đồ án: `gct-core-1`. Nguồn tham chiếu cố định: [WXF World Xiangqi Rules 2018](https://www.wxf-xiangqi.org/images/wxf-rules/2018_World_XiangQi_Rules_English2018.pdf), Chương 1; luật phân xử nâng cao ở Chương 4–5. Đây là phạm vi lựa chọn cho đồ án, chưa phải chứng nhận tuân thủ đầy đủ WXF.

## Luật cơ bản cần triển khai

| Quân | Di chuyển |
|---|---|
| Tướng | Ngang/dọc một điểm trong cung; không đối mặt Tướng kia |
| Sĩ | Chéo một điểm trong cung |
| Tượng | Chéo hai điểm, mắt trống, không qua sông |
| Mã | Chữ L, chân không bị chặn |
| Xe | Ngang/dọc, đường trống |
| Pháo | Đi như Xe; ăn cần đúng một ngòi |
| Tốt | Tiến một; qua sông thêm ngang; không lùi, không phong cấp |

Đỏ đi trước. Không ăn quân mình hoặc tự chiếu. Bên hết nước hợp lệ thua, kể cả không bị chiếu. Engine chốt chiếu bí/bí nước, không cần cho ăn mất Tướng.

## Quy ước riêng của dự án

Theo GCT-42: cột 0–8; hàng 0–9; Đen trên, Đỏ dưới. Cung: cột 3–5, hàng 0–2/7–9. Sông giữa hàng 4/5; Tốt Đỏ qua sông tại hàng ≤4, Tốt Đen tại hàng ≥5. Đảo góc nhìn chỉ đổi giao diện, không đổi tọa độ mạng.

## Chưa hỗ trợ

GCT-62 chỉ đặc tả lặp thế/trường chiếu/trường tróc; chưa tự phân xử đầy đủ, chưa có hòa ba lần, hòa do số nước không ăn hoặc tự nhận thiếu lực lượng. UI phải công bố giới hạn. Hòa thỏa thuận, đồng hồ và phục hồi dùng chính sách đồ án tại `requirements.md`, không gán là luật giải đấu WXF.
