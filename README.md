# Felion

Felion là backend quản trị hoạt động câu lạc bộ tích hợp Discord. Dự án được xây dựng theo mô hình modular monolith .NET 10: một process ASP.NET Core duy nhất cung cấp Web API, dashboard nội bộ và NetCord Discord Gateway, sử dụng PostgreSQL làm system of record.

Felion hiện phục vụ một Discord guild duy nhất. Guild ID nằm trong configuration và không được suy luận từ request hay Discord role.

> Trạng thái: **Milestone 9 — Hardening (đang thực hiện)**. Xem [PROJECT_STATUS.md](docs/PROJECT_STATUS.md) để biết save point và công việc tiếp theo.

## Tính năng hiện tại

### Thành viên và định danh

- Quản lý <code>Member</code> với StudentId, họ tên, club Workspace email, Department, Generation, Position và trạng thái Active/Inactive.
- CRUD Member và import CSV/XLSX create-only; toàn bộ file được validate trước khi commit, có lỗi thì không import một phần.
- Department <code>Core</code> được bảo vệ cho Admin/Core; Member thường phải thuộc Department khác <code>Core</code>.
- Đăng nhập Web bằng Google Workspace OAuth và cookie session.
- Email đúng Workspace domain chưa đủ quyền: email phải khớp một Member đang Active.
- Authorization server-side theo các vị trí Admin, Core và Member. Probation candidate không có web access.
- Development/Testing có compatibility header <code>X-Felion-Actor-Member-Id</code>; production từ chối header này.

### Discord và role synchronization

- Verification message với button/modal để người dùng liên kết Discord bằng StudentId.
- Bảo vệ uniqueness trên toàn hệ thống: một StudentId chỉ liên kết một Discord user và ngược lại.
- Mapping role theo các chiều Position, Probation, Department, Generation và ProbationTeam.
- Admin có thể tạo role mới hoặc map role hiện có trong guild được cấu hình.
- Admin có thể gán nhiều role riêng cho từng Member hoặc ProbationCandidate; danh sách gán role là replacement-based và dashboard cũng hiển thị các role tự động đang áp dụng.
- PASS chuyển Discord identity và role assignment từ candidate sang Member rồi đồng bộ role ngay; FAIL xóa role assignment và xếp hàng thao tác kick.
- Role synchronization chạy ngay từ dữ liệu hiện tại sau khi link StudentId hoặc Admin/Core thay đổi dữ liệu; `/role sync` đồng bộ toàn bộ Member/ProbationCandidate đang active và đã link.
- Chỉ thao tác kick sau FAIL còn dùng retryable job với durable backoff; worker không hot-loop khi Discord lỗi.
- Các Discord administration command hiện có:
  - <code>/verification publish</code>
  - <code>/role create</code>
  - <code>/role map</code>
  - <code>/role sync</code>
  - <code>/department create</code>
  - <code>/generation create</code>
- Trước khi có Admin liên kết đầu tiên, Discord server Administrator có thể chạy <code>/verification publish</code> để khởi tạo verification message. Ngoại lệ này không cấp quyền Felion khác.

### Probation

- <code>ProbationCandidate</code> là aggregate riêng, không phải một trạng thái của <code>Member</code>.
- Quản lý candidate, lọc/tìm kiếm theo StudentId, họ tên, Department, Generation, team và status.
- Quản lý ProbationTeam, gán/gỡ candidate và gán nhiều mentor.
- Mentor bắt buộc là Member đang Active; một Member có thể mentor nhiều team.
- Dashboard nội bộ framework-free tại <code>/admin/probation/</code> với:
  - Candidate list/detail, create/edit và chuyển team atomic.
  - Team create/edit, mentor management và candidate assignment/removal.
  - Tab Members & Discord roles để tạo Member và gán role riêng.
  - Xác nhận PASS/FAIL trước khi thực hiện quyết định không thể hoàn tác.

### Evaluation

- Evaluation period với lifecycle <code>Draft -> Open -> Closed</code>.
- Form Peer/Mentor cấu hình được, chỉ gồm hai loại câu hỏi <code>Score</code> và <code>Text</code>.
- Peer evaluation chỉ cho candidate đánh giá candidate khác trong cùng team, không được tự đánh giá.
- Mentor evaluation chỉ cho mentor đánh giá candidate thuộc team mình mentor.
- Chỉ Core/Admin được đọc raw submissions và kết quả; candidate không đọc được điểm, note hay danh tính reviewer.
- Submission lưu snapshot danh tính và câu hỏi để lịch sử vẫn đọc được khi candidate/form thay đổi.
- Gửi lại cùng submission khi period đang Open sẽ cập nhật bản ghi hiện có.
- Core/Admin quyết định PASS/FAIL thủ công theo bulk API; điểm không tự động quyết định kết quả.
- PASS tạo Member mới, sinh Workspace email, chuyển identity/role assignment, sync role rồi archive/delete candidate theo policy.
- FAIL audit, xóa identity/role assignment, queue guild kick và archive/delete theo <code>Probation:FailurePolicy</code>.

### Events & Attendance

- Quản lý Event với lifecycle <code>Draft</code>, <code>Published</code>, <code>RegistrationClosed</code>, <code>InProgress</code>, <code>Completed</code> và <code>Cancelled</code>.
- Event có các <code>EventPosition</code> cục bộ, mỗi position có capacity cứng và tùy chọn yêu cầu một Department.
- Member đăng ký một position ở trạng thái <code>Pending</code>; Core/Admin duyệt hoặc từ chối.
- Core/Admin có thể direct-assign Member và bỏ qua Department eligibility, nhưng không bỏ qua capacity hay <code>AllowMultiplePositions</code>.
- Capacity được bảo vệ concurrency-safe; không có waitlist.
- Check-in thủ công ở cấp Event bởi Core/Admin trong trạng thái <code>InProgress</code> hoặc <code>Completed</code>.
- Check-in không yêu cầu đăng ký trước và idempotent theo <code>(EventId, MemberId)</code>.
- Member xem được published events và lịch sử registration/attendance của mình; Core/Admin xem được lịch sử Member.
- Endpoint hủy registration của Member vẫn là phần planned.

### Cross-cutting

- AuditLog cho các privileged mutation, gồm actor, action, entity, correlation ID và metadata trước/sau khi phù hợp.
- ASP.NET Core Problem Details, correlation ID và structured JSON logging ở non-Development environment.
- Health checks:
  - <code>/health/live</code>: process liveness.
  - <code>/health/ready</code>: PostgreSQL và Discord Gateway khi các dependency tương ứng được cấu hình.
- Built-in OpenAPI tại <code>/openapi/v1.json</code> trong Development.
- Cookie <code>HttpOnly</code>, <code>Secure</code>, <code>SameSite=Lax</code>; HSTS, HTTPS redirection, security response headers và giới hạn request headers.
- Fixed-window abuse limits dùng chung cho HTTP và Discord: 5 lần link/Discord user/10 phút và 10 lần evaluation/reviewer/1 phút.

## Kiến trúc

~~~text
Felion.slnx
src/
  Felion.Host/                 # executable và composition root duy nhất
  Felion.Domain/               # entities, value objects, invariants
  Felion.Application/          # use cases, contracts, authorization abstractions
  Felion.Infrastructure/       # EF Core, PostgreSQL, Google/Discord adapters, imports
  Felion.Api/                  # ASP.NET Core API adapter
  Felion.Bot/                  # NetCord command/component adapter
tests/
  Felion.Domain.Tests/
  Felion.Application.Tests/
  Felion.IntegrationTests/
~~~

HTTP endpoint và NetCord handler chỉ làm nhiệm vụ transport orchestration. Business rules nằm trong Application/Domain services dùng chung cho cả hai transport. <code>Felion.Host</code> là nơi composition; không tách thành microservices.

Chi tiết xem [ARCHITECTURE.md](docs/ARCHITECTURE.md), [PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md) và [DATA_MODEL.md](docs/DATA_MODEL.md).

## Công nghệ và yêu cầu

- .NET SDK <code>10.0.400</code> — được pin trong [global.json](global.json).
- Target framework <code>net10.0</code>, C# 14.
- ASP.NET Core Web API.
- NetCord Gateway và application commands/components.
- EF Core 10 với Npgsql.
- PostgreSQL; [Supabase](https://supabase.com/) chỉ là managed PostgreSQL host.
- Google Workspace OAuth.
- xUnit và GitHub Actions CI.

PostgreSQL cần thiết để chạy các tính năng persistence. Discord và Google OAuth là optional ở local, nhưng bắt buộc tương ứng khi sử dụng Discord integration hoặc Web login.

## Setup local

### 1. Restore solution

~~~powershell
git clone <repository-url>
Set-Location Felion

dotnet --version
dotnet restore Felion.slnx
~~~

SDK nên trả về <code>10.0.400</code> hoặc patch tương thích theo policy trong <code>global.json</code>.

### 2. Cấu hình PostgreSQL

Có thể dùng PostgreSQL local, PostgreSQL container hoặc connection string của Supabase. Ví dụ chạy PostgreSQL 16 local bằng Docker:

~~~powershell
docker run --name felion-postgres -e POSTGRES_DB=felion -e POSTGRES_USER=felion -e POSTGRES_PASSWORD=change-me -p 5432:5432 -d postgres:16-alpine
~~~

Đặt connection string cho process hiện tại:

~~~powershell
$postgresConnection = "Host=localhost;Port=5432;Database=felion;Username=felion;Password=change-me"
$env:ConnectionStrings__Postgres = $postgresConnection
~~~

Tên configuration dạng <code>Section:Key</code> của .NET được truyền qua environment variable bằng dấu <code>__</code>, ví dụ <code>ConnectionStrings:Postgres</code> thành <code>ConnectionStrings__Postgres</code>.

### 3. Cấu hình secrets

Không commit Discord token, Google client secret hoặc database password. <code>.env.example</code> chỉ là template; .NET không tự động load file <code>.env</code>.

Có thể dùng .NET user-secrets:

~~~powershell
dotnet user-secrets init --project src/Felion.Host
dotnet user-secrets set "ConnectionStrings:Postgres" $postgresConnection --project src/Felion.Host
dotnet user-secrets set "Authentication:Google:ClientId" "<google-client-id>" --project src/Felion.Host
dotnet user-secrets set "Authentication:Google:ClientSecret" "<google-client-secret>" --project src/Felion.Host
dotnet user-secrets set "Authentication:Google:WorkspaceDomain" "example.org" --project src/Felion.Host
~~~

Hoặc dùng environment variables:

~~~powershell
$env:Authentication__Google__ClientId = "<google-client-id>"
$env:Authentication__Google__ClientSecret = "<google-client-secret>"
$env:Authentication__Google__WorkspaceDomain = "example.org"
~~~

Google OAuth dùng callback mặc định <code>/signin-google</code>. Khi chạy profile HTTPS local, đăng ký <code>https://localhost:7112/signin-google</code> trong Google Cloud Console. Web login chỉ thành công khi email Google khớp <code>ClubEmail</code> của một Member Active trong database; đúng domain một mình không cấp quyền.

### 4. Apply EF Core migrations

Nếu máy chưa có EF CLI:

~~~powershell
dotnet tool install --global dotnet-ef --version 10.0.12
~~~

Migration dùng <code>FelionDbContextFactory</code>, đọc connection string từ <code>FELION_DESIGN_TIME_CONNECTION</code>:

~~~powershell
$env:FELION_DESIGN_TIME_CONNECTION = $postgresConnection
dotnet ef database update --project src/Felion.Infrastructure/Felion.Infrastructure.csproj --startup-project src/Felion.Host/Felion.Host.csproj
~~~

Migration files nằm tại [src/Felion.Infrastructure/Persistence/Migrations](src/Felion.Infrastructure/Persistence/Migrations). Khi thay đổi persistence model, tạo migration bằng:

~~~powershell
dotnet ef migrations add <MigrationName> --project src/Felion.Infrastructure/Felion.Infrastructure.csproj --startup-project src/Felion.Host/Felion.Host.csproj --output-dir Persistence/Migrations
~~~

### 5. Tạo Admin đầu tiên

Database rỗng chưa có actor để gọi Member-management API. Sau khi apply migrations, chạy one-shot <code>bootstrap-admin</code>:

~~~powershell
$env:Bootstrap__StudentId = "<student-id>"
$env:Bootstrap__FullName = "<full-name>"
$env:Bootstrap__ClubEmail = "<workspace-email>"
$env:Bootstrap__GenerationName = "<generation-name>"
$env:Bootstrap__GenerationCode = "<generation-code>"

dotnet run --project src/Felion.Host -- bootstrap-admin
~~~

Command này tạo Generation nếu cần, tạo active Admin trong Department <code>Core</code> và ghi system audit trong cùng persistence operation. Nó chỉ chạy khi database chưa có Member. <code>ClubEmail</code> phải khớp chính xác email Google Workspace dùng để đăng nhập lần đầu. Sau khi thành công, xóa toàn bộ biến <code>Bootstrap__*</code>.

### 6. Chạy ứng dụng

~~~powershell
dotnet run --project src/Felion.Host --launch-profile https
~~~

Các URL local mặc định:

- HTTPS: <code>https://localhost:7112</code>
- HTTP: <code>http://localhost:5067</code>
- Dashboard: <code>https://localhost:7112/admin/probation/</code>
- OpenAPI Development: <code>https://localhost:7112/openapi/v1.json</code>
- Liveness: <code>https://localhost:7112/health/live</code>
- Readiness: <code>https://localhost:7112/health/ready</code>

Để bật Discord Gateway, cung cấp tối thiểu token và guild ID:

~~~powershell
$env:Discord__Token = "<bot-token>"
$env:Discord__GuildId = "<positive-guild-snowflake>"
dotnet run --project src/Felion.Host --launch-profile https
~~~

Bot cần được cài vào đúng guild với scope <code>bot</code> và <code>applications.commands</code>. Kênh verification cần <code>View Channel</code> và <code>Send Messages</code>; role creation/synchronization cần <code>Manage Roles</code>, đồng thời highest role của bot phải đứng trên mọi role do Felion quản lý. <code>Discord:ApplicationId</code> và <code>Discord:PublicKey</code> chỉ cần cho transport tương ứng; Gateway hiện tại được bật bằng <code>Token</code> và <code>GuildId</code>.

Sau khi bot online, user có Discord server permission <code>Administrator</code> có thể chạy <code>/verification publish</code> trong kênh verification, rồi dùng button để link StudentId của Admin đầu tiên. Các privileged Discord command khác yêu cầu active linked Felion Admin; Discord role không phải nguồn authorization.

## Kiểm thử và quality gates

Các lệnh CI chính:

~~~powershell
dotnet restore Felion.slnx
dotnet format Felion.slnx --verify-no-changes --no-restore
dotnet build Felion.slnx --configuration Release --no-restore
dotnet test Felion.slnx --configuration Release --no-build
~~~

PostgreSQL integration tests được bỏ qua nếu không có <code>FELION_POSTGRES_TEST_CONNECTION</code>. Khi bật, test fixture tạo/migrate/xóa database ngẫu nhiên dạng <code>felion_test_*</code>, vì vậy PostgreSQL user phải có quyền create/drop database:

~~~powershell
$env:FELION_POSTGRES_TEST_CONNECTION = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<password>"
dotnet test tests/Felion.IntegrationTests/Felion.IntegrationTests.csproj --configuration Release
~~~

CI hiện tại chạy trên GitHub Actions với PostgreSQL 16 service; xem [ci.yml](.github/workflows/ci.yml). Tài liệu API và Discord command đầy đủ nằm ở [API_AND_COMMANDS.md](docs/API_AND_COMMANDS.md).

## Deploy

Repository hiện có CI restore/format/build/test nhưng chưa có Dockerfile, Docker Compose file hoặc automated deployment workflow. Cách deploy được hỗ trợ hiện tại là publish <code>Felion.Host</code> thành artifact rồi chạy như một ASP.NET Core service phía sau reverse proxy.

### Checklist hạ tầng

1. PostgreSQL production hoặc Supabase project.
2. Discord application/bot, đúng guild ID và permissions cần thiết.
3. Google OAuth client với callback <code>https://&lt;production-host&gt;/signin-google</code>.
4. HTTPS termination ở application hoặc reverse proxy.
5. Secret/configuration store cho connection string, Discord token, Google credentials và bootstrap values dùng một lần.
6. Process supervisor phù hợp với nền tảng triển khai (systemd, Windows Service, App Service hoặc container platform).

### Publish artifact

Chạy quality gates trước khi publish:

~~~powershell
dotnet restore Felion.slnx
dotnet format Felion.slnx --verify-no-changes --no-restore
dotnet build Felion.slnx --configuration Release --no-restore
dotnet test Felion.slnx --configuration Release --no-build
dotnet publish src/Felion.Host/Felion.Host.csproj --configuration Release --output ./artifacts/publish --no-restore
~~~

Copy nội dung <code>artifacts/publish</code> lên máy chạy production. Với framework-dependent publish, máy đích cần cài .NET 10 ASP.NET Core runtime tương thích.

### Migration và bootstrap khi deploy

Apply migrations trước khi start web process. Có thể chạy <code>dotnet ef database update</code> từ checkout/build environment có source project và quyền truy cập production database, hoặc đưa migration bundle vào release process. Không đặt mật khẩu database trong repository.

Sau migrations, dùng chính artifact đã publish để bootstrap Admin một lần:

~~~powershell
$env:ConnectionStrings__Postgres = "<production-connection-string>"
$env:Bootstrap__StudentId = "<student-id>"
$env:Bootstrap__FullName = "<full-name>"
$env:Bootstrap__ClubEmail = "<workspace-email>"
$env:Bootstrap__GenerationName = "<generation-name>"
$env:Bootstrap__GenerationCode = "<generation-code>"

dotnet Felion.Host.dll bootstrap-admin
~~~

Xóa các biến <code>Bootstrap__*</code> ngay sau khi command thành công. <code>bootstrap-admin</code> không chạy tự động khi web khởi động và không có HTTP endpoint tương ứng.

### Chạy production

Ví dụ cấu hình process chạy sau reverse proxy:

~~~powershell
$env:DOTNET_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://127.0.0.1:8080"
$env:AllowedHosts = "felion.example.com"
$env:ConnectionStrings__Postgres = "<production-connection-string>"
$env:Discord__Token = "<bot-token>"
$env:Discord__GuildId = "<guild-id>"
$env:Authentication__Google__ClientId = "<google-client-id>"
$env:Authentication__Google__ClientSecret = "<google-client-secret>"
$env:Authentication__Google__WorkspaceDomain = "example.org"

dotnet Felion.Host.dll
~~~

Reverse proxy phải dùng HTTPS public, route traffic vào port nội bộ của Host và kiểm tra:

- <code>/health/live</code> cho liveness.
- <code>/health/ready</code> cho readiness trước khi nhận traffic.
- Forwarded headers chỉ được tin cậy từ proxy/network đã allowlist và phải được xử lý trước HTTPS redirection.
- <code>AllowedHosts</code> phải là hostname production, không dùng <code>*</code> như local default.
- Không log hoặc expose secret/token.

<code>DiscordSyncJob</code> chỉ retry thao tác kick sau FAIL qua process restart; role synchronization chạy trực tiếp và không đi qua job. Rate limiter hiện giữ state trong memory của một process. Không mở rộng thành nhiều replica nếu chưa thay bằng shared limiter store.

## Cấu hình chính

| Configuration key | Mục đích |
| --- | --- |
| <code>ConnectionStrings__Postgres</code> | PostgreSQL connection string; bắt buộc cho persistence production |
| <code>Discord__Token</code> | Bật NetCord Gateway khi có giá trị |
| <code>Discord__GuildId</code> | Guild duy nhất Felion được phép quản lý |
| <code>Discord__ApplicationId</code> | Application ID nếu transport tương ứng cần dùng |
| <code>Discord__PublicKey</code> | Public key cho HTTP interactions nếu được bật |
| <code>Authentication__Google__ClientId</code> | Google OAuth client ID |
| <code>Authentication__Google__ClientSecret</code> | Google OAuth client secret |
| <code>Authentication__Google__WorkspaceDomain</code> | Domain restriction/hint; không tự cấp quyền |
| <code>Probation__SuccessPolicy</code> | <code>Archive</code> hoặc <code>Delete</code>, mặc định <code>Archive</code> |
| <code>Probation__FailurePolicy</code> | <code>MarkInactive</code> hoặc <code>Delete</code>, mặc định <code>MarkInactive</code> |
| <code>Evaluation__DefaultScoreMin</code> | Score minimum mặc định |
| <code>Evaluation__DefaultScoreMax</code> | Score maximum mặc định |
| <code>Evaluation__DefaultTextMaxLength</code> | Text answer length mặc định |
| <code>AllowedHosts</code> | Hostname được phép trong production |
| <code>Bootstrap__*</code> | Thông tin Admin đầu tiên; chỉ dùng cho one-shot bootstrap rồi xóa |

File mẫu: [.env.example](.env.example) và [appsettings.json](src/Felion.Host/appsettings.json). Các giá trị trong hai file này không phải production secrets.

## Tài liệu liên quan

- [Product specification](docs/PRODUCT_SPEC.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Data model](docs/DATA_MODEL.md)
- [API và Discord commands](docs/API_AND_COMMANDS.md)
- [Development guide](docs/DEVELOPMENT.md)
- [Project status](docs/PROJECT_STATUS.md)
- [TODO](docs/TODO.md)
- [Approved decisions](docs/DECISIONS.md)
