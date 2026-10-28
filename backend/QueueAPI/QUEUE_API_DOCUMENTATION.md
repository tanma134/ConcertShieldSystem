# ConcertShield QueueAPI Documentation

## 1. Mục đích

QueueAPI triển khai Virtual Queue / Fair Admission cho các sự kiện có lượng truy cập lớn.

QueueAPI chịu trách nhiệm:

- Cấu hình thời gian pre-queue.
- Đăng ký user vào pre-queue.
- Tạo queue token cho từng user.
- Đưa user vào hàng chờ Redis.
- Tính vị trí queue từ randomized order.
- Admission user theo admission rate.
- Cấp purchase window 5 phút bằng Redis TTL.
- Theo dõi trạng thái queue.
- Cho phép user rời queue.
- Cho phép Admin/Security Staff revoke, flag, block và unblock session.
- Ghi dữ liệu cấu hình/session/abuse alert vào PostgreSQL.

QueueAPI không thực hiện thanh toán hoặc mua vé. TicketAPI sẽ sử dụng admission state/token sau này.

---

## 2. Kiến trúc tổng thể

```text
Frontend
   |
   v
GatewayAPI :7164
   |
   v
QueueAPI :7310
   |\
   | \
   v  v
Redis  PostgreSQL
```

### Redis

Redis là nguồn dữ liệu runtime cho queue:

- Waiting users.
- Randomized order.
- Active queue token.
- Admission token.
- Admission TTL.
- Admission batch coordination.

### PostgreSQL

PostgreSQL là nguồn dữ liệu persistent cho:

- Queue configuration.
- Queue session.
- Abuse alert.
- Audit/business history.

PostgreSQL không được dùng thay Redis để lock hoặc điều phối admission concurrent.

---

## 3. Các file chính

### Startup

- `Program.cs`
- `appsettings.json`
- `Properties/launchSettings.json`

### Database

- `Data/QueueDbContext.cs`
- `Models/QueueEntities.cs`

### API

- `Controllers/QueueController.cs`
- `Controllers/QueueAdminController.cs`
- `DTOs/QueueDtos.cs`

### Business logic

- `Services/IQueueService.cs`
- `Services/QueueService.cs`
- `Services/QueueAdmissionWorker.cs`

### Redis

- `Services/IRedisQueueStore.cs`
- `Services/RedisQueueStore.cs`

### Timezone

- `Time/QueueTimeZone.cs`

---

## 4. Program.cs

`Program.cs` cấu hình toàn bộ dependency và middleware.

### 4.1 Controllers

```csharp
builder.Services.AddControllers();
```

Cho phép ASP.NET Core phát hiện và chạy các controller trong thư mục `Controllers`.

### 4.2 PostgreSQL

```csharp
builder.Services.AddDbContext<QueueDbContext>(options =>
    options.UseNpgsql(connectionString));
```

QueueAPI dùng EF Core với PostgreSQL.

Database connection nằm trong:

```text
ConnectionStrings:DefaultConnection
```

### 4.3 Redis

```csharp
builder.Services.AddSingleton<IRedisQueueStore, RedisQueueStore>();
```

`RedisQueueStore` là singleton vì `ConnectionMultiplexer` nên được dùng chung trong toàn process.

Không tạo một Redis connection mới cho từng request.

### 4.4 Queue service

```csharp
builder.Services.AddScoped<IQueueService, QueueService>();
```

`QueueService` là business layer. Controller không tự thao tác database hoặc Redis trực tiếp.

### 4.5 Admission worker

```csharp
builder.Services.AddHostedService<QueueAdmissionWorker>();
```

Worker chạy nền và định kỳ admit user từ Redis waiting set.

### 4.6 JWT

QueueAPI xác thực JWT bằng:

- Secret key.
- Issuer.
- Audience.
- Lifetime.
- Role claim.
- Name identifier claim.

User ID được đọc từ:

```text
ClaimTypes.NameIdentifier
hoặc
sub
```

### 4.7 Authorization policies

```text
Customer:
Customer, Organizer, Admin

QueueOrganizer:
Organizer, Admin

QueueAdmin:
Admin, Staff
```

Backend authorization là lớp bảo mật chính. Frontend chỉ ẩn/hiện UI, không được xem là security boundary.

---

## 5. Timezone strategy

QueueAPI hiện dùng timezone `Asia/Ho_Chi_Minh` hoặc fallback Windows:

```text
SE Asia Standard Time
```

Helper nằm trong `Time/QueueTimeZone.cs`.

### 5.1 QueueTimeZone.Now

```csharp
QueueTimeZone.Now
```

Trả về giờ Việt Nam hiện tại dưới dạng `DateTimeKind.Unspecified`.

`Unspecified` là chủ ý vì PostgreSQL hiện dùng:

```text
timestamp without time zone
```

### 5.2 QueueTimeZone.ToVietnam

Dùng khi nhận thời gian cấu hình từ frontend.

- Input `Unspecified`: coi là giờ Việt Nam.
- Input `Utc`: chuyển sang giờ Việt Nam.
- Không phụ thuộc timezone của máy chủ.

### 5.3 QueueTimeZone.AsVietnamOffset

Dùng khi trả timestamp ra API với offset `+07:00`.

Ví dụ:

```text
2026-09-29T21:18:00+07:00
```

### 5.4 PostgreSQL timestamp mapping

Schema hiện tại dùng `timestamp without time zone`, vì vậy `QueueDbContext` phải khai báo rõ:

```csharp
.HasColumnType("timestamp without time zone")
```

Các field bắt buộc phải map rõ:

- `QueueConfiguration.PreQueueStartTime`
- `QueueConfiguration.PreQueueEndTime`
- `QueueConfiguration.CreatedAt`
- `QueueConfiguration.UpdatedAt`
- `QueueSession.CreatedAt`
- `QueueSession.UpdatedAt`
- `AbuseAlert.CreatedAt`

Không thay đổi database sang `timestamp with time zone`.

---

## 6. Database entities

## 6.1 QueueConfiguration

Entity lưu một cấu hình queue cho một event.

Các field quan trọng:

```text
QueueConfigurationId
EventId
PreQueueStartTime
PreQueueEndTime
AdmissionRate
QueueEnabled
FairnessPolicy
CreatedAt
UpdatedAt
```

`EventId` có unique index nên mỗi event chỉ có một queue configuration.

### Ý nghĩa admission rate

Nếu:

```text
AdmissionRate = 10
```

thì worker chỉ admit tối đa một batch 10 user trong mỗi admission window một phút.

---

## 6.2 QueueSession

Một row đại diện cho session queue của một user trong một event.

Unique constraint:

```text
(EventId, UserId)
```

Điều này ngăn một user tạo nhiều session persistent cho cùng một event.

Các field quan trọng:

```text
QueueSessionId
EventId
UserId
SessionId
QueueToken
Status
Position
CreatedAt
UpdatedAt
```

`QueueToken` là token dành riêng cho queue, không phải JWT.

---

## 6.3 AbuseAlert

Lưu các cảnh báo do thao tác security/admin tạo ra.

Ví dụ alert type:

```text
MANUAL_FLAG
SESSION_BLOCKED
```

Severity:

```text
LOW
MEDIUM
HIGH
CRITICAL
```

---

## 7. API DTOs

File `DTOs/QueueDtos.cs` định nghĩa request/response contract.

### ConfigureQueueRequest

```json
{
  "preQueueStartTime": "2026-09-29T21:00:00",
  "preQueueEndTime": "2026-09-29T22:00:00",
  "admissionRate": 10,
  "queueEnabled": true,
  "fairnessPolicy": "RANDOMIZED"
}
```

### QueueStatusResponse

```json
{
  "eventId": 3,
  "queueToken": "...",
  "status": "WAITING",
  "position": 17,
  "totalWaiting": 50,
  "estimatedWaitTimeMinutes": 8,
  "admissionExpiresAt": null,
  "lastUpdated": "2026-09-29T21:18:00+07:00"
}
```

`queueToken` không nên hiển thị đầy đủ trong admin UI hoặc customer UI.

---

## 8. Customer API flow

## 8.1 Configure pre-queue

```http
POST /api/queue/{eventId}/prequeue/configure
```

Authorization:

```text
QueueOrganizer
```

Flow:

1. Convert input time về giờ Việt Nam.
2. Kiểm tra start nhỏ hơn end.
3. Kiểm tra admission rate lớn hơn 0.
4. Tìm configuration theo event.
5. Tạo mới hoặc update configuration.
6. Ghi vào PostgreSQL.
7. Trả configuration response.

---

## 8.2 Register pre-queue

```http
POST /api/queue/{eventId}/prequeue/register
```

Authorization:

```text
Customer
```

Flow:

```text
JWT
  |
  v
Lấy UserId
  |
  v
Đọc queue configuration từ PostgreSQL
  |
  v
Kiểm tra thời gian Việt Nam
  |
  v
Sinh queue token
  |
  v
Ghi QueueSession vào PostgreSQL
  |
  v
Ghi session/token vào Redis
  |
  v
Trả REGISTERED
```

Các lỗi chính:

```text
EVENT_NOT_FOUND
QUEUE_NOT_AVAILABLE
PREQUEUE_NOT_OPEN
PREQUEUE_CLOSED
ALREADY_REGISTERED
UNAUTHORIZED
```

Nếu session cũ là `LEFT` hoặc `EXPIRED`, user được phép đăng ký lại.

---

## 8.3 Join virtual queue

```http
POST /api/queue/{eventId}/join
X-Queue-Token: {queueToken}
```

Flow:

1. Tìm session PostgreSQL.
2. Xác minh user hiện tại sở hữu session.
3. Kiểm tra queue token trong Redis.
4. Nếu user đã admitted, trả `ADMITTED`.
5. Nếu pre-queue chưa đóng, giữ trạng thái `REGISTERED`.
6. Khi pre-queue đóng, worker (hoặc request Join đầu tiên sau thời điểm đóng) finalize randomization đúng một lần cho cycle.
7. Finalization thêm toàn bộ registration hợp lệ vào Redis waiting/order set với cùng score.
8. Tính position từ order đã finalize.
9. Ghi status `WAITING` vào PostgreSQL.
10. Trả queue response.

---

## 8.4 View queue status

```http
GET /api/queue/{eventId}/status
X-Queue-Token: {queueToken}
```

`/position` cũng được hỗ trợ như alias.

Status được lấy chủ yếu từ Redis runtime state.

Nếu admission key còn TTL:

```text
ADMITTED
```

Nếu user còn trong randomized order:

```text
WAITING
```

Nếu chưa join nhưng đã register:

```text
REGISTERED
```

Nếu token Redis không còn:

```text
TOKEN_INVALID
```

---

## 8.5 Leave queue

```http
DELETE /api/queue/{eventId}/leave
X-Queue-Token: {queueToken}
```

Redis xóa:

```text
waiting
order
admission
session
queue token
```

PostgreSQL cập nhật:

```text
Status = LEFT
```

Token cũ không thể dùng lại.

---

## 9. RedisQueueStore

File `Services/RedisQueueStore.cs` là lớp duy nhất thao tác Redis.

### Key structure

```text
queue:event:{eventId}:waiting
queue:event:{eventId}:order
queue:event:{eventId}:session:{userId}
queue:event:{eventId}:token:{token}
queue:event:{eventId}:registrations:{cycleKey}
queue:event:{eventId}:randomized:{cycleKey}
queue:event:{eventId}:admission:{userId}
queue:event:{eventId}:admission-window
```

### Waiting set

`waiting` là Sorted Set dùng để worker lấy người tiếp theo bằng `ZPOPMIN`.

### Order set

`order` lưu randomized priority lâu hơn waiting set.

Mục đích là tránh position bị trùng hoặc thay đổi bất thường khi worker xóa user khỏi waiting set.

### Queue token

Session Redis có TTL khoảng một ngày.

Nếu key hết hạn hoặc bị xóa, token không còn hợp lệ.

### Admission token

Admission key có TTL 300 giây:

```text
queue:event:{eventId}:admission:{userId}
TTL = 300 seconds
```

Redis TTL là nguồn sự thật cho purchase window.

---

## 10. Admission Worker

File `Services/QueueAdmissionWorker.cs` là `BackgroundService`.

Worker lặp khoảng mỗi 5 giây:

```text
Đọc QueueConfiguration
  |
  v
Tìm queue đã qua PreQueueEndTime
  |
  v
Gọi Redis AdmitNextAsync
  |
  v
Admit tối đa admissionRate user
```

`AdmitNextAsync` dùng Redis Lua script:

```lua
local items = redis.call('ZPOPMIN', waitingKey, rate)
for each user do
  redis.call('SET', admissionKey, '1', 'EX', '300')
end
return count
```

Lua script giúp thao tác lấy user và tạo admission key chạy atomic trong Redis.

Admission window key có TTL một phút để tránh admit nhiều batch trong cùng một phút.

---

## 11. Admin API

File `Controllers/QueueAdminController.cs`.

### Xem session

```http
GET /api/queue/admin/sessions
```

Có thể filter:

```http
GET /api/queue/admin/sessions?eventId=3
```

### Revoke token

```http
POST /api/queue/admin/tokens/{token}/revoke
```

Hoặc revoke bằng session ID:

```http
POST /api/queue/admin/sessions/{sessionId}/revoke
```

Kết quả:

```text
Redis state bị xóa
Session status = REVOKED
Token không dùng lại được
```

### Abuse alerts

```http
GET /api/queue/admin/abuse-alerts
GET /api/queue/admin/abuse-alerts/{id}
```

Queue registration also runs two rule-based abuse checks over a rolling
five-minute window:

- More than 2 active sessions for the same user: `MULTIPLE_USER_SESSIONS`.
- More than 3 active sessions for the same event: `EVENT_SESSION_SPIKE`.

Matching alerts are created automatically with `Severity = HIGH`,
`Status = OPEN`, and a `DetectedBehavior` message beginning with `RULE:`.

### Flag/block/unblock

```http
POST /api/queue/admin/sessions/{sessionId}/flag
POST /api/queue/admin/sessions/{sessionId}/block
POST /api/queue/admin/sessions/{sessionId}/unblock
```

Khi block:

- Revoke Redis state.
- Đổi session thành `BLOCKED`.
- Tạo abuse alert.

---

## 12. Frontend integration

### Customer API client

File:

```text
frontend/src/api/queueApi.js
```

Các method:

```javascript
registerPreQueue(eventId)
joinQueue(eventId, token)
getQueueStatus(eventId, token)
leaveQueue(eventId, token)
```

Token được lưu tại:

```text
queue-token:{eventId}
```

### Queue page

File:

```text
frontend/src/pages/VirtualQueuePage.jsx
```

Page xử lý:

- Loading.
- Register.
- Join.
- Waiting.
- Position.
- Estimated wait.
- Polling mỗi vài giây.
- Admission countdown.
- Leave confirmation.
- Expired/revoked/blocked state.

### Customer route

```text
/events/:eventId/queue
```

Route yêu cầu authentication.

### Ticket placeholder

```text
/events/:eventId/tickets
```

Route này chỉ là placeholder cho TicketAPI tương lai.

---

## 13. Admin frontend

Các page chính:

```text
/admin/queue
/admin/queue/abuse-alerts
/admin/queue/abuse-alerts/:alertId
```

Files:

```text
frontend/src/pages/admin/QueueManagementPage.jsx
frontend/src/pages/admin/QueueAbuseAlertsPage.jsx
frontend/src/pages/admin/QueueAbuseAlertDetailPage.jsx
frontend/src/api/queueAdminApi.js
```

Queue management page có:

- Mở pre-queue.
- Xem statistics.
- Xem sessions.
- Revoke token.
- Link tới abuse alerts.

---

## 14. Gateway routes

Gateway cấu hình trong:

```text
backend/GatewayAPI/ocelot.json
```

Các route chính:

```text
/api/queue/{eventId}/prequeue/register
/api/queue/{eventId}/prequeue/configure
/api/queue/{eventId}/join
/api/queue/{eventId}/status
/api/queue/{eventId}/position
/api/queue/{eventId}/leave
/api/queue/admin/{everything}
```

Downstream QueueAPI:

```text
https://localhost:7310
```

Frontend gọi Gateway:

```text
https://localhost:7164/api
```

---

## 15. Load test 50 user

File:

```text
tests/queue_load_test_50.mjs
```

Script dùng:

- Node.js built-in `fetch`.
- JWT HS256 dev token.
- 50 synthetic user IDs.
- 50 request register đồng thời.
- 50 request join đồng thời.
- 50 request status đồng thời.

Chạy:

```powershell
$env:QUEUE_EVENT_ID="3"
$env:QUEUE_USER_OFFSET="40000"
node .\ConcertShieldSystem\tests\queue_load_test_50.mjs
```

Kết quả mong đợi:

```text
REGISTER: 50/50
JOIN:     50/50
STATUS:   50/50
POSITIONS: 50 returned, 50 unique
```

Test trước đó đã đạt:

```text
REGISTER: 50/50
JOIN:     50/50
STATUS:   50/50
POSITIONS: 50 returned, 50 unique
```

---

## 16. Các trạng thái queue

### REGISTERED

User đã đăng ký pre-queue nhưng chưa join waiting set.

### WAITING

User đã vào Redis waiting/order set và đang chờ admission.

### ADMITTED

User có admission key trong Redis.

Purchase window còn hiệu lực trong 300 giây.

### PURCHASE_WINDOW

Có thể dùng như alias nghiệp vụ cho `ADMITTED` nếu TicketAPI yêu cầu.

### LEFT

User đã chủ động rời queue.

### EXPIRED

Admission/purchase window đã hết hạn.

Việc hết TTL được Redis xử lý. DB status cần được đồng bộ khi có request status hoặc job xử lý bổ sung.

### REVOKED

Admin đã thu hồi queue token.

### BLOCKED

Session/user bị security block.

### COMPLETED

Dành cho tương lai khi TicketAPI báo purchase thành công.

---

## 17. Error mapping

Controller chuyển exception thành response code:

```text
EVENT_NOT_FOUND      -> 404
TOKEN_INVALID        -> 401
UNAUTHORIZED         -> 401
PREQUEUE_NOT_OPEN    -> 409
PREQUEUE_CLOSED      -> 409
ALREADY_REGISTERED   -> 409
QUEUE_NOT_AVAILABLE  -> 409
SERVER_ERROR         -> 500
```

Frontend ánh xạ các code này thành thông báo thân thiện, không hiển thị raw exception.

---

## 18. Những giới hạn hiện tại

### 18.1 Randomization timing

Random score hiện được tạo khi user join queue. Chưa có bước gom toàn bộ pre-queue rồi randomize duy nhất tại thời điểm đóng pre-queue.

### 18.2 Database/Redis transaction

PostgreSQL và Redis không nằm trong cùng một distributed transaction.

Registration ghi PostgreSQL trước, sau đó dùng thao tác Redis idempotent theo token/cycle. Nếu Redis thất bại, session PostgreSQL được giữ lại để request retry có thể khôi phục registration; không có distributed transaction giả lập.

### 18.3 DB status sau TTL

Redis tự hết admission TTL sau 300 giây. `QueueAdmissionWorker` và status endpoint cùng dùng cập nhật có điều kiện `ADMITTED -> EXPIRED`, nên xử lý đồng thời là idempotent và không chuyển ngược trạng thái.

### 18.4 Event validation

Queue configuration gọi named `HttpClient` tới `Services:EventAPI`. Event không tồn tại trả `EVENT_NOT_FOUND`; timeout/unavailable trả `503 EVENT_API_UNAVAILABLE`, và không ghi configuration.

### 18.5 Estimated wait

Estimated wait dùng `ceil(position / admissionRate)` phút, tương ứng một admission batch mỗi admission window một phút. Nếu admission rate không hợp lệ thì fallback heuristic cũ; giá trị này chỉ để hiển thị, không tham gia quyết định admission.

### 18.6 TicketAPI

Ticket purchase/payment chưa được triển khai. QueueAPI chỉ cấp admission access.

---

## 19. Thứ tự chạy local

1. PostgreSQL.
2. Redis.
3. QueueAPI:

```powershell
cd "E:\Study Case\Do An\New3\ConcertShieldSystem\backend\QueueAPI"
dotnet run --launch-profile https
```

QueueAPI:

```text
https://localhost:7310
```

Swagger:

```text
https://localhost:7310/swagger
```

4. GatewayAPI:

```powershell
cd "E:\Study Case\Do An\New3\ConcertShieldSystem\backend\GatewayAPI"
dotnet run --launch-profile https
```

5. Frontend:

```powershell
cd "E:\Study Case\Do An\New3\ConcertShieldSystem\frontend"
npm run dev
```

---

## 20. Checklist debug nhanh

### User nhận `PREQUEUE_NOT_OPEN`

Kiểm tra:

- Event đã có queue configuration chưa.
- Giờ hệ thống có phải giờ Việt Nam không.
- `preQueueStartTime` đã tới chưa.
- Database timestamp có đúng giờ Việt Nam không.

### User nhận `TOKEN_INVALID`

Kiểm tra:

- Header `X-Queue-Token`.
- Redis có session key tương ứng không.
- Token có bị leave/revoke không.
- User ID trong JWT có đúng không.

### Frontend không gọi được QueueAPI

Kiểm tra:

- Frontend gọi Gateway `https://localhost:7164/api`.
- Gateway có route queue trong `ocelot.json`.
- QueueAPI đang chạy port `7310`.
- Certificate HTTPS local có được chấp nhận không.

### Admission không chạy

Kiểm tra:

- Redis đang chạy.
- Queue configuration đã qua `PreQueueEndTime` chưa.
- `QueueAdmissionWorker` có log lỗi không.
- `admission-window` key có đang giữ lock một phút không.

---

## 21. Tổng kết

QueueAPI hiện chia rõ trách nhiệm:

```text
Controller
  nhận request, auth, trả HTTP response

QueueService
  xử lý business rule và PostgreSQL

RedisQueueStore
  xử lý runtime queue và atomic Redis operations

QueueAdmissionWorker
  chạy admission định kỳ

QueueDbContext
  map persistent schema

QueueTimeZone
  thống nhất giờ Việt Nam

Frontend
  hiển thị queue state và polling
```

PostgreSQL giữ dữ liệu lâu dài, Redis điều phối queue runtime, còn frontend chỉ hiển thị trạng thái do backend trả về. Frontend không tự randomize position, không tự admit user và không tự gia hạn admission window.
