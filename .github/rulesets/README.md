# Cấu hình bảo vệ nhánh — chưa áp dụng

`gct-review.json` là cấu hình chuẩn bị để import cho repo `vle2362005-collab/Game-Co-Tuong`. Commit/merge file này không bật quy tắc trên GitHub. Giá trị `enforcement: active` chỉ có hiệu lực sau khi người có quyền quản trị import/lưu ruleset.

## Trạng thái quan sát ngày 26/10/2026

| Mục | Quan sát thực tế |
|---|---|
| main | GitHub trả protected=true; không đọc được chi tiết do integration thiếu quyền Administration, HTTP 403 |
| develop | GitHub trả protected=false |
| Repository rulesets | API trả danh sách rỗng |
| CODEOWNERS trên main/develop | Đã có `* @vle2362005-collab` |

Chưa có bằng chứng bảo vệ develop hoặc bắt buộc review của trưởng nhóm trên cả hai nhánh. Bộ công cụ GitHub hiện tại không có thao tác ghi cấu hình quản trị; chưa thay đổi thiết lập đó trong GCT-22.

## Cấu hình đã chuẩn bị

| Thành phần | Tác dụng dự kiến sau khi áp dụng |
|---|---|
| Phạm vi | Chỉ main và develop; thành viên vẫn push nhánh feature/fix |
| Restrict updates | Chỉ actor có bypass được cập nhật nhánh, kể cả merge |
| Bypass | Đúng user `vle2362005-collab`, ID 231604162; chỉ dùng qua PR, không cấp cho mọi admin hoặc ứng dụng |
| Pull request | Bắt buộc PR, một approval và Code Owner review; commit mới làm review cũ hết hiệu lực |
| Review dismissal | Chỉ user trưởng nhóm được dismiss review |
| Merge | Dùng merge commit; cần resolve các trao đổi review |
| Force-push/xóa | Bị chặn trên hai nhánh; không thêm ngoại lệ luôn luôn bypass |

Chỉ trưởng nhóm có quyền merge theo Restrict updates + bypass user cụ thể. Thành viên khác vẫn có thể bình luận/review nếu GitHub cấp quyền, nhưng approval của họ không thay Code Owner approval. GitHub không có ý nghĩa “ẩn nút Approve của mọi người khác” trong cấu hình này.

PR do chính tài khoản trưởng nhóm tạo không thể được chính tài khoản đó Approve. User bypass chỉ qua PR cho phép trưởng nhóm rà soát và merge PR của mình; không tạo một review tự phê duyệt. Với PR của thành viên, trưởng nhóm vẫn review và Approve trước khi merge theo quy trình nhóm. Đây là quyền kiểm soát của trưởng nhóm, không phải bảo đảm trưởng nhóm không thể chủ động bỏ qua bước review.

Không thêm required status checks chưa tồn tại; GCT-23 triển khai CI rồi mới gắn đúng tên check. Không áp dụng quy tắc tên commit lên toàn bộ lịch sử cũ hoặc các merge commit GitHub tự tạo.

## Thao tác áp dụng cụ thể

1. Mở Settings → Rules → Rulesets của repo. Kiểm tra lại actor, CODEOWNERS và bảo vệ nhánh cũ trước khi lưu; không xóa cấu hình cũ chỉ vì không đọc được nó qua integration.
2. Chọn New ruleset → Import a ruleset, tải file `gct-review.json`. Kiểm tra tên ruleset, hai nhánh đích và user bypass chỉ ở chế độ Pull requests only.
3. Kiểm tra Require a pull request, một approval, Code Owners, stale approval dismissal, review dismissal, Restrict updates, xóa/force-push. Lưu ở trạng thái Active khi trưởng nhóm chấp thuận cấu hình.
4. Xem lại rule đang áp dụng cho main/develop và xác nhận các bảo vệ classic/ruleset không gây yêu cầu chồng chéo ngoài ý muốn. Bypass của ruleset này không tự thay đổi quy tắc classic đang có.
5. Kiểm chứng bằng PR thử của thành viên: trước trưởng nhóm duyệt không merge được, sau duyệt thành viên vẫn không tự merge được, trưởng nhóm merge được. Kiểm tra PR do trưởng nhóm tạo vẫn đi qua PR; không thử force-push/xóa nhánh thật để chứng minh.

Khi chưa làm các bước trên, ghi “cấu hình sẵn sàng, chưa áp dụng”, không ghi “đã bảo vệ”.

Nguồn chính thức đã đối chiếu: [REST rulesets](https://docs.github.com/en/rest/repos/rules), [nhập ruleset JSON](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/managing-rulesets-for-a-repository), [user bypass](https://github.blog/changelog/2026-05-07-repository-rulesets-user-bypass-and-branch-renaming/), [các quy tắc](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets).
