# Quy ước Jira và minh chứng — GCT-22

Áp dụng cho dự án GCT, board 36. Quy trình dùng các trạng thái sẵn có To Do, In Progress và Done; review-pending là nhãn, không phải trạng thái mới.

## Cấu trúc

| Loại | Vai trò | Parent và liên kết |
|---|---|---|
| Epic | Gom một nhóm chức năng hoặc công việc nền tảng | Không cần tạo thêm cấp cha |
| Story | Nhu cầu người dùng và điều kiện nghiệm thu | Parent là Epic; Relates tới các Task triển khai/kiểm thử |
| Task | Một phần triển khai, tài liệu hoặc kiểm thử cụ thể | Parent là Epic; Relates tới Story khi phục vụ Story đó |

Story và Task cùng cấp dưới Epic. Không đổi Task thành con của Story và không tạo Sub-task. Task nền tảng dùng chung có thể chỉ thuộc Epic và có Blocks tới Task phụ thuộc; không bắt buộc tạo một Story hình thức cho mọi Task.

Ví dụ quy ước: cùng Epic có Story S và Task T. Task T Relates Story S; Task nền tảng A Blocks Task T nếu T cần đầu ra của A. `A blocks B` nghĩa B cần đầu ra của A. Relates không thể hiện thứ tự, Blocks không thay quan hệ Parent. Không tạo hai chiều Blocks cho cùng một phụ thuộc hoặc tạo chu trình.

## Trường cần dùng

| Trường Jira | Quy ước |
|---|---|
| Assignee | Một người chịu trách nhiệm trên mỗi Story/Task |
| Original estimate | Ước lượng giờ cho Task, không cộng lặp qua Story/Epic |
| Start date / Due date | Thời điểm mục tiêu bắt đầu và hạn của chính công việc |
| Sprint | Giai đoạn làm việc; dùng trường Sprint để lập kế hoạch |
| Fix versions | Bản phát hành chứa phạm vi công việc; khác với Sprint |
| Parent | Epic đúng nhóm chức năng |
| Issue links | Relates giữa Story–Task; Blocks cho đầu vào/đầu ra cần trước |
| Description | Mục tiêu, việc cần làm, điều kiện hoàn thành; link bàn giao ngắn gọn ở cuối |

Giữ kế hoạch hiện có của nhóm. Thay người làm, giờ, lịch hoặc phạm vi khi có trao đổi rõ ràng; GCT-22 không tự đổi các trường đó khi đã đầy đủ. Một ngày bắt đầu trong kế hoạch không chứng minh đã thực sự làm việc; không ghi worklog giả theo estimate.

## Bắt đầu và nghiệm thu

Trước In Progress: hiểu tiêu chí, có người phụ trách và đầu vào từ phụ thuộc đã sẵn sàng; đã cập nhật develop rồi tạo nhánh đúng mã. Nếu task đang bị chặn, ghi rõ blocker và phần nào có thể làm độc lập. Blocks chỉ là liên kết; Jira không tự cấm Start hoặc chuyển trạng thái nếu chưa cấu hình thêm điều kiện workflow.

Trong In Progress: giữ cập nhật thực tế; thêm review-pending khi có PR để trưởng nhóm xem. Không coi việc mở PR, build thành công hoặc kéo sang Done là bằng chứng đủ cho toàn bộ chức năng.

Done khi đã đối chiếu điều kiện hoàn thành, có đầu ra truy cập được, có kết quả kiểm tra phù hợp và trưởng nhóm nghiệm thu/merge. Tài liệu dùng kết quả rà soát tài liệu; code dùng kiểm thử đúng chức năng, build và kiểm thử tích hợp cần thiết. Ca mới lập kế hoạch không ghi là đã pass. Story cần nghiệm thu hành vi người dùng qua các Task Relates, không chỉ nhìn số thẻ con.

## Nội dung bàn giao trên thẻ

```text
Bàn giao:
Nhánh: feature/GCT-<mã>-<mô-tả-ngắn>
Commit: <link commit đầy đủ>
PR: <link PR, base và trạng thái>
Đầu ra: <link file/tài liệu tại commit cố định>
Kiểm tra: <lệnh/bước đã chạy, môi trường, kết quả và giới hạn>
Nghiệm thu: <trưởng nhóm xác nhận; chưa có thì ghi chờ review>
```

Không chỉ gắn link gốc repo hoặc nhánh dự kiến rồi coi là đã có source. Link file cố định theo commit không đổi khi nhánh được cập nhật/xóa. Mã GCT xuất hiện trong tên nhánh, commit và PR title để GitHub for Jira tự liên kết; Development có thể đồng bộ chậm. Không đổi trạng thái Jira tự động bằng Smart Commit trước nghiệm thu.

## Rà soát định kỳ

Các truy vấn có thể dùng trong Jira Advanced search:

```jql
project = GCT AND issuetype = Sub-task
project = GCT AND issuetype in (Story, Task) AND assignee is EMPTY
project = GCT AND issuetype = Task AND originalEstimate is EMPTY
project = GCT AND issuetype in (Story, Task) AND parent is EMPTY
project = GCT AND statusCategory = Done ORDER BY key
project = GCT AND labels = review-pending ORDER BY updated
```

Kiểm tra Parent thực sự là Epic, liên kết Story–Task, chu trình Blocks và các trường hợp Done thiếu minh chứng bằng dữ liệu issue đầy đủ. Truy vấn đếm không đủ để chứng minh các liên kết này đúng.

Kết quả rà soát chi tiết được ghi trong [thẻ GCT-22](https://vle2362005.atlassian.net/browse/GCT-22). Không xuất lịch, phân công và đồ thị phụ thuộc Jira vào repo công khai. [Tệp bàn giao](GCT-22-audit.md) trong repo mô tả cách dùng quy trình và phần thiết lập cần xác minh. GCT-23 tiếp tục phần CI; quy tắc bảo vệ nhánh xem [.github/rulesets](../.github/rulesets/README.md).
