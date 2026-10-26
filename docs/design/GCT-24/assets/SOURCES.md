# Quân cờ vector

14 quân SVG gồm 7 loại × 2 bên. Phần mặt quân, hai vòng viền, màu nền và cách đặt trên bàn được chỉnh lại theo hình tham khảo của người dùng. Không đưa hình tham khảo vào repository.

Ký hiệu Hán tự là đường vector, không dùng font ở máy người xem và không dùng nhãn chữ tiếng Việt để vẽ mặt quân. Nhãn tiếng Việt vẫn có trong phần accessibility.

Nguồn đường nét ký hiệu:

- Wj654cj86, bộ quân Xiangqi trên Wikimedia Commons, được tác giả đưa vào public domain. Ví dụ: [Tượng Đen](https://commons.wikimedia.org/wiki/File:Xiangqi_ed1.svg), [Mã Đen](https://commons.wikimedia.org/wiki/File:Xiangqi_hd1.svg).
- Bản vector dùng làm đầu vào: [MeldrumJon/Xiangqi, res](https://github.com/MeldrumJon/Xiangqi/tree/9d1aac93007bf198e8c1e09c23ca1da9a99acc72/res), commit `9d1aac93007bf198e8c1e09c23ca1da9a99acc72`. [README nguồn](https://github.com/MeldrumJon/Xiangqi/blob/9d1aac93007bf198e8c1e09c23ca1da9a99acc72/README.md) ghi nhận Wj654cj86 và public-domain/CC0 cho artwork.
- Chỉ sử dụng đường nét hình ảnh; không sao chép mã trò chơi GPL của repository nguồn.
- Thay đổi: bỏ bóng/viền nền cũ, bỏ metadata/ID phụ thuộc, giữ outline ký hiệu, vẽ lại mặt quân xanh ngà cho Đen và ngà hồng cho Đỏ, hai vòng viền, đóng gói 14 SVG và symbol nội tuyến trong `pieces.mjs`.

Quân Tướng/Sĩ/Tượng/Tốt của hai bên dùng ký hiệu riêng; Xe/Mã/Pháo giữ đúng mẫu từng bên. Mỗi asset đứng riêng và có thể dùng trong Vue bằng `<img>` hoặc inline SVG. `pieces.mjs` cung cấp các symbol để prototype và SVG màn hình chạy không cần tài nguyên bên ngoài.
