> Cập nhật 26/10/2026: ruleset [25888788](https://github.com/vle2362005-collab/Game-Co-Tuong/settings/rules/25888788) đã Active cho main/develop, được xác minh lại qua API. Chỉ user trưởng nhóm bypass qua PR. Các đoạn mô tả chưa áp dụng bên dưới là baseline trước khi cấu hình; không còn là trạng thái hiện tại. Chưa thử bằng tài khoản thành viên.

# GCT-22 — Bàn giao quy trình Jira và GitHub

[Jira GCT-22](https://vle2362005.atlassian.net/browse/GCT-22) · Nhánh `feature/GCT-22-jira-workflow`, PR vào develop.

## Đầu ra

| Tệp | Cách dùng |
|---|---|
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Chọn Task, cập nhật develop, tạo nhánh, commit, PR, xử lý conflict và nghiệm thu |
| [Mẫu PR](../.github/PULL_REQUEST_TEMPLATE.md) | Ghi mã GCT, Jira, thay đổi, kiểm tra, minh chứng và phụ thuộc |
| [jira-workflow.md](jira-workflow.md) | Cấp Epic–Story–Task, trường kế hoạch, Blocks/Relates, điều kiện bắt đầu/Done |
| [Bộ ruleset](../.github/rulesets/README.md) | Cấu hình để trưởng nhóm kiểm soát review và merge trên main/develop |

Kết quả rà soát thực tế, thông tin phân công, lịch và quan hệ giữa các issue được giữ trên Jira. Repo công khai không chứa bản xuất dữ liệu kế hoạch hoặc snapshot toàn bộ dự án.

## Quy trình theo dõi

Story và Task trực tiếp dưới Epic, liên kết bằng Relates; Task có Assignee, Original estimate, lịch và giai đoạn trong trường Jira. Phụ thuộc dùng Blocks, không dùng Parent sai cấp. Khi bàn giao, gắn source/tài liệu cố định theo commit, PR và kết quả kiểm tra phù hợp. Trưởng nhóm đối chiếu tiêu chí rồi mới chuyển Done.

Nhánh, commit và tiêu đề PR mang mã GCT. PR công việc vào develop, PR tích hợp bản ổn định vào main. GCT-23 triển khai CI; quy trình GCT-22 không tạo check tự động hoặc tự chuyển Jira sang Done.

## Kiểm tra tệp

Đã kiểm tra JSON cấu hình đọc được và các trường khớp đặc tả REST rulesets của GitHub; liên kết tài liệu nội bộ tồn tại; CODEOWNERS hiện có chỉ định trưởng nhóm cho mọi file; không sửa mã ứng dụng. Việc kiểm tra cấu hình không chứng minh ruleset đã được bật hoặc quyền của tài khoản thành viên đã được kiểm thử.

## Phần thiết lập cần xác minh

Bộ công cụ GitHub hiện không có thao tác ghi thiết lập quản trị; đọc bảo vệ classic trả HTTP 403. Đã chuẩn bị ruleset dành cho main/develop, chỉ user trưởng nhóm bypass qua PR, yêu cầu Code Owner review và giới hạn update/merge. Xem hướng dẫn và trạng thái quan sát tại [.github/rulesets](../.github/rulesets/README.md).

Merge các file cấu hình này không tự bật bảo vệ GitHub. Khi cấu hình được áp dụng, đọc lại trạng thái ruleset và kiểm tra riêng quyền merge để ghi bằng chứng vào Jira. Không thử xóa hay force-push nhánh thật để kiểm tra. Giữ GCT-22 In Progress khi còn chờ review hoặc thiết lập quản trị.
