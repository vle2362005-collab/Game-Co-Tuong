# GCT-20 — Bàn giao bộ thiết kế

26/10/2026 · [GCT-20](https://vle2362005.atlassian.net/browse/GCT-20) · Nhánh `feature/GCT-20-contracts` · Hợp đồng `1.0.0`.

## Đầu ra để review

| Yêu cầu Jira | Minh chứng |
|---|---|
| Kiến trúc, trách nhiệm từng lớp, luồng xử lý | [architecture.md](architecture.md) |
| API, mã lỗi, dữ liệu và sự kiện dùng chung | [contracts.md](contracts.md), [openapi.json](../contracts/openapi.json), [schemas.json](../contracts/schemas.json), [signalr.json](../contracts/signalr.json) |
| Chống gửi lặp và xử lý version cũ | contracts.md mục 5, schema SubmitMoveRequest, ca move-accepted/version-conflict |
| Lấy lại dữ liệu khi lệch hoặc thiếu sự kiện | contracts.md mục 6, SubscribeRoom/Match, GET snapshot/moves/messages |
| Đối chiếu phạm vi và vòng đời | [requirements.md](requirements.md), [lifecycle.md](lifecycle.md) |
| Ví dụ đúng/sai và công cụ kiểm tra | [examples.json](../contracts/examples.json), [validate_contracts.py](../scripts/validate_contracts.py), [test_contracts.py](../tests/test_contracts.py) |

Bộ thiết kế có 23 thao tác HTTP, 6 phương thức Hub, 3 loại sự kiện và 67 schema. Có 62 mẫu: 34 hợp lệ và 28 cố ý sai. Các mẫu là dữ liệu giả, không có tài khoản hoặc token thật.

## Kiểm tra đã thực hiện

```sh
python -m pip install -r requirements-contracts.txt
python scripts/validate_contracts.py
python -m unittest discover -s tests -v
```

Kết quả ngày 26/10/2026: đặc tả OpenAPI 3.1 và JSON Schema Draft 2020-12 hợp lệ; 62 mẫu cho kết quả đúng với nhãn valid/invalid; **12 kiểm tra hồi quy hợp đồng pass**. Các kiểm tra gồm ID/version/ply liên kết giữa response và event, requestId, chat sequence, trùng ô/người chơi, deadline theo số dư 40s, phục hồi restart, thời hạn hòa và các tổ hợp trạng thái sai.

Kết quả này chỉ xác nhận đặc tả và dữ liệu mẫu. Chưa có backend/frontend/engine chạy, migration, kiểm thử mạng, race thực tế hay kết quả hiệu năng. Không đánh dấu GCT-21/25/26/42/73–79 đã hoàn thành từ các mẫu thiết kế.

## Hướng dẫn từng phần

- Frontend nhập `contracts/openapi.json` trực tiếp vào công cụ hỗ trợ OpenAPI 3.1; tệp đã nhúng schema, không cần tải tham chiếu ngoài. Dùng cùng enum/toạ độ, lấy snapshot làm trạng thái chính thức, buffer event trong lúc sync và không tự cộng ply khi nhận receipt trùng.
- Backend dùng chung application handler cho REST/Hub, xác thực từ phiên đăng nhập, lưu snapshot/move/receipt/outbox trong transaction rồi mới báo thành công. Domain/engine không phụ thuộc EF hoặc SignalR.
- Phần phòng/trận thực hiện đủ bảng R01–13/M01–19: paused có đồng hồ dừng, ngân sách reconnect không reset; startup giữ checkpoint/deadline; pendingDraw chỉ có hiệu lực 30s khi playing.
- Kiểm thử tích hợp GCT-93–96 tiếp tục từ AC-01–16, đặc biệt deadline±1ms, kết nối cũ ngắt sau kết nối mới, hai lệnh cạnh tranh, crash sau commit và mất sự kiện cuối.

Khi sửa schema, cập nhật các mẫu và tài liệu; chạy `python scripts/validate_contracts.py --write-openapi` để nhúng lại schema vào OpenAPI, sau đó chạy hai lệnh kiểm tra. Không sửa trực tiếp một bản schema nhúng mà bỏ quên nguồn schemas.json.

## Duyệt và tích hợp

Ba phần có phụ thuộc: GCT-19 → GCT-15 → GCT-20. Mỗi phần có nhánh/commit/PR mang mã Jira. PR GCT-19 vào main; PR GCT-15 ban đầu vào nhánh GCT-19; PR GCT-20 ban đầu vào nhánh GCT-15 để diff chỉ chứa việc của đúng task. Khi tích hợp, duyệt theo thứ tự trên và chuyển base của PR tiếp theo sang main sau khi phụ thuộc đã merge. Nếu dùng squash/rebase thì cần cập nhật nhánh phụ thuộc để tránh mang lại commit cũ. Không tự merge hoặc bật auto-merge.

Trưởng nhóm review các chính sách BR-05/08/09, đối chiếu sơ đồ và xác nhận đầu ra thiết kế trước khi chuyển Jira sang Done. Jira chứa link tệp/nhánh/commit/PR và kết quả kiểm tra; chưa có nghiệm thu nhóm thì không ghi đã nghiệm thu.
