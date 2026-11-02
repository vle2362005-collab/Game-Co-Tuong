# Bàn giao ứng viên S4 — GCT-102

- Source: nhánh `feature/GCT-114-s4-closeout`, PR vào `develop`, base `7fb3b66`; không đẩy thẳng main/develop.
- Kiểm tra: `scripts/verify.sh` / `.ps1`; [báo cáo và giới hạn](verification.md), [phân tích](analysis-design.md), [kịch bản](demo.md).
- Package: `scripts/package-demo.sh` / `.ps1` tạo `artifacts/demo` gồm API, web, SQL idempotent, mẫu cấu hình, SOURCE_COMMIT, WORKTREE_STATE và manifest SHA-256. Chạy lại sau commit để xác nhận worktree sạch; không kèm config Local, DB hoặc credential thật.
- [Hướng dẫn chạy/backup/rollback](runbook.md). Frontend cấu hình API trước khi build; host tĩnh phải fallback route về index.html.
- Chờ trưởng nhóm review và nghiệm thu các giới hạn SQL Server/LAN/TURN/thiết bị. Chỉ sau đó mới chọn commit đã merge/build/test để gắn tag phát hành; hiện là ứng viên review, chưa phải bản production đã triển khai.

Quy trình tag sau duyệt (tên tag phải chưa tồn tại): checkout đúng SHA đã nghiệm thu → chạy verify và package → đối chiếu SOURCE_COMMIT/manifest → `git tag -a s4-rc-20261103 <SHA> -m "S4 release candidate"` → `git push origin s4-rc-20261103`. Không chuyển tag cũ hoặc tag một commit chưa kiểm tra.

Trước đổi các thẻ sang Done: mở được link PR/source/kiểm thử, xác nhận PR đã merge, rà lỗi/giới hạn trong verification và ghi rõ mục chưa nghiệm thu. Hướng dẫn nguồn commit và tag là điều kiện bàn giao, không thay thế phê duyệt của trưởng nhóm.
