# Cách làm việc chung — Game Cờ Tướng

Jira giữ yêu cầu và phân công; GitHub giữ mã nguồn, tài liệu và PR. Mỗi Task có một người chịu trách nhiệm, một nhánh chính và PR mang đúng mã GCT. Trưởng nhóm `@vle2362005-collab` duyệt và merge.

## 1. Trước khi viết code

Mở Task được giao, đọc mục tiêu và điều kiện hoàn thành. Kiểm tra Epic, Story liên quan qua Relates, người phụ trách, Original estimate, Start date, Due date, Sprint, Fix versions và Blocks. Phụ thuộc chưa hoàn thành thì trao đổi trên thẻ và làm rõ đầu vào trước khi bắt đầu phần bị chặn. Jira Blocks là thông tin phụ thuộc, không tự khóa nút chuyển trạng thái.

Xem [quy ước Jira](docs/jira-workflow.md) và [hướng dẫn chạy](docs/development.md). Không tạo project riêng rồi chép cả thư mục lên repo chung. Domain/Engine/Application/Infrastructure đã có vị trí trong solution.

## 2. Lấy nền tảng mới nhất và tạo nhánh

Lần đầu:

```sh
git clone --branch develop https://github.com/vle2362005-collab/Game-Co-Tuong.git
cd Game-Co-Tuong
```

Mỗi công việc mới, từ working tree sạch:

```sh
git switch develop
git pull --ff-only origin develop
git switch -c feature/GCT-42-engine-model
```

`GCT-42` là ví dụ: thay bằng mã Task thực sự được giao. Tên nhánh theo mẫu `feature/GCT-<số>-<mô-tả-ngắn>`; sửa lỗi có thể dùng `fix/GCT-<số>-<mô-tả-ngắn>`. Dùng đúng chữ hoa `GCT`. Mỗi thành viên làm trên nhánh của mình, PR vào develop. Main nhận bản ổn định qua PR do trưởng nhóm xử lý.

Nếu còn thay đổi chưa commit, tự commit hoặc stash phần của mình trước khi đổi nhánh/pull. Không dùng force-push, reset --hard hay --allow-unrelated-histories để xử lý một nhánh đang lệch khi chưa hiểu nguyên nhân.

## 3. Kiểm tra, commit và push

Chạy `./scripts/verify.ps1` trên PowerShell hoặc `./scripts/verify.sh` trên Bash. Khi thêm chức năng, bổ sung ca kiểm tra đúng tiêu chí của Task. Các kiểm thử bộ khung hiện có không chứng minh chức năng mới đã đạt.

```sh
git status
git add <các-file-đã-sửa>
git diff --cached
git commit -m "GCT-42: add board and piece model"
git push -u origin feature/GCT-42-engine-model
```

Thay `<các-file-đã-sửa>` bằng đường dẫn thật. Không commit bin, obj, node_modules, dist, file cấu hình riêng, mật khẩu hoặc token. Kiểm tra diff để không đẩy nhầm phần của người khác.

## 4. Mở PR

Trên GitHub chọn **base: develop**, **compare: nhánh của mình**. Tiêu đề ví dụ `GCT-42: Xây dựng dữ liệu bàn cờ và quân cờ`. Điền [mẫu PR](.github/PULL_REQUEST_TEMPLATE.md): Jira, thay đổi, kết quả kiểm tra, đường dẫn file chính và phụ thuộc. Nếu GitHub chưa tự hiện mẫu do file chưa vào nhánh mặc định, copy nội dung mẫu vào PR.

Gắn link PR, commit và file chính vào Jira; để In Progress và thêm nhãn review-pending. Chọn trưởng nhóm làm reviewer nếu mình không phải tác giả dùng tài khoản trưởng nhóm. Không bật auto-merge hoặc tự merge PR của mình.

Mã GCT trong nhánh/commit/tiêu đề PR giúp GitHub for Jira nối dữ liệu vào Development. Vẫn giữ link trực tiếp trong thẻ để tra cứu khi tích hợp chưa đồng bộ. Không thêm lệnh Smart Commit chuyển Done khi chưa nghiệm thu.

## 5. Khi develop thay đổi hoặc có conflict

Từ nhánh đang làm và working tree sạch:

```sh
git fetch origin
git merge origin/develop
```

Nếu có conflict, đọc từng khối, giữ hành vi đã thống nhất của cả hai phần và nhờ người phụ trách file phối hợp khi cần. Sau khi sửa:

```sh
git add <các-file-đã-sửa-conflict>
git commit
git push
```

Chạy lại kiểm tra trước khi yêu cầu review. Không chọn toàn bộ ours/theirs chỉ để xóa dấu conflict. PR cũ bị đóng trước khi có nền tảng GCT-21 thì tạo nhánh mới từ develop đã cập nhật, chuyển từng thay đổi cần thiết rồi kiểm tra; không chép đè cả repo bằng bản cũ.

## 6. Trưởng nhóm duyệt và tích hợp

Trưởng nhóm đọc Files changed, đối chiếu tiêu chí Jira, xem kết quả kiểm tra và các PR phụ thuộc. Thiếu phần nào thì Request changes, thành viên sửa trên cùng nhánh và đẩy commit mới. Khi đủ điều kiện, trưởng nhóm Approve PR của thành viên rồi dùng Create a merge commit để giữ lịch sử các commit mang mã Jira.

GitHub không cho tác giả tự Approve PR. PR do tài khoản trưởng nhóm tạo phải được trưởng nhóm tự rà soát và xử lý qua quyền bypass chỉ dành cho PR, khi bộ quy tắc đó đã được bật. Không giả lập review hoặc lấy Approve của người khác để tuyên bố đã có xác nhận của trưởng nhóm.

CODEOWNERS hiện chỉ định trưởng nhóm cho mọi file. File này cần đi cùng quy tắc yêu cầu Code Owner review; tự nó không chặn merge. Bộ cấu hình bảo vệ main/develop đã chuẩn bị ở [.github/rulesets](.github/rulesets/README.md), **chưa được áp dụng bởi PR tài liệu này**. Không coi việc merge PR GCT-22 là đã bật bảo vệ GitHub.

## 7. Sau merge

Trưởng nhóm hoặc người phụ trách cập nhật link PR đã merge, commit/tài liệu và kết quả nghiệm thu vào Jira, xóa review-pending/source-pending phù hợp, rồi chuyển Done. Story chỉ nghiệm thu khi các Task liên quan đáp ứng yêu cầu người dùng; không sao chép ước lượng Task sang Story để cộng hai lần.

Trước công việc tiếp theo, thành viên trở về develop và pull lại. Chưa tích hợp develop vào main thì clone mặc định main có thể chưa chứa thay đổi mới nhất. PR phát hành dùng base main, compare develop và liệt kê các mã GCT trong phạm vi tích hợp.

Nguồn: [Atlassian — nối GitHub với Jira](https://support.atlassian.com/jira-cloud-administration/docs/use-the-github-for-jira-app/), [GitHub — CODEOWNERS](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners), [GitHub — review PR](https://docs.github.com/en/pull-requests/how-tos/review-pull-requests/approving-a-pull-request-with-required-reviews).
