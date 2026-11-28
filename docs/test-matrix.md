# Test matrix — chống Bot / Gian lận (Bước 2)

Quy ước: `H` = header "người thật" (UA Chrome thật + `sec-ch-ua`, `sec-fetch-site`,
`Accept-Language`, `Accept-Encoding`, `Origin: http://localhost:5173`, `X-Had-Input: 1`,
`X-Device-Fp`/`X-Session-Id`/`X-Forwarded-For` riêng mỗi test).
`B(x)` = header bot (bỏ/sửa `x`). Mọi test đọc điểm thật từ `GET /api/admin/risk/policy`,
không hardcode. `retries = 0`. Mode gốc lưu ở setup, ENFORCE khi cần, restore ở teardown.

## 0. Chuẩn bị môi trường (đang thiếu — cần seed trước Bước 3)

- Seed tối thiểu: 1 Admin, 1 Staff, 3 Customer **KYC Passed** (qua `POST /api/kyc/submit`
  + duyệt dev), 1 user mới <24h (FRD_A01), 1 user già >6 tháng + ≥3 đơn Paid (TRU_01).
- 1 event mở bán + `ticketTypeId`/ghế test; ghi `eventId` vào `.env` (`TEST_EVENT_ID`...).
- `X-Internal-Api-Key` qua biến môi trường. OTP: dùng mailbox/SMTP dev đọc OTP thật;
  Turnstile: secret thật (nếu không, case pass CAPTCHA ghi "chưa test được").
- Cho phép `PUT /api/admin/risk/policy` ENFORCE trên dev + tạo/restore block test.

## 1. AUTH-BOT

| ID | Chức năng | Dữ liệu | Mong đợi |
|---|---|---|---|
| BOT-001 | Happy-path register + login | H, email mới hợp lệ | 200, JWT có `account_created_at`, `ekyc_status`; không decision |
| BOT-002 | UA CLI | `User-Agent: curl/8.0` | BOT_H01 hit; ENFORCE: 428 `RISK_CHALLENGE`/CAPTCHA hoặc block theo score |
| BOT-003 | Headless/playwright UA | UA mặc định Playwright | BOT_E01 hit |
| BOT-004 | `X-Webdriver: 1` | H + `X-Webdriver: 1` | BOT_E01 hit |
| BOT-005 | Thiếu sec-ch-ua | UA Chrome, bỏ `sec-ch-ua` | BOT_H02 hit |
| BOT-006 | Thiếu Accept-Language | H trừ `Accept-Language` | BOT_H03 hit |
| BOT-007 | Origin lạ | `Origin: https://evil.example` | BOT_H04 hit |
| BOT-008 | Rate limit login 10/phút/IP | 11 login/phút cùng IP | lần 11: 429 + `Retry-After` |
| BOT-009 | Đăng ký hàng loạt BOT_S02 | 5 register/IP/giờ | lần 1–4 OK (200), lần 5: BOT_S02 (428/block) |
| BOT-010 | Boundary S02 | 4 register OK, register 5 dính | như trên, assert ranh giới |
| BOT-011 | FRD_A02 disposable | `user@mailinator.com` | FRD_A02 hit |
| BOT-012 | FRD_A02 random local | local-part ≥12 alnum ngẫu nhiên | FRD_A02 hit |
| BOT-013 | OTP rate limit 5/5phút | 6 `step-up/send-otp`/5phút | lần 6: 429 |
| BOT-014 | Brute-force password | N login sai liên tiếp | 401, không khóa tài khoản thật ngoài phạm vi test |
| BOT-015 | Block IP/device/session chặn login | block IP test qua admin → login từ IP đó | 403 `RISK_BLOCKED` |

## 2. TKT-BOT

| ID | Chức năng | Dữ liệu | Mong đợi |
|---|---|---|---|
| TKTB-001 | Happy-path hold→order | H, hold hợp lệ, chờ >3s rồi order | 200; không decision |
| TKTB-002 | Order không HoldId (BOT_B01) | `HoldId: ""` | BOT_B01 hit |
| TKTB-003 | Hold→order <3s (BOT_V02) | order ngay sau hold | BOT_V02 hit |
| TKTB-004 | Boundary V02 | order sau ~4s | không dính BOT_V02 |
| TKTB-005 | `X-Had-Input: 0` (BOT_E03) | H + `X-Had-Input: 0` khi đặt | BOT_E03 hit |
| TKTB-006 | Thiếu `X-Had-Input` | bỏ header | `signals_missing` có `had_input`, không hit |
| TKTB-007 | Rate limit hold 30/phút/user | 31 tạo hold/phút | lần 31: 429 |
| TKTB-008 | Rate limit order 10/phút/user | 11 create/phút | lần 11: 429 |
| TKTB-009 | BOT_V01 nhiều user chung session | nhiều user thật chung `X-Session-Id`, >50 req/10s | L1; >100 req/10s → L2 |
| TKTB-010 | BOT_V04 nhiều booking chung IP | >30 bookings/phút/IP | BOT_V04 hit |
| TKTB-011 | SHADOW cho qua | mode SHADOW + header bot | decision `IsShadow=true`, request vẫn 200 |

## 3. TKT-FRAUD

| ID | Chức năng | Dữ liệu | Mong đợi |
|---|---|---|---|
| F-001 | Happy-path ≤6 vé | mua 2 vé | 200, không FRD_T01 |
| F-002 | Boundary FRD_T01 | tổng = 6 OK; +1 → 7 | 6 OK, 7 dính FRD_T01 |
| F-003 | FRD_T02 | 3 `POST /api/ticket-returns` trong 7d | lần 3: FRD_T02 |
| F-004 | Boundary T02 | 2 returns OK, return 3 dính | như trên |
| F-005 | FRD_T03 | 3 user chung email/SĐT nhận vé 24h | user 3 dính FRD_T03 |
| F-006 | FRD_A01 | user <24h mua ≥4 vé | FRD_A01 hit |
| F-007 | FRD_A01 thiếu claim | token không `account_created_at` | `signals_missing`, không crash |
| F-008 | HR_VIP | đơn ≥20tr + score ≥High | ép High/HOLD `HR_VIP` |
| F-009 | eKYC gate | user `ekyc_status=NotSubmitted` đặt vé | 403 `EKYC_REQUIRED` |
| F-010 | Pending hết hạn | Pending quá 600s → mua tiếp | pending cũ không tính vào T01 |

## 4. DECISION

| ID | Nội dung | Mong đợi |
|---|---|---|
| D-001 | Fraud Medium → OTP challenge | 428 `RISK_CHALLENGE`, `challengeType=OTP` |
| D-002 | Bot Medium → CAPTCHA challenge | 428, `challengeType=CAPTCHA` |
| D-003 | Fraud High → HOLD | 403 `RISK_HOLD` |
| D-004 | Bot High → BLOCK SESSION+IP 1h | 403 `RISK_BLOCKED`; `GET /api/internal/risk/check` blocked |
| D-005 | Critical fraud → BLOCK ACCOUNT | 403; login/refresh cũng 403 |
| D-006 | Critical 1 nhóm → downgrade | fraud HOLD / bot CAPTCHA, reason `DOWNGRADED_SINGLE_GROUP` |
| D-007 | Cap nhóm/combo/trust | tính từ policy thật; assert score = công thức `min(40/nhóm)+combo−trust` |
| D-008 | TRU_01 giảm điểm | user già + ≥3 Paid: score thấp hơn user mới cùng hành vi |
| D-009 | Leo thang 24h→72h→manual | block 1: 24h; 2: 72h; 3: HOLD `ESCALATION_MANUAL_REVIEW` |
| D-010 | Step-up OTP hợp lệ vượt challenge | `X-StepUp-Token` đúng user → request qua |
| D-011 | Token sai/hết hạn/khác user | vẫn 428/403 |
| D-012 | Turnstile thật | token thật → qua; không secret → "chưa test được" + lý do |

## 5. ADMIN

| ID | Nội dung | Mong đợi |
|---|---|---|
| ADM-001 | Manual block + restore | block ACCOUNT test → 403; restore → đặt lại được |
| ADM-002 | Validate block | scope DEVICE/SESSION → 400; `DurationHours` 0/721 → 400; reason <10 → 400; tự khóa → 400; target Admin → 403; user không tồn tại → 404; user inactive → 409; trùng → 409; TICKET/IP thiếu UserId → 400; >2 scope → 409; whitelist → 400 |
| ADM-003 | Fraud case luồng | alert → assign Staff → recommend → resolve |
| ADM-004 | Phân quyền | Customer gọi admin → 403; không token → 401; Staff xem case không được giao → 403; Staff resolve → 403 |
| ADM-005 | Appeal end-to-end Accept | user bị block → appeal → admin Accept → block gỡ, đặt lại được |
| ADM-006 | Appeal Reject | vẫn 403 `RISK_BLOCKED` |
| ADM-007 | Appeal quá hạn/trùng | quá 7d (TICKET 24h) → 409; appeal 2 lần → 409; HOLD appeal → 409 |
| ADM-008 | Đổi policy có hiệu lực ngay (+60s cache) | hạ ngưỡng → hành vi cũ bị chặn; khôi phục sau test |
| ADM-009 | Audit log | mọi block/restore/policy/appeal-resolve có bản ghi |

## 6. SECURITY

| ID | Nội dung | Mong đợi |
|---|---|---|
| SEC-001 | Giả `X-Forwarded-For` né block IP | nếu né được → ghi bug; nếu không → ghi cơ chế |
| SEC-002 | Replay `X-StepUp-Token`/`X-Challenge-Token` | token dùng lại ngoài phạm vi → từ chối |
| SEC-003 | Token user A dùng cho user B | 428/403, không qua |
| SEC-004 | Gọi internal không key/sai key | 401; không đưa key vào code |
| SEC-005 | User thường gọi API admin | 403; anon → 401 |

## 7. RACE (`Promise.all`, user thật)

| ID | Nội dung | Mong đợi |
|---|---|---|
| R-001 | Nhiều user giữ/mua 1 ghế/vé cuối | đúng 1 thành công, không bán trùng |
| R-002 | 1 user bắn song song nhiều order vượt 6 vé | FRD_T01 vẫn bắt (hoặc ghi bug nếu lách được) |

## 8. E2E

| ID | Luồng |
|---|---|
| E2E-001 | user thật bị flag → admin thấy alert/case → assign → resolve → block/restore → user phản hồi đúng |

## 9. Chưa test được (ghi rõ lý do khi chạy)

BOT_E02, FRD_G01/G02, TRU_02 (cần GeoIP/lịch sử login); CAPTCHA pass-through nếu thiếu
Turnstile secret; BOT_V01/V04 nếu rate-limit/user đơn lẻ không khả thi (ghi nhận).
Fail-open AdminAPI lỗi: không test bằng tắt service thật.
