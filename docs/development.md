# Hướng dẫn phát triển — GCT-21

## 1. Bộ công cụ chung

| Công cụ | Phiên bản / quy ước |
|---|---|
| .NET SDK | `10.0.403`, ghi trong `global.json`; chỉ roll forward bản patch cùng feature band, không dùng preview |
| ASP.NET Core | `10.0.14` đi cùng SDK đã kiểm tra; target `net10.0` |
| Node.js | `24.22.1` trong `.nvmrc`; frontend chấp nhận `>=24.14.0 <25` |
| npm | Kiểm tra bằng `11.9.0`; chấp nhận npm 11.x |
| Vue / TypeScript | `3.5.44` / `5.9.3` |
| Vite / Vitest | `8.3.5` / `5.0.5` |
| Backend test | xUnit `2.9.3`, Test SDK `17.14.1`, Mvc.Testing `10.0.14` |
| Database khi triển khai GCT-26 | Dự kiến SQL Server 2022 Developer, EF Core 10.x; GCT-26 chốt package/migration. Bộ khung chưa kết nối DB. |

Tải SDK từ [Microsoft .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) và Node từ [Node.js](https://nodejs.org/en/download). Kiểm tra `dotnet --version`, `node --version`, `npm --version`. Với nvm có thể dùng `nvm install 24.22.1` và `nvm use 24.22.1`. SDK khác version báo lỗi thì cài đúng SDK; không tự sửa global.json cho riêng máy mình.

Nguồn kỹ thuật: [metadata SDK chính thức](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [Vite guide](https://vite.dev/guide/), [Vue TypeScript](https://vuejs.org/guide/typescript/overview). Phiên bản trong repo là mốc nhóm kiểm tra, không tự cập nhật mỗi lần clone.

## 2. Clone và chạy

```sh
git clone https://github.com/vle2362005-collab/Game-Co-Tuong.git
cd Game-Co-Tuong
git switch develop
git pull --ff-only origin develop
```

Trước khi PR GCT-21 được merge, người review dùng `git switch feature/GCT-21-repository` để chạy đầu ra của task. Nhánh develop khi mới tạo chỉ có nội dung của main hiện tại; không giả định đã có source GCT-21 trên đó.

Chạy hai terminal như README. Lệnh kiểm tra dùng `-m:1` và `UseSharedCompilation=false` để không phụ thuộc MSBuild worker/compiler server; phù hợp cả môi trường kiểm tra hạn chế IPC và máy ít tài nguyên. Sau build Release, lệnh run dùng `--no-build` đúng cấu hình Release. Khi sửa backend, build lại hoặc dùng `dotnet watch --project src/backend/GameCoTuong.Api run --launch-profile http` trên máy phát triển thông thường.

Mặc định: frontend `http://localhost:5173`; API `http://localhost:5000`; Vite preview của bản build `http://localhost:4173`. Preview chỉ xem bundle; muốn bấm kiểm tra máy chủ ở preview, thêm origin 4173 vào CORS. Không đổi sang 5174 khi cổng bận: Vite dùng `strictPort` để báo rõ, tránh lệch CORS.

Kiểm tra riêng API trên PowerShell:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

Hoặc trên Bash:

```sh
curl --fail http://localhost:5000/health
```

Kỳ vọng JSON `{"status":"Healthy","service":"game-co-tuong-api"}`. Endpoint chỉ kiểm tra tiến trình HTTP. Chưa kiểm tra DB, chưa đăng nhập, mở phòng hoặc xác nhận luật cờ.

## 3. Cấu hình từng máy

Chạy mặc định không cần file riêng. Khi cần đổi địa chỉ API, sao chép `src/frontend/.env.example` thành `src/frontend/.env.local`, sửa URL rồi khởi động lại Vite; khi build lại phải chạy `npm run build`. Tất cả biến `VITE_` nằm trong bundle công khai: không đặt mật khẩu, signing key hay connection string ở frontend.

Backend đọc theo thứ tự ưu tiên tăng dần: cấu hình mặc định theo môi trường → `appsettings.Local.json` → user-secrets ở Development → biến môi trường → tham số dòng lệnh. Có thể sao chép `appsettings.Local.example.json` thành `appsettings.Local.json` trong project API. Cấu hình DB/JWT trong file mẫu dành cho task sau; hiện chưa được dùng. Chuỗi kết nối dùng Windows Integrated Security, không chứa mật khẩu; SigningKey trống có chủ đích, không có key mặc định để dùng nhầm.

Khi GCT-26/28 cần dữ liệu bí mật, dùng user-secrets ở máy phát triển hoặc biến môi trường. Các file `.env*`, `appsettings.Local.json`, chứng thư/khóa riêng đã bị `.gitignore` loại trừ; file `.env.example` và `appsettings.Local.example.json` vẫn theo dõi trong Git. File cấu hình Local không được copy khi publish.

Đổi API sang cổng 5001 trên PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/backend/GameCoTuong.Api -c Release --no-build --no-restore --no-launch-profile --urls http://localhost:5001
```

Sau đó sửa `VITE_API_BASE_URL=http://localhost:5001`. Nếu đổi origin frontend, đặt `BrowserClient__AllowedOrigins__0` tương ứng hoặc thêm vào appsettings.Local.json. Origin phải có scheme + host + port, không có đường dẫn, dấu `/` cuối, ký tự `*` hoặc thông tin đăng nhập. API chỉ cho phép hai origin phát triển mặc định, không bật cookie credentials. CORS là quy tắc trình duyệt, không thay thế xác thực. Xác thực và SignalR được triển khai ở task riêng.

## 4. Phụ thuộc và lockfile

- `Api → Application + Infrastructure`; `Application → Domain + Engine`; `Infrastructure → Application + Domain`; `Engine → Domain`; Domain không tham chiếu project khác.
- Domain và Engine không dùng ASP.NET Core, EF Core, HTTP hoặc SignalR. Luật đi quân bắt đầu ở GCT-42 trở đi. Các AssemblyMarker đánh dấu project nền, chưa có engine hoạt động.
- EF Core/DbContext/migration đặt trong Infrastructure tại GCT-26. REST/Hub gọi use case chung trong Application theo GCT-20; chưa tạo các endpoint nghiệp vụ giả.
- GCT-76 dùng `MatchReconnectService` cùng `ReconnectTimeoutRegistry`. Deadline lưu trên `RoomMembership`/`Match` là dữ liệu chuẩn; `ProcessRoomTimeouts` và `ProcessMatchTimeouts` xử lý các deadline với quy tắc `now >= deadline`. Khi bổ sung Hub/worker, cần gọi các hàm xử lý này định kỳ và sau reconnect, đồng thời lưu thay đổi trong transaction. Kiểm tra logic bằng `dotnet test tests/backend/GameCoTuong.Foundation.Tests --filter FullyQualifiedName~ReconnectTimeoutTests`.
- Package .NET đặt version tập trung tại `Directory.Packages.props`; `packages.lock.json` được commit theo từng project. Lần clone dùng `dotnet restore --locked-mode`. Khi đổi package, sửa props rồi `dotnet restore --force-evaluate`, review lockfile và chạy lại build/test.
- Frontend pin trực tiếp dependency và commit `package-lock.json`; dùng `npm ci`. Khi đổi package dùng `npm install <tên>@<version> --save-exact` hoặc `-D`, review diff và kiểm tra lại. Không xóa lockfile để né lỗi restore.
- Không commit bin, obj, node_modules, dist hoặc TestResults. Không sửa solution để trỏ tới đường dẫn tuyệt đối trên máy mình.

## 5. Luồng làm việc chung

Sau khi nền tảng đã merge vào develop:

```sh
git switch develop
git pull --ff-only origin develop
git switch -c feature/GCT-42-engine-model
```

Làm đúng phần việc, chạy kiểm tra, rồi commit có mã Jira và push nhánh:

```sh
git add src tests docs
git commit -m "GCT-42: add board and piece model"
git push -u origin feature/GCT-42-engine-model
```

Tạo PR vào `develop` và gắn link trong Jira. Trưởng nhóm review/merge; main nhận bản ổn định qua PR sau. Trước khi tiếp tục một nhánh cũ, fetch rồi merge `origin/develop` vào nhánh của mình, xử lý conflict và kiểm tra lại. Không force-push hoặc đẩy thẳng main/develop để giải quyết conflict.

## 6. Lỗi thường gặp

| Biểu hiện | Cách xử lý |
|---|---|
| SDK not found / SDK khác feature band | Cài SDK đúng global.json, chạy từ thư mục repo |
| npm báo engine không hợp lệ | Dùng Node 24 được chỉ định và npm 11.x |
| Restore không tải được package | Kiểm tra Internet/proxy, NuGet.org và npm registry; không bỏ qua lỗi rồi báo build thành công |
| API báo address already in use | Dừng tiến trình cũ hoặc đổi cổng và cập nhật URL frontend |
| Bấm kiểm tra máy chủ thất bại | Mở `/health` trước, kiểm tra URL, CORS và origin thực tế; dùng localhost:5173 như README |
| Frontend vẫn dùng URL cũ | Khởi động lại Vite; production bundle cần build lại sau đổi env |
| Không thấy source ở develop | PR GCT-21 chưa merge; reviewer chạy nhánh feature, thành viên cập nhật develop sau merge |

HTTPS, reverse proxy, health DB, secrets triển khai và vận hành do task triển khai xử lý. Khởi tạo hiện tại chạy HTTP trên loopback để không phụ thuộc chứng thư phát triển của từng máy.
