# GCT-23 — Tự động build và chạy kiểm thử

## Đầu ra

- [Workflow](../.github/workflows/ci.yml): build/test backend và lint/typecheck/test/build frontend trên PR vào `develop`/`main`, chạy lại sau push.
- [ESLint Vue/TypeScript](../src/frontend/eslint.config.mjs), lockfile cập nhật, SDK thống nhất với hướng dẫn phát triển.
- [Cách chạy, đọc lỗi và dữ liệu kiểm thử](ci.md). Báo cáo TRX/JUnit và bundle frontend có trong Artifacts của mỗi lượt chạy, giữ 14 ngày.

## Kiểm chứng

PR: [#33](https://github.com/vle2362005-collab/Game-Co-Tuong/pull/33), nhánh `feature/GCT-23-ci` → `develop`.

| Minh chứng | Kết quả |
|---|---|
| [Lượt lỗi có chủ đích](https://github.com/vle2362005-collab/Game-Co-Tuong/actions/runs/40474356016), commit [6cf31f0](https://github.com/vle2362005-collab/Game-Co-Tuong/commit/6cf31f0e4512895c20c05d817406d0cb3cba1801) | Backend 1 lỗi/49 test, frontend 1 lỗi/4 test; cả hai check thất bại tại bước test, vẫn có TRX/JUnit |
| [Lượt sau sửa](https://github.com/vle2362005-collab/Game-Co-Tuong/actions/runs/40474674252), commit [37e8843](https://github.com/vle2362005-collab/Game-Co-Tuong/commit/37e8843f7c9c88f61a9ef00b50f16f1775939c11) | Cả hai check thành công: backend 50 test, frontend 4 test; lint/typecheck/build đạt; có đủ 3 artifact |
| Kiểm tra local | Restore khóa phiên bản, build/test backend; `npm ci`, lint/typecheck/test/build frontend; actionlint và cú pháp Bash đạt |
| Cách ly cấu hình | Test host xác nhận môi trường Testing và CSDL/JWT rỗng; vẫn đạt khi truyền cấu hình CSDL/JWT giả từ môi trường ngoài |

Các kỳ vọng sai đã được khôi phục; không bỏ hoặc tắt test để có kết quả xanh. Có thêm một test bảo vệ cấu hình test host. PR chờ trưởng nhóm review/merge; chưa chuyển Jira Done.

## Giới hạn

Bộ test hiện kiểm tra API health/CORS, dữ liệu engine và model EF; không chứng minh kết nối SQL Server/migration, chức năng chơi hoàn chỉnh hoặc triển khai demo. GCT-99 tiếp tục cấu hình môi trường triển khai sau CI.
