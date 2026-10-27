# CI — GCT-23

[Workflow](../.github/workflows/ci.yml) chạy trên mọi PR vào `develop`/`main` và mỗi push vào hai nhánh đó. Không lọc theo đường dẫn: sửa tài liệu hoặc cấu hình vẫn có đủ checks. Commit mới trong cùng PR hủy lượt cũ đang chạy. Sau khi workflow vào nhánh mặc định, có thể chạy thủ công từ tab Actions.

## Các bước kiểm tra

| Check | Nội dung | Kết quả tải về |
|---|---|---|
| Backend | Restore `--locked-mode`, build Release, toàn bộ xUnit | `backend-test-results`: báo cáo TRX |
| Frontend | `npm ci`, ESLint, `vue-tsc`, Vitest, build Vite | `frontend-test-results`: JUnit XML; `frontend-dist`: bundle |

SDK đọc từ `global.json`, Node từ `.nvmrc`. Cache NuGet/npm phụ thuộc lockfile; luôn restore lại, không dùng cache thay bước kiểm tra. Action được ghim bằng commit SHA, runner là `ubuntu-24.04`. Bước lỗi giữ exit code và làm check thất bại; không dùng `continue-on-error`. Báo cáo test được tải lên kể cả khi test lỗi; nếu chưa tới bước test thì xem log của bước lỗi. Artifact giữ 14 ngày.

Mở **PR → Checks → Backend/Frontend → Details** để đọc từng bước. Hoặc mở **Actions → GCT CI → lượt chạy**; phần **Artifacts** chứa báo cáo. `frontend-dist` là bản build để kiểm tra, chưa phải bản đã triển khai.

## Dữ liệu và cấu hình

- Backend CI và `TestApiFactory` dùng môi trường `Testing`, không nạp user-secrets Development. Test host ghi đè cấu hình CSDL/JWT bằng giá trị rỗng; có test xác nhận môi trường và cấu hình này. Checkout sạch không có `appsettings.Local.json` hay `.env.local` của thành viên.
- Test engine tạo bàn/quân riêng trong bộ nhớ; test API dùng test host trong tiến trình; test frontend giả lập `fetch` và dọn mock sau mỗi ca. Test EF hiện chỉ đọc model/seed bằng dữ liệu demo, không mở kết nối SQL Server.
- CI không cần repository secret và không nhận cấu hình production. Token chỉ có `contents: read`; checkout không lưu credential. Chạy bằng `pull_request`, không dùng `pull_request_target` để thực thi mã PR.
- Khi bổ sung test CSDL thật, tạo SQL Server/database riêng cho từng lượt chạy và dọn sau test. Không trỏ CI vào CSDL demo/production; mật khẩu/JWT/TURN phải ở biến môi trường/secret phía backend. Biến `VITE_*` là công khai trong bundle.

## Chạy tương đương trên máy

Từ gốc repo chạy `./scripts/verify.sh` hoặc `./scripts/verify.ps1`. Hai script có cùng bước lint, build và test với CI. Có thể kiểm tra riêng frontend bằng `npm --prefix src/frontend run lint`, `run typecheck`, `test` và `run build`.

Khi đổi package, cập nhật và review lockfile; không bỏ `--locked-mode`/`npm ci` để né lỗi. Khi sửa lỗi, push lên cùng nhánh PR để CI chạy lại; **Re-run failed jobs** chỉ dùng cho lỗi hạ tầng tạm thời.

## Minh chứng lỗi → sửa

Trên PR GCT-23, một commit kiểm chứng tạm thời sửa kỳ vọng trong test health backend và frontend thành giá trị sai. Cả hai check phải thất bại tại bước test, có báo cáo lỗi. Commit tiếp theo khôi phục kỳ vọng đúng; toàn bộ check phải thành công. File cuối của PR không giữ thay đổi gây lỗi.

Link commit và lượt chạy được ghi ở [bàn giao GCT-23](GCT-23-handover.md). Các check chạy tự động không thay thế review/merge của trưởng nhóm; quy tắc bắt buộc check trước merge chỉ có hiệu lực khi được cấu hình trên GitHub.

Nguồn: [GitHub workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), [setup-dotnet](https://github.com/actions/setup-dotnet), [setup-node](https://github.com/actions/setup-node), [Vue ESLint](https://eslint.vuejs.org/user-guide/), [Vitest reporters](https://vitest.dev/guide/reporters.html).
