# Chạy ConcertShield bằng Docker

## Yêu cầu

- Docker Desktop đang chạy với Linux containers.
- Docker Compose v2.

## Khởi chạy

Từ thư mục gốc repository:

```powershell
Set-Location "E:\Study Case\Do An\New7\ConcertShieldSystem"
if (!(Test-Path .env)) { Copy-Item .env.example .env }
# Sửa .env và tạo secret riêng. Nếu nhập database đang có, làm theo mục
# "Nhập dữ liệu từ PostgreSQL hiện có" trước khi khởi chạy toàn bộ dịch vụ.
docker compose up --build -d
docker compose ps
```

Mở frontend tại `http://localhost:8088`. Có thể đổi cổng bằng `FRONTEND_PORT` trong `.env`.
Gateway và các API chỉ được truy cập nội bộ trong mạng Compose; PostgreSQL chỉ bind vào
localhost của máy chạy Docker.

## Database

Volume `postgres_data` giữ dữ liệu qua các lần khởi động. Mặc định, lần khởi chạy đầu
tạo database và nạp schema mẫu trong repository. Các SQL bootstrap Authentication/Event
có thao tác `DROP TABLE`, vì vậy **không dùng chế độ mặc định này để thay thế database
hiện có**.

### Nhập dữ liệu từ PostgreSQL hiện có

Quy trình này tạo bản dump tại chỗ, không gửi database ra ngoài. Script dùng Docker làm
PostgreSQL client nên không cần cài `psql`/`pg_dump` trên Windows. Mặc định nó kết nối
`host.docker.internal:5432`, user `postgres`, tự lấy phiên bản server và liệt kê mọi
database không phải template trên server.

```powershell
.\database\docker\export-existing-databases.ps1
```

Script sẽ hỏi mật khẩu nguồn một lần. Các file `.dump` chứa dữ liệu
database, được lưu trong `database/import` và loại khỏi Git; không gửi các file này lên
chat hoặc commit vào repository. Nếu PostgreSQL từ chối kết nối từ Docker, cần cho phép
kết nối từ Docker Desktop trong `listen_addresses`/`pg_hba.conf` hoặc dùng chức năng
Backup của pgAdmin để xuất từng database dạng custom (`.dump`) vào `database/import`.
Nếu server của bạn không ở máy này, truyền host/port/user cho script, ví dụ:
`.\database\docker\export-existing-databases.ps1 -HostName db.example.local -Port 5432 -Username postgres`.

Trong `.env`, đặt `RESTORE_EXISTING_DATABASES=true`, `POSTGRES_PORT=5433`, đồng thời
dùng `POSTGRES_VERSION` khớp major version PostgreSQL nguồn và secret PostgreSQL mới;
không dùng lại các secret đã dán vào chat. Sau đó chạy:

```powershell
docker compose up -d postgres
docker compose --profile import run --rm db-restore
docker compose up --build -d
docker compose ps
```

Restore thay thế các object trùng tên **bên trong PostgreSQL container**; không sửa
PostgreSQL nguồn trên Windows. Không chạy restore vào volume Docker đang chứa dữ liệu
cần giữ. Script export lấy danh sách trực tiếp từ server; dump filename giữ nguyên tên
database nguồn.

Với PostgreSQL 18, Compose mount volume mới `postgres_data_v18` tại `/var/lib/postgresql`
theo yêu cầu của image chính thức. Volume cũ `postgres_data` được giữ nguyên, không bị
xóa hoặc chuyển đổi.

Để tạo schema mẫu thay vì nhập database cũ, đặt `RESTORE_EXISTING_DATABASES=false`.
Compose dùng database `payment_db` để khớp với tên database hiện có; appsettings cũ
của PaymentAPI dùng `04_payment_db` nhưng được Compose ghi đè.

Gateway vẫn chứa một số route cũ tới các dịch vụ Chat, Social, Withdrawal và Chatbot
không có project tương ứng trong repository hiện tại; các route đó không hoạt động cho
đến khi bổ sung các API còn thiếu.

## Vận hành

```powershell
docker compose logs -f
docker compose logs -f gateway-api
docker compose stop
docker compose down
```

`docker compose down` giữ dữ liệu. `docker compose down -v` sẽ xóa volume PostgreSQL,
Redis và file KYC; chỉ dùng khi chủ động muốn xóa toàn bộ dữ liệu.

Đây là cấu hình khởi chạy self-hosted, không tự cấu hình HTTPS/domain, SMTP, VNPay,
Cloudinary, VNPT eKYC hoặc OAuth credentials. Trước khi public lên Internet, hãy thay
các giá trị `.env`, cung cấp credentials dịch vụ ngoài, cấu hình HTTPS/reverse proxy và
luân chuyển các secret đã từng được lưu trong file cấu hình của repository.
