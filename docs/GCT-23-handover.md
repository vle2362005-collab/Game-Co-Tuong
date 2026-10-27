# GCT-23 — Tự động build và chạy kiểm thử

## Đầu ra

- [Workflow](../.github/workflows/ci.yml): build/test backend và lint/typecheck/test/build frontend trên PR vào `develop`/`main`, chạy lại sau push.
- [ESLint Vue/TypeScript](../src/frontend/eslint.config.mjs), lockfile cập nhật, SDK thống nhất với hướng dẫn phát triển.
- [Cách chạy, đọc lỗi và dữ liệu kiểm thử](ci.md). Báo cáo TRX/JUnit và bundle frontend có trong Artifacts của mỗi lượt chạy, giữ 14 ngày.

## Kiểm chứng

Đang chạy kiểm tra và thu thập link minh chứng lỗi → sửa trên PR. Chỉ nghiệm thu khi đã có kết quả thực tế và PR được trưởng nhóm review/merge.

## Giới hạn

Bộ test hiện kiểm tra API health/CORS, dữ liệu engine và model EF; không chứng minh kết nối SQL Server/migration, chức năng chơi hoàn chỉnh hoặc triển khai demo. GCT-99 tiếp tục cấu hình môi trường triển khai sau CI.
