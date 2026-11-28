# Phân tích chống Bot / Gian lận — ConcertShield (đối chiếu source, Bước 1)

Nguồn đọc: `backend/TicketAPI/Risk/*`, `backend/AuthenticationAPI/Risk/*`,
`backend/AdminAPI/Services/Risks/*`, Controllers (`RiskPolicyAdmin`, `RiskBlockAdmin`,
`Appeal`, `AppealAdmin`, `FraudAlertAdmin`, `FraudCaseAdmin`, `InternalFraudAlert`,
`InternalRiskCheck`, `RiskDecision`), `TicketAPI/Controllers/{Holds,Order,TicketReturns}Controller.cs`,
`AuthenticationAPI/Controllers/{Authentication,Kyc}Controller.cs`, `Program.cs` + `appsettings.json` các API,
`launchSettings.json`.

Nguyên tắc test: điểm số từng rule nằm trong DB (`risk.rule.<CODE>`), đọc bằng
`GET /api/admin/risk/policy` lúc chạy test. **Không hardcode điểm trong test.**
Bảng dưới ghi điều kiện kích hoạt + hành động theo code; cột điểm để trống, điền từ policy thật.

## 1. Cấu hình và thang quyết định (đã xác minh)

`RiskConfig.cs` defaults: `medium 30 / high 60 / critical 80`, `CapGroup 40`,
`CapTrust 30`, `BonusCombo 10` khi `groupCount >= 3`, `CriticalMinGroups 2`.
Per-type override (`risk.threshold.bot.*`, `risk.threshold.fraud.*`) nếu null thì fallback về chung.

`RiskScoreCalculator.Decide` (đã đọc code, khớp đặc tả):
- Dedup theo code (lấy |score| lớn nhất), cộng theo nhóm và cap 40/nhóm, combo +10,
  trừ trust tối đa 30. Trust tắt khi có hardRule hoặc rule bất kỳ `DisableTrust`.
- `HR_WL` → Allow/Low. `HR_BL01` (thẻ→Fraud/ACCOUNT), `HR_BL02` (thiết bị→Bot/DEVICE),
  `HR_BL03` (IP→Bot/IP): block Critical score 100 + leo thang.
- Bot: Medium → CHALLENGE CAPTCHA (428 `RISK_CHALLENGE`); High → BLOCK SESSION+IP
  trong `BotHighBlockHours` (mặc định 1h); Critical → BLOCK SESSION+IP+DEVICE.
  Critical mà `groupCount < 2` → hạ High, và High-bot 1 nhóm → CHALLENGE CAPTCHA
  (`DOWNGRADED_SINGLE_GROUP`).
- Fraud: Medium → CHALLENGE OTP (428); High → HOLD (403 `RISK_HOLD`); Critical → BLOCK ACCOUNT.
  Critical 1 nhóm → HOLD (`DOWNGRADED_SINGLE_GROUP`).
- `HR_VIP`: fraud + (`IsVipCustomer` hoặc `OrderAmount >= risk.vip.min_order_amount`
  mặc định **20.000.000**) + `score >= ThresholdHigh` → ép High/HOLD.
- Leo thang (đếm SYSTEM non-shadow BLOCK trong `EscalationWindowDays` = 30d,
  khớp user HOẶC device HOẶC (no-user + ip)): lần 1 = `BlockHoursLevel1` 24h,
  lần 2 = `BlockHoursLevel2` 72h, lần 3 → HOLD + `RequiresManualReview`
  (`ESCALATION_MANUAL_REVIEW`). Hard rule cũng leo thang (trừ level 3 mới thành Hold).
- Validate policy PUT: `0 < medium < high < critical <= 100`;
  `1 <= L1 < L2 <= 720`; `ComboMinGroups >= 2`, `CriticalMinGroups >= 2`;
  reason 10–500 ký tự; trùng key/không đổi giá trị → 400/409; sai version → 409;
  `risk.rule_version` tự bump nếu không đổi tay. Cache config TTL **60s**
  → đổi policy xong test phải chờ/retry khi đọc lại.
- `risk.mode` mặc định **SHADOW**: engine vẫn ingest + ghi decision/alert nhưng
  `RiskGuard` trả Allow (`IsShadow=true`). Shadow BLOCK/HOLD **vô hình** với
  enforcement/restore/appeal/`decisions/me`/`HasActive*`. Test chặn phải PUT ENFORCE
  rồi **khôi phục giá trị gốc ở teardown**.
- Fail-open: `IngestAsync` null (AdminAPI lỗi) → Allow. Chỉ ghi "Cần xác nhận",
  không test bằng cách tắt service.

## 2. Header risk engine đọc (đã xác minh, khớp đặc tả)

`RiskRequestContext.From`: `User-Agent`, `X-Device-Fp` (trim, cap 128, rỗng→null),
`X-Session-Id` (idem), `X-Webdriver` (`"1"`→true), `X-Had-Input` (`1/0`, else null),
và `Request.Headers` cho `sec-ch-ua`, `sec-fetch-site`, `Accept-Language`,
`Accept-Encoding`, `Origin`/`Referer`. IP: `X-Forwarded-For` entry đầu → fallback
`RemoteIpAddress`; parse fail → null; loopback → `127.0.0.1`.

UA Playwright chứa `playwright`, headless chứa `HeadlessChrome` → dính `BOT_E01`.
Test happy-path **phải** ghi đè UA Chrome thật + đủ `sec-ch-ua`, `sec-fetch-site`,
`Accept-Language`, `Accept-Encoding`, `Origin ∈ Risk:AllowedOrigins`
(mặc định `http(s)://localhost:5173`), `X-Had-Input: 1`, `X-Device-Fp`/`X-Session-Id`
riêng mỗi test. `SlidingCounter` là **in-memory单体** (`ConcurrentDictionary`, purge
>5000 keys) → restart service reset đếm; test V01/V04 phải tính lại từ đầu.

## 3. Rule bot (TicketAPI + AuthenticationAPI dùng chung detector)

| Mã | Nhóm | Điều kiện kích hoạt (code) | Ngưỡng/đếm |
|---|---|---|---|
| BOT_H01 | header | UA rỗng hoặc chứa `curl, wget, python-requests, python-urllib, go-http-client, axios, okhttp, httpclient, postman, java/, libwww-perl, node-fetch, insomnia` (case-insensitive) | không đếm |
| BOT_H02 | header | UA có `Chrome`/`Edg/` nhưng thiếu `sec-ch-ua` HOẶC `sec-fetch-site` | — |
| BOT_H03 | header | thiếu `Accept-Language` HOẶC `Accept-Encoding` | — |
| BOT_H04 | header | `Origin` (fallback `Referer`) rỗng hoặc không `StartsWith` `AllowedOrigins` | default `["http://localhost:5173","https://localhost:5173"]` |
| BOT_E01 | env | `X-Webdriver: 1` HOẶC UA chứa `headlesschrome, phantomjs, puppeteer, playwright, selenium, webdriver` | — |
| BOT_E03 | behavior | chỉ khi `isBooking`: `X-Had-Input=0` → hit; vắng → `signals_missing+=had_input` | header `X-Had-Input` |
| BOT_V01 | velocity | requests/10s theo `sessionId ?? ip ?? "unknown"`: `>50` L1, `>100` L2 | `Hit("v01:{who}",10s)` |
| BOT_V04 | velocity | chỉ `isBooking && Ip!=null`: `>30` bookings/phút/IP | `Hit("v04:{ip}",60s)` |
| BOT_S02 | velocity | đăng ký/IP/giờ: `Hit("s02:{ip}",1h) >= 5` → **lần đăng ký thứ 5 dính** | Auth register |
| BOT_B01 | booking | order không có `HoldId` (rỗng) | order only |
| BOT_V02 | booking | order khi `now - hold.CreatedAtUtc ∈ [0,3s)` (fallback `ExpiresAt-600s`) | `HoldDuration=600s`, order only |
| (missing) | — | vắng `X-Device-Fp` → `missing+=device_fp` | — |

Chưa triển khai: `BOT_H05` (bị `[Authorize]` 401 trước), `BOT_E02` (cần ASN/GeoIP).

## 4. Rule fraud (`TicketRiskService.cs` + AdminAPI auto-rules)

| Mã | Kích hoạt (code) | Ngưỡng |
|---|---|---|
| FRD_T01 | hold+order: `Paid + Pending chưa hết hạn (ExpiresAt==null \|\| > now) + incoming(max(Σqty, seatIds.count)) > 6` /event/user | `MaxTicketsPerEvent=6` → 6 OK, 7 dính |
| FRD_T02 | hold/order: returns cũ `>= 3`; endpoint return: `cũ + 1 >= 3`. Đếm `TicketReturnRequests` của user trong 7d, loại `Rejected/Cancelled` | 2 lần cũ OK, lần 3 dính |
| FRD_T03 | order only: email/phone (email case-insensitive) xuất hiện ở `>= 3 CustomerId` khác nhau trong 24h (orders chưa xóa) + cộng user hiện tại | 3 tài khoản chung liên hệ dính |
| FRD_A01 | hold/order: JWT claim `account_created_at` tuổi `< 24h` VÀ `incoming >= 4`; thiếu claim → `signals_missing` | tài khoản <24h mua ≥4 vé |
| FRD_A02 | register: domain trong 15 disposable (`mailinator, guerrillamail, 10minutemail, tempmail.com, temp-mail.org, yopmail, trashmail, sharklasers, getnada, dispostable, throwawaymail, maildrop.cc, fakeinbox, mailnesia, mohmal`) HOẶC local-part ≥12 ký tự toàn alnum với digits ≥ nửa hoặc 0 nguyên âm | Auth register |
| TRU_01 | trust âm: tuổi TK `> 183d` VÀ `>= 3` orders Paid chưa xóa | trừ điểm, cap 30 |
| HR_VIP | xem §1 | ≥20tr + score ≥ High → HOLD |
| FRD_A03 | **AdminAPI tự tính** khi ingest: `PASSWORD_CHANGED/RESET` trong audit log trong window + booking có `EventId` | xem `InternalFraudAlertController` |
| FRD_L02 | AdminAPI auto: block gần đây trong `WindowSec` | ingest |
| TRU_05 | AdminAPI auto: từng bị restore trong window / `TrustRestoreDays=30d` | ingest |

Chưa triển khai: FRD_G01, FRD_G02, TRU_02 (cần GeoIP/lịch sử login).
Pending hết hạn không tính vào FRD_T01 (Pending `ExpiresAt = hold.ExpiresAtUtc`, worker hết hạn riêng).

## 5. Rate limit (đã xác minh — chạy TRƯỚC risk)

Fixed-window, `QueueLimit=0`, 429 JSON + `Retry-After`:
- AuthAPI theo `ip:{RemoteIpAddress}` (**socket IP, không phải X-Forwarded-For**):
  `auth-login` 10 req/60s/IP (`register, login, google`); `auth-otp` 5 req/300s/IP
  (`forgot-password, verify-reset-otp, step-up/send-otp, step-up/verify-otp`).
- TicketAPI theo `u:{NameIdentifier}` (có login) else `ip:{RemoteIpAddress}`:
  `hold` 30 req/60s/user (tạo/hủy/đổi ghế); `order` 10 req/60s/user (`POST create`).

## 6. Endpoint cần phủ (route chính xác từ code)

- Auth: `POST /api/auth/{register, verify-email, login, refresh, logout, forgot-password, verify-reset-otp, reset-password, google, step-up/send-otp, step-up/verify-otp, change-password}`; KYC `GET /api/kyc/consent`, `POST /api/kyc/submit` (multipart ≤6MB/ảnh), `GET /api/kyc/status/{id}`.
- Ticket: `POST /api/Holds/session {EventId, Tickets[{TicketTypeId, Quantity}], SeatIds[]}`, `PUT .../seats`, `POST .../cancel`, `GET .../active`, `PUT .../attendees`; `POST /api/Orders/create` (chú ý prefix **Orders** số nhiều, `HoldId` không required nhưng trống → BOT_B01); `POST /api/Orders/{id}/cancel-pending` (internal-key); `POST /api/ticket-returns` (+ review/confirm-manual-refund cho Staff/Admin).
- Admin risk: `GET/PUT /api/admin/risk/policy`, `GET/POST /api/admin/risk/blocks`, `POST .../{id}/restore`, `GET /api/admin/fraud-alerts[/summary/{id}]`, `POST /{id}/ai-summary` (Admin; chỉ test phân quyền), `GET /api/admin/fraud-cases`, `POST /{id}/{assign, recommend, resolve}`, `GET /api/risk/decisions/me` (BLOCK/HOLD non-shadow, 30d, tối đa 20), `GET /api/risk/decisions/{id}` (chủ sở hữu hoặc admin; Staff cần được giao), `POST /api/risk/appeals`, `GET/POST /api/admin/risk/appeals[/{id}/resolve]`.
- Nội bộ (`X-Internal-Api-Key`, sai/thiếu → 401, IP sai → 400): `POST /api/internal/fraud-alerts` (cần ≥1 trong UserId/SessionId/DeviceFp/IpAddress), `GET /api/internal/risk/check`, TicketAPI `POST /api/internal/tickets/risk/{id}/{block,unblock,revoke-and-refund}`.

Hiệu chỉnh so với đặc tả ban đầu:
- Block thủ công `scope` chỉ `ACCOUNT|TICKET|IP` — **DEVICE/SESSION bị 400**.
- `DurationHours` 1..720; reason block/restore/policy 10–500; appeal reason 10–2000;
  resolve appeal `reviewNote` 10–1000; case resolve note 10–500; evidence ≤5 file, mỗi file ≤5MB, URL http(s).
- Không tự khóa chính mình (ACCOUNT); khóa target Admin/Staff → 403; TICKET/IP cần `UserId` chủ sở hữu; whitelist → 400; trùng block → 409; >2 scope active/user → 409.
- Appeal: chỉ `BLOCK` (HOLD bị từ chối), non-shadow, còn Active, chưa hết hạn, trong hạn 7 ngày (scope TICKET 24h), mỗi decision 1 appeal, sai chủ → 404.
- Fraud case: Staff chỉ thấy/đề xuất case được giao cho mình; resolve chỉ Admin; đóng FalsePositive/Dismissed khi còn block → 409; còn appeal pending → 409.
- eKYC gate (TicketAPI): claim `ekyc_status ∈ {Passed, Verified}` else 403 `EKYC_REQUIRED`; Admin/Staff/Organizer bypass; check ở cả hold và order. AuthAPI không có gate này.
- Challenge vượt: OTP = JWT `X-StepUp-Token` (`aud=AuthenticationAPI.StepUp`, `purpose=stepup`, `sub=userId`, skew 30s); CAPTCHA = `X-Challenge-Token` verify qua Turnstile (`IMemoryCache chal:captcha:{session??device??ip}` TTL 10 phút; thiếu secret → luôn fail).
- Ports (launchSettings): AdminAPI `http 5153 / https 7076`; TicketAPI `5019/7268`; Auth `5010/7058`; Payment `5182/7227`; frontend `5173`.

## 7. Cần xác nhận (dừng trước khi sang Bước 2)

1. Base URL từng service đang chạy (Admin/Auth/Ticket/Payment) + frontend URL.
2. Tài khoản test: Admin, Staff, ≥3 Customer **đã KYC Passed/Verified** (hoặc quy trình duyệt KYC trên dev + ai duyệt), user mới <24h cho FRD_A01, user già >6 tháng + ≥3 đơn Paid cho TRU_01 (hoặc cho phép seed).
3. `eventId` đang mở bán + `ticketTypeId`/ghế test được phép giữ/mua/hủy.
4. `X-Internal-Api-Key` dùng cho test (đưa qua biến môi trường, không commit).
5. OTP test lấy thế nào: SMTP thật / đọc DB / API dev / mailbox test? Step-up OTP và verify-email/reset-password phụ thuộc vào đây.
6. Turnstile secret trên môi trường test có thật không? Nếu không, các case CAPTCHA chỉ assert 428 + decision, không pass-through.
7. Có được phép PUT `risk.mode` ENFORCE và tạo/restore block trên dev không? Teardown sẽ khôi phục mode + restore block do test tạo.
