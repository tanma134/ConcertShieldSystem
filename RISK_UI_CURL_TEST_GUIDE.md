# Risk UI and cURL Test Guide

This guide tests both **Bot** and **Fraud** alerts, then verifies the alert on the Fraud Dashboard and exercises manual block, appeal and restore.

> Run these tests only against your local/development services and test data. The `ENFORCE` seeds can temporarily block a real account, device, session or IP.

## 0. Service URLs

| Service | URL |
|---|---|
| Frontend | `http://localhost:5173` |
| Gateway | `https://localhost:7164` |
| AdminAPI direct/internal test endpoint | `http://localhost:5153` |
| AuthenticationAPI | `https://localhost:7058` |
| TicketAPI | `https://localhost:7268` |

Start the services needed by the test:

```bash
dotnet run --project backend/AdminAPI/AdminAPI.csproj --launch-profile http
dotnet run --project backend/TicketAPI/TicketAPI.csproj --launch-profile https
dotnet run --project backend/GatewayAPI/GatewayAPI.csproj --launch-profile https
```

Open the frontend normally and log in with a real customer account. Log in separately with an Admin account to open the dashboard.

## 1. Find a real User ID

Run against `authentication_db`:

```sql
SELECT user_id, email, full_name
FROM users
WHERE email = 'your-real-customer-email@example.com';
```

Save the returned value as `USER_ID`. Do not use the Admin's ID for a customer test.

## 2. Get an Admin bearer token for cURL

After logging in to the web UI as Admin, open browser DevTools → **Application** → **Local Storage** → the frontend origin and copy `accessToken`.

Git Bash:

```bash
export ADMIN_TOKEN='paste-admin-access-token-here'
```

PowerShell:

```powershell
$ADMIN_TOKEN = 'paste-admin-access-token-here'
```

The internal ingest cURL calls do not need a bearer token. They need the `InternalApi:Key` from `backend/AdminAPI/appsettings.json`:

```bash
export INTERNAL_RISK_KEY='copy-InternalApi-Key-from-backend/AdminAPI/appsettings.json'
```

PowerShell:

```powershell
$INTERNAL_RISK_KEY = 'copy-InternalApi-Key-from-backend/appsettings.json'
```

## 3. Test A — Bot alert on the real UI

### 3.1 Seed the temporary Bot rule

Run against `admin_db`:

```text
database/risk_ui_test_bot_seed.sql
```

This temporarily configures:

- `BOT_E01` score = 70
- Critical threshold = 60
- `risk.mode = ENFORCE`
- Critical requires only one group

Restart AdminAPI after running the seed because risk configuration is cached for up to 60 seconds.

### 3.2 Make the browser send the Bot signal

1. Open the frontend and log in as the customer.
2. Press `F12` → **Console**.
3. Run this before the purchase request:

```js
Object.defineProperty(navigator, "webdriver", {
  get: () => true,
  configurable: true
});
```

4. Verify:

```js
navigator.webdriver
```

It must return `true`.

5. Reload the page, run the override again after reload, then select any ticket and submit the order/payment form.

The frontend sends `X-Webdriver: 1`; TicketAPI reports `BOT_E01` to AdminAPI.

### 3.3 Expected UI result

The checkout page should show an error similar to:

```text
Your account or device is temporarily restricted due to unusual activity.
Please contact support if you think this is a mistake.
```

`PaymentInfoPage` also displays the backend error in the page error area, toast and browser alert.

### 3.4 Expected Fraud Dashboard result

Open:

```text
Admin → Fraud & Risk Management → Fraud Alerts
```

The new alert should contain:

- Alert type: `Bot`
- Risk level: `Critical`
- Triggered rule: `BOT_E01`
- Score: approximately `70`
- Reason value related to `navigator.webdriver`

A Bot-dominant decision automatically targets `SESSION`, `DEVICE` and `IP`, not `ACCOUNT`. This is expected. If you want to manually block the account, open the alert and use **Block for this alert → ACCOUNT**.

## 4. Test B — Fraud alert on the real UI

### 4.1 Prepare an event that allows seven tickets

Run against `event_db` after replacing `<EVENT_ID>`:

```sql
UPDATE events
SET max_tickets_per_account = 10
WHERE event_id = <EVENT_ID>;

UPDATE ticket_types
SET max_per_order = 10
WHERE event_id = <EVENT_ID>
  AND status = 'Active';

SELECT ticket_type_id, type_name, quantity, sold_quantity,
       quantity - sold_quantity AS remaining, max_per_order
FROM ticket_types
WHERE event_id = <EVENT_ID>
  AND status = 'Active';
```

Choose a ticket type with at least seven remaining tickets. If this is a local-only event, you can increase its quantity:

```sql
UPDATE ticket_types
SET quantity = sold_quantity + 20
WHERE ticket_type_id = <TICKET_TYPE_ID>;
```

### 4.2 Seed the real fraud rule

Run against `admin_db`:

```text
database/risk_ui_test_seed.sql
```

This temporarily configures:

- Real TicketAPI rule: `FRD_T01`
- `FRD_T01` score = 70
- Critical threshold = 60
- `risk.mode = ENFORCE`

Restart AdminAPI.

### 4.3 Trigger the rule through the UI

1. Log in as the real customer.
2. Open the prepared event.
3. Select **7 tickets**.
4. Continue to checkout and submit the order.

TicketAPI sends `FRD_T01` when `existing tickets + incoming tickets > 6`.

The UI should show the same temporary-restriction error, and the payment page should not proceed to VNPay.

### 4.4 Expected Fraud Dashboard result

Open the Fraud Dashboard. The alert should show:

- Alert type: `Fraud`
- Risk level: `Critical`
- Triggered rule: `FRD_T01`
- Value similar to `7 tickets for event <EVENT_ID>`
- Scope: `ACCOUNT`
- The real customer User ID

From the alert detail, click **Block for this alert** and verify the prefilled real User ID. Enter a reason of at least 10 characters and confirm.

## 5. Test C — Generate a Bot alert with cURL

This bypasses the browser but creates a real database alert that appears in the same Fraud Dashboard.

First run:

```text
database/risk_ui_test_bot_seed.sql
```

Restart AdminAPI, then run in Git Bash:

```bash
curl -X POST "http://localhost:5153/api/internal/fraud-alerts" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: $INTERNAL_RISK_KEY" \
  -d "{\
    \"userId\": 43,\
    \"ipAddress\": \"198.51.100.10\",\
    \"sessionId\": \"local-bot-test-session-001\",\
    \"deviceFp\": \"local-bot-test-device-001\",\
    \"botRules\": [\
      {\"code\":\"BOT_E01\",\"level\":1,\"value\":\"navigator.webdriver = true\"}\
    ],\
    \"fraudRules\": [],\
    \"trustRules\": [],\
    \"signalsMissing\": []\
  }"
```

Replace `43` with your real `USER_ID`.

Expected response fields:

```json
{
  "fraudAlertId": 123,
  "level": "Critical",
  "action": "BLOCK",
  "dominant": "Bot",
  "isShadow": false
}
```

If the response contains a non-null `fraudAlertId`, open the Fraud Dashboard and search the last seven days. The alert is already stored and does not require a browser order.

PowerShell equivalent:

```powershell
$body = @{
  userId = 43
  ipAddress = '198.51.100.10'
  sessionId = 'local-bot-test-session-001'
  deviceFp = 'local-bot-test-device-001'
  botRules = @(@{ code = 'BOT_E01'; level = 1; value = 'navigator.webdriver = true' })
  fraudRules = @()
  trustRules = @()
  signalsMissing = @()
} | ConvertTo-Json -Depth 5

curl.exe -X POST 'http://localhost:5153/api/internal/fraud-alerts' `
  -H 'Content-Type: application/json' `
  -H "X-Internal-Api-Key: $INTERNAL_RISK_KEY" `
  -d $body
```

## 6. Test D — Generate a Fraud alert with cURL

For a safe dashboard-only fraud test, run:

```text
database/risk_dashboard_test_seed.sql
```

This adds three temporary FRAUD rules in three groups. It is intended to create a Critical alert while leaving your existing enforcement mode unchanged. Restart AdminAPI, then run:

```bash
curl -X POST "http://localhost:5153/api/internal/fraud-alerts" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: $INTERNAL_RISK_KEY" \
  -d "{\
    \"userId\": 43,\
    \"orderId\": 900001,\
    \"ticketId\": 700001,\
    \"eventId\": 1,\
    \"ipAddress\": \"198.51.100.11\",\
    \"fraudRules\": [\
      {\"code\":\"TEST_FRAUD_ACCOUNT_A\",\"level\":1,\"value\":\"local fraud test A\"},\
      {\"code\":\"TEST_FRAUD_ACCOUNT_B\",\"level\":1,\"value\":\"local fraud test B\"},\
      {\"code\":\"TEST_FRAUD_ACCOUNT_C\",\"level\":1,\"value\":\"local fraud test C\"}\
    ],\
    \"botRules\": [],\
    \"trustRules\": [],\
    \"signalsMissing\": []\
  }"
```

The `orderId`, `ticketId` and `eventId` above are test correlation values. Replace them with real IDs if you want the alert detail to link to real data.

Expected result:

```json
{
  "fraudAlertId": 124,
  "level": "Critical",
  "dominant": "Fraud"
}
```

Because this seed is dashboard-only and may keep `SHADOW`, the alert can be visible without automatically blocking. Use the alert detail's **Block for this alert** action to test manual ACCOUNT or TICKET blocking.

## 7. cURL manual block through the Gateway

After obtaining an Admin bearer token, use the gateway endpoint:

### Block an account

```bash
curl -k -X POST "https://localhost:7164/api/admin/risk/blocks" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "scope": "ACCOUNT",
    "userId": 43,
    "durationHours": 24,
    "reason": "Local fraud dashboard test block"
  }'
```

### Block a ticket

Use a real ticket owned by the same user and not already checked in:

```bash
curl -k -X POST "https://localhost:7164/api/admin/risk/blocks" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "scope": "TICKET",
    "userId": 43,
    "ticketId": 700001,
    "durationHours": 24,
    "reason": "Local ticket fraud review test"
  }'
```

Expected result:

- `riskDecisionId` is returned.
- The real ticket status becomes `Blocked` in `ticket_db`.
- QR/check-in rejects the ticket.
- My Tickets shows `Blocked` and the 24-hour appeal deadline.

### Block an IP

```bash
curl -k -X POST "https://localhost:7164/api/admin/risk/blocks" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "scope": "IP",
    "userId": 43,
    "ipAddress": "198.51.100.10",
    "durationHours": 24,
    "reason": "Local IP fraud review test"
  }'
```

## 8. Verify and clean up

### Query recent alerts

Using the Admin token:

```bash
curl -k "https://localhost:7164/api/admin/fraud-alerts?page=1&pageSize=20" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

### Query active blocks

```bash
curl -k "https://localhost:7164/api/admin/risk/blocks?status=Active&page=1&pageSize=20" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

### Roll back temporary policy/rules

Run against `admin_db`:

```text
database/risk_ui_test_rollback.sql
```

This returns the temporary policy to SHADOW/default values and removes the temporary `FRD_T01` and `BOT_E01` rows only when their descriptions match the test seeds.

If those rules already existed before testing, restore their original values from the backup query in the seed file instead.

### Restore test blocks

Restore them from the Admin UI:

```text
Admin → Fraud & Risk Management → Risk Blocks → Restore
```

A block created by cURL or by the dashboard remains in `risk_decisions` until it is restored or expires. The rollback SQL does not delete historical alerts or decisions.

## 9. Common problems

| Symptom | Check |
|---|---|
| `ignoredRules: rule does not exist` | Run the matching seed in `admin_db`, then restart AdminAPI. |
| `fraudAlertId` is null | The score did not reach High/Critical, or the rule type/group is wrong. |
| UI displays no error | The error is shown in PaymentInfo's error card, toast and browser alert; inspect the browser Console and Network tab. |
| Bot test does not fire | Run the `navigator.webdriver` override after every page reload and verify it returns `true`. |
| Fraud purchase test cannot select 7 | Increase event `max_tickets_per_account`, ticket type `max_per_order`, and available quantity in `event_db`. |
| cURL returns 401 | Check `X-Internal-Api-Key` against AdminAPI `InternalApi:Key`; this endpoint is direct AdminAPI, not the Gateway. |
| Dashboard is empty | Use the last-seven-days filter/default, verify `fraudAlertId` is non-null, and make sure the Admin token has the Admin role. |
