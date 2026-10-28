# QueueAPI - Các thay đổi và luồng xử lý

Tài liệu này mô tả các thay đổi đã thực hiện trong phạm vi `QueueAPI`.

QueueAPI chỉ chịu trách nhiệm:

- cấu hình Pre-Queue;
- đăng ký queue session;
- randomize thứ tự queue;
- admission access;
- theo dõi trạng thái queue;
- hết hạn admission window;
- revoke/block queue session.

QueueAPI không xử lý mua vé, payment, order, seat allocation hoặc triển khai TicketAPI.

## 1. Tổng quan implementation trước đây

Flow cũ có các đặc điểm chính:

1. Organizer/Admin cấu hình queue theo `EventId`.
2. User gọi register để tạo `QueueSession` trong PostgreSQL và token trong Redis.
3. User gọi join.
4. Redis tạo random score ngay trong `JoinAsync`.
5. User được thêm vào các sorted set `waiting` và `order`.
6. `QueueAdmissionWorker` lấy user bằng `ZPOPMIN` và tạo admission key TTL 300 giây.
7. Status được đọc chủ yếu từ Redis và đồng bộ ngược vào PostgreSQL khi user polling.
8. Estimated wait dùng heuristic cố định dựa trên position.

Các vấn đề chính:

- randomization xảy ra quá sớm và phụ thuộc thời điểm join;
- PostgreSQL và Redis chưa có flow retry/reconciliation rõ ràng;
- admission key có thể expire nhưng DB vẫn giữ `ADMITTED`;
- configure queue chưa validate EventAPI;
- estimated wait chưa dùng admission rate.

## 2. Files đã thay đổi

- `Services/QueueService.cs`
- `Services/RedisQueueStore.cs`
- `Services/IRedisQueueStore.cs`
- `Services/QueueAdmissionWorker.cs`
- `Controllers/QueueAdminController.cs`
- `Controllers/QueueController.cs`
- `Program.cs`
- `appsettings.json`
- `QUEUE_API_DOCUMENTATION.md`

Không thay đổi EventAPI, GatewayAPI, TicketAPI, PaymentAPI, OrderAPI hoặc seat logic.

## 3. Randomization timing

### 3.1. Registration trong Pre-Queue

Khi user register:

1. QueueAPI lấy `QueueConfiguration` từ PostgreSQL.
2. Kiểm tra:
   - queue có enabled hay không;
   - Pre-Queue đã mở hay chưa;
   - Pre-Queue đã đóng hay chưa.
3. Tạo hoặc cập nhật `QueueSession` với:

   ```text
   Status = REGISTERED
   Position = null
   ```

4. Redis lưu:

   ```text
   queue:event:{eventId}:session:{userId}
   queue:event:{eventId}:token:{token}
   queue:event:{eventId}:registrations:{cycleKey}
   ```

Trong thời gian Pre-Queue, user chưa được tạo random score và chưa được đưa vào thứ tự admission.

### 3.2. Cycle key

Mỗi Pre-Queue cycle được nhận diện bằng `PreQueueEndTime`:

```text
yyyyMMddHHmmssfff
```

Ví dụ:

```text
queue:event:10:registrations:20261008090000000
```

Việc gắn registration với cycle giúp tránh việc registration của cycle cũ bị sử dụng cho cycle mới.

### 3.3. Finalize randomization

Sau khi `PreQueueEndTime` đã qua, randomization được trigger bởi một trong hai nơi:

- `QueueAdmissionWorker`;
- request `Join` đầu tiên sau khi Pre-Queue đóng.

Redis sử dụng marker:

```text
queue:event:{eventId}:randomized:{cycleKey}
```

Lua script thực hiện atomic:

1. Kiểm tra marker.
2. Nếu marker đã tồn tại, kết thúc ngay.
3. Lấy toàn bộ user trong registration set của cycle.
4. Tạo score cho từng user.
5. Ghi cùng score vào:

   ```text
   queue:event:{eventId}:order
   queue:event:{eventId}:waiting
   ```

6. Tạo randomization marker.

Nhờ marker và Lua script:

- worker chạy lặp lại không random lại;
- nhiều request Join đồng thời không tạo nhiều thứ tự khác nhau;
- retry an toàn;
- user join sớm trong Pre-Queue không có lợi thế về thứ tự.

### 3.4. Join sau khi randomization

Sau khi Pre-Queue đóng:

1. QueueAPI finalize randomization cho cycle.
2. Kiểm tra queue token trong Redis.
3. Nếu user đã có admission key, trả `ADMITTED`.
4. Nếu user đã có trong `order`, lấy rank làm position.
5. Nếu user chưa có score, trả `REGISTERED`.
6. Cập nhật `QueueSession.Status` và `Position` trong PostgreSQL.

Join không còn tạo random score.

## 4. PostgreSQL và Redis consistency

### 4.1. Register flow

Register sử dụng flow DB-first:

1. Tạo hoặc cập nhật QueueSession trong PostgreSQL.
2. Commit DB.
3. Ghi Redis bằng Lua script idempotent.

Lua script kiểm tra session token hiện tại:

- nếu token khác token request, operation thất bại;
- nếu token giống nhau, retry được xem là cùng registration;
- token mapping và registration set được cập nhật trong cùng Redis script.

### 4.2. Retry behavior

Nếu Redis tạm thời unavailable sau khi PostgreSQL đã commit:

- QueueSession vẫn được giữ trong PostgreSQL;
- request retry có thể dùng lại token/session;
- không tạo duplicate QueueSession;
- không tạo duplicate registration trong Redis.

Thiết kế này không giả lập distributed transaction giữa PostgreSQL và Redis. Thay vào đó, consistency được xử lý bằng:

- idempotent Redis operation;
- token identity;
- cycle-specific registration set;
- retry;
- background reconciliation.

### 4.3. Leave và revoke

Leave/revoke xóa runtime data liên quan:

```text
waiting member
order member
admission key
token key
session key
registration set member
```

Sau đó PostgreSQL được cập nhật trạng thái:

```text
LEFT
REVOKED
```

Block sử dụng revoke Redis state trước khi ghi `BLOCKED` và tạo `AbuseAlert`.

## 5. Admission và TTL reconciliation

### 5.1. Admission flow

`QueueAdmissionWorker` chạy định kỳ mỗi 5 giây:

1. Lấy các queue configuration có `PreQueueEndTime <= now`.
2. Nếu queue enabled:
   - finalize randomization;
   - gọi Redis `AdmitNextAsync`.
3. Redis dùng admission window key để giới hạn batch:

   ```text
   queue:event:{eventId}:admission-window
   ```

4. Lua script dùng `ZPOPMIN` để lấy user có score nhỏ nhất.
5. Với mỗi user được admit:

   ```text
   queue:event:{eventId}:admission:{userId}
   ```

   được tạo với TTL 300 giây.

### 5.2. DB reconciliation

Sau admission, worker lấy các session PostgreSQL đang ở trạng thái `ADMITTED`.

Với từng session:

1. Kiểm tra Redis admission key.
2. Nếu key còn tồn tại, không thay đổi DB.
3. Nếu key đã hết hạn, chạy update có điều kiện:

   ```sql
   WHERE queue_session_id = @id
     AND status = 'ADMITTED'
   ```

4. Chuyển trạng thái thành:

   ```text
   EXPIRED
   ```

Status endpoint cũng xử lý cùng transition nếu user polling trước khi worker kịp chạy.

### 5.3. Race condition

Các tình huống sau được xử lý bằng conditional update và kiểm tra Redis runtime state:

- worker và status endpoint chạy đồng thời;
- admission key vừa expire trong lúc status được đọc;
- worker chạy nhiều lần;
- status request retry.

Transition chỉ đi theo hướng:

```text
ADMITTED -> EXPIRED
```

Không tự động chuyển ngược `EXPIRED` về `ADMITTED`.

## 6. Event validation

Khi Organizer/Admin configure queue:

1. QueueAPI tạo named HttpClient `EventAPI`.
2. HttpClient gọi:

   ```http
   GET /api/events/{eventId}
   ```

3. Nếu EventAPI trả thành công:
   - tiếp tục validate thời gian và admission rate;
   - tạo hoặc update QueueConfiguration.
4. Nếu EventAPI trả `404`:
   - trả `404 EVENT_NOT_FOUND`;
   - không lưu QueueConfiguration.
5. Nếu timeout, network error hoặc status lỗi:
   - log lỗi;
   - trả `503 EVENT_API_UNAVAILABLE`;
   - không làm QueueAPI crash;
   - không lưu QueueConfiguration.

Base URL lấy từ:

```json
{
  "Services": {
    "EventAPI": "https://localhost:7289"
  }
}
```

Có thể override bằng environment variable:

```text
Services__EventAPI=https://event-api-host
```

## 7. Estimated wait

Estimated wait chỉ dùng để hiển thị, không tham gia quyết định admission.

Công thức hiện tại:

```text
estimatedWaitMinutes =
    ceil(position / admissionRate)
```

Ví dụ:

| Position | Admission rate | Estimated wait |
|---:|---:|---:|
| 1 | 10 | 1 phút |
| 10 | 10 | 1 phút |
| 11 | 10 | 2 phút |
| 25 | 10 | 3 phút |

Nếu position không tồn tại, kết quả là `null`.

Nếu admission rate không hợp lệ, service fallback về heuristic cũ và không thực hiện division by zero.

## 8. State transition chính

### Register

```text
Không có session
    -> REGISTERED
```

### Join trong Pre-Queue

```text
REGISTERED
    -> REGISTERED
```

User chưa có position.

### Join sau Pre-Queue

```text
REGISTERED
    -> WAITING
```

Nếu user đã được admit:

```text
REGISTERED hoặc WAITING
    -> ADMITTED
```

### Admission TTL hết hạn

```text
ADMITTED
    -> EXPIRED
```

### User rời queue

```text
REGISTERED / WAITING / EXPIRED
    -> LEFT
```

### Admin revoke/block

```text
REGISTERED / WAITING / ADMITTED
    -> REVOKED hoặc BLOCKED
```

## 9. Redis key summary

### Queue ordering

```text
queue:event:{eventId}:waiting
queue:event:{eventId}:order
```

### Registration/session

```text
queue:event:{eventId}:session:{userId}
queue:event:{eventId}:token:{token}
queue:event:{eventId}:registrations:{cycleKey}
```

### Randomization

```text
queue:event:{eventId}:randomized:{cycleKey}
```

### Admission

```text
queue:event:{eventId}:admission:{userId}
queue:event:{eventId}:admission-window
```

### Revocation

```text
queue:event:{eventId}:revoked:{userId}
```

## 10. Validation đã thực hiện

Đã chạy build:

```powershell
dotnet build .\ConcertShieldSystem\backend\QueueAPI\QueueAPI.csproj --no-restore
```

Kết quả:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Không có database migration mới vì thay đổi không thêm hoặc sửa column PostgreSQL.

## 11. Phạm vi chưa xử lý

Các phần sau vẫn nằm ngoài QueueAPI:

- mua vé;
- payment;
- order;
- seat allocation;
- TicketAPI;
- tạo mock TicketAPI;
- thay đổi Gateway architecture;
- distributed transaction giữa PostgreSQL và Redis.

