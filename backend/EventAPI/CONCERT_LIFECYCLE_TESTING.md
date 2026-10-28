# Concert Create → Approval Lifecycle — Run & Test Guide

Backend workflow for **Customer creates concert → Draft → Submit → Admin approval → Published**,
on .NET 8 + PostgreSQL + Cloudinary + Swagger.

> **Note on verification.** The migration SQL in `EventAPI/Migrations/` was executed
> against a real PostgreSQL 16 database and verified (legacy status migration,
> timestamp backfill, zone capacity backfill, ticket quantity resync, idempotent
> re-runs, CHECK constraint). The **C# was not compiled or run** — the environment it
> was written in blocks `api.nuget.org`, so `dotnet restore` could not complete.
> Expect to fix a compile error or two on first build.

---

## 1. Prerequisites

- .NET 8 SDK
- PostgreSQL running locally
- Two databases: `auth_db` and `event_db`

```sql
CREATE DATABASE auth_db;
CREATE DATABASE event_db;
```

Connection strings assume `Username=postgres;Password=123456`. Change them in
`AuthenticationAPI/appsettings.json` and `EventAPI/appsettings.json` if yours differ.

---

## 2. Apply the database change

Either EF Core:

```bash
cd backend/EventAPI
dotnet ef migrations add ConcertLifecycleAndMixedZones
dotnet ef database update
```

…or the raw SQL (idempotent, safe to re-run):

```bash
psql -U postgres -d event_db -f backend/EventAPI/Migrations/001_concert_lifecycle_and_zones.sql
```

What it does:

| Change | Why |
|---|---|
| `events.submitted_at / approved_at / rejected_at / reviewed_by` | Lifecycle audit trail |
| `ix_events_status` | Every public list query filters on status |
| `status 'Approved'` → `'Published'` | Matches the spec's lifecycle naming |
| `seat_zones.zone_type / capacity` + CHECK | Mixed seated/standing zones |
| Backfills | Existing zones become `Seated`; capacity from seat counts; quantities resynced |

---

## 3. Run both services

```bash
# Terminal 1
cd backend/AuthenticationAPI && dotnet run     # http://localhost:5010/swagger

# Terminal 2
cd backend/EventAPI && dotnet run              # http://localhost:5298/swagger
```

`AuthenticationAPI` seeds on startup:

| Account | Password | Roles |
|---|---|---|
| `admin@ticketbox.com` | `Admin@123` | Admin |
| `customer@ticketbox.com` | `Customer@123` | Customer |

The test customer is pre-verified so you don't need a working SMTP server.

Both services must share the same `JwtSettings` (they do by default) or EventAPI
will reject AuthenticationAPI's tokens.

---

## 4. Main flow

> In Swagger, click **Authorize** and paste the `accessToken` value.

### 4.1 Customer login
`POST http://localhost:5010/api/auth/login`
```json
{ "email": "customer@ticketbox.com", "password": "Customer@123" }
```
Copy `accessToken`. Note the roles: `["Customer"]` only — no Organizer yet.

### 4.2 Create the draft
`POST http://localhost:5298/api/events`
```json
{
  "title": "Sơn Tùng M-TP Live in Cần Thơ",
  "shortDescription": "A night of hits",
  "description": "Full concert with special guests.",
  "locationName": "Can Tho Convention Center",
  "address": "1 Le Loi",
  "city": "Cần Thơ",
  "latitude": 10.0333,
  "longitude": 105.7833,
  "startsAt": "2027-03-15T19:00:00Z",
  "endsAt": "2027-03-15T22:30:00Z",
  "hasSeatingChart": false,
  "minTicketsPerAccount": 1,
  "maxTicketsPerAccount": 4
}
```
→ `201`, `status: "Draft"`, `category: "Music"`. Keep the `eventId` (assume `1`).

Category is fixed server-side; there is no field to choose another one.

### 4.3 Upload images
`POST /api/events/1/poster` — form-data, `file` = a .jpg/.png.
Optional: `POST /api/events/1/banner`, `POST /api/eventimages/event/1/upload` for gallery.

Re-uploading replaces the image **and deletes the old Cloudinary asset**.
`DELETE /api/events/1/poster` removes it entirely.

### 4.4 Add ticket types
`POST /api/tickettypes/event/1`
```json
{ "typeName": "VIP", "description": "Front rows", "price": 2500000, "quantity": 100, "minPerOrder": 1, "maxPerOrder": 4, "sortOrder": 1 }
```
```json
{ "typeName": "Regular", "price": 900000, "quantity": 500, "minPerOrder": 1, "maxPerOrder": 6, "sortOrder": 2 }
```

### 4.5 Refund policy
`POST /api/refundpolicies/event/1`
```json
{ "policyName": "Full refund", "description": "Up to 7 days before", "deadlineBeforeEventHours": 168, "refundPercent": 100, "requiresOrganizerApproval": false, "isActive": true }
```

### 4.6 Seating — pick one

**Option A — general admission.** Skip seating entirely. Capacity comes from
`quantity` on each ticket type. Done.

**Option B — mixed seated + standing** (see §5 for detail):

`POST /api/seating/event/1`
```json
{
  "name": "Convention Center Layout",
  "zones": [
    { "ticketTypeId": 1, "zoneName": "VIP Seated", "zoneType": "Seated", "rows": 10, "seatsPerRow": 10, "rowLabelPrefix": "A" },
    { "ticketTypeId": 2, "zoneName": "Standing Pit", "zoneType": "Standing", "capacity": 500 }
  ]
}
```

Once a layout exists, `quantity` becomes **derived** — the service recalculates it
from zone capacity and rejects manual edits. That's the behaviour the big platforms
use, and it's why seats and quantity can't drift apart.

### 4.7 Update the draft
`PUT /api/events/1` — partial, send only changed fields.

### 4.8 Check readiness (optional but useful)
`GET /api/events/1/validate` → every missing field listed, before you submit.

### 4.9 Submit
`POST /api/events/1/submit` → `status: "Pending"`, `submittedAt` set.

If anything is incomplete you get `400` with the full error list — not one error at a time.

### 4.10 Admin login
`POST http://localhost:5010/api/auth/login` with `admin@ticketbox.com` / `Admin@123`.

### 4.11 Review the queue
- `GET /api/events/pending` — queue with ticket/price summary
- `GET /api/events/admin/1` — full detail, any status

### 4.12 Approve
`POST /api/events/1/approve` → `status: "Published"`, `approvedAt` + `publishedAt` set.

Behind the scenes EventAPI calls AuthenticationAPI to grant **Organizer** additively.
The response message confirms it. If AuthenticationAPI is unreachable the approval
still succeeds and the response carries a warning — approval is never rolled back
over a role-grant failure.

### 4.13 Confirm both roles
Log the customer in again → roles are now `["Customer", "Organizer"]`.

### 4.14 Public endpoints
No token needed:
- `GET /api/events` — Published only
- `GET /api/events/slug/son-tung-m-tp-live-in-can-tho`
- `GET /api/seating/event/1` — full chart
- `GET /api/seating/zones/{seatZoneId}/seats?availableOnly=true` — seat picker

Drafts/Pending/Rejected return `404` publicly, even by direct id.

---

## 5. Seating model

A concert is one of:

**No layout** — `hasSeatingChart: false`, no `seat_maps` row. Pure general admission.

**A layout with any mix of zones:**

| | Seated zone | Standing zone |
|---|---|---|
| `zoneType` | `"Seated"` | `"Standing"` |
| Seat rows | One per physical seat | None |
| Capacity from | `rows × seatsPerRow` | `capacity` field |
| Buyer picks | An individual seat | Just a quantity |
| Resize by | Delete + re-add the zone | `PUT` a new `capacity` |

Mixing is the normal concert case — numbered seats on the balcony, standing pit at
the front — and `zoneType` is per zone, so one concert does both.

**Buyer seat selection:**
1. `GET /api/seating/event/{eventId}/preview` — zones, prices, availability
2. `GET /api/seating/zones/{zoneId}/seats?availableOnly=true` — the grid
3. Hold/purchase → **TicketAPI / QueueAPI**, not here

EventAPI is read-only for seats after the layout is built. Holding and selling stay
in TicketAPI, as specified. `Seat.Status` (`Available` / `Held` / `Reserved` / `Sold` /
`Blocked`) plus `HeldByUserId` / `HoldExpiresAt` are already on the model for TicketAPI
to drive with a Redis lock and TTL.

**Safety rules:** you can't rebuild a layout, delete a zone, or re-link a zone to
another ticket type once any seat is `Held`/`Reserved`/`Sold`, or once a standing
zone's ticket type has sales. A standing zone can't shrink below what's sold.

---

## 6. Reject → edit → resubmit

1. Customer submits → `Pending`
2. `POST /api/events/1/reject` (admin)
   ```json
   { "reason": "The poster is low resolution. Please upload at least 1200x1600." }
   ```
   → `status: "Rejected"`, `rejectedAt` + `rejectedReason` set
3. Customer edits — `PUT /api/events/1`, re-upload poster, adjust tickets/seating.
   Rejected concerts are editable exactly like drafts.
4. `POST /api/events/1/submit` again → `Pending`, `rejectedReason` cleared
5. `POST /api/events/1/approve` → `Published`

---

## 7. Authorization

| Endpoint group | Who |
|---|---|
| `GET /api/events`, `/slug/{slug}`, `/{id}`, `/featured`, `/city/{city}` | Anyone (Published only) |
| `GET /api/seating/event/{id}`, `/zones/{id}/seats` | Anyone |
| Create / update / delete / submit, images, ticket types, refund, seating | Customer (**owner only**) |
| `pending`, `admin/{id}`, `approve`, `reject`, `restore` | Admin |

Ownership is checked in the service layer on **every** mutation — `OrganizerId` must
equal the caller's user id, else `403`. Admins bypass the ownership check but the
status rules still apply unless explicitly overridden.

Editing is limited to `Draft` and `Rejected` for the concert itself, its ticket
types, its refund policies and its seating layout.

---

## 8. Submit checklist

`GET /api/events/{id}/validate` and `POST .../submit` both enforce:

- Title; description (Description or ShortDescription); unique slug
- Venue name, address, city
- `StartsAt` in the future, `EndsAt` after `StartsAt`
- Poster (configurable), banner (off by default)
- ≥1 ticket type, ≥1 with `Status = "Active"`
- Per type: quantity > 0, price ≥ 0, `OriginalPrice ≥ Price`, `MinPerOrder > 0`,
  `MaxPerOrder ≥ MinPerOrder`, sale window ordered and ending before the concert
- ≥1 active refund policy (configurable), percent 0–100, deadline > 0
- With a layout: every ticket type placed in ≥1 zone; seated zones have seats;
  standing zones have capacity > 0; quantity matches derived capacity

Toggle the configurable ones in `appsettings.json`:

```json
"Publication": { "RequirePoster": true, "RequireBanner": false, "RequireRefundPolicy": true }
```

Approve re-runs the same validation — a concert that went stale in the queue can't
slip through.

---

## 9. Endpoint reference

**Concerts** — `api/events`

| Method | Route | Auth |
|---|---|---|
| GET | `/` `/filter` `/featured` `/city/{city}` | public |
| GET | `/{id}` `/slug/{slug}` | public |
| GET | `/mine` `/mine/{id}` | customer |
| POST | `/` | customer |
| PUT | `/{id}` | owner |
| DELETE | `/{id}` | owner |
| GET | `/{id}/validate` | owner |
| POST | `/{id}/submit` `/{id}/cancel` | owner |
| GET | `/pending` `/moderation/queue` `/admin/{id}` | admin |
| POST | `/{id}/approve` `/{id}/reject` `/{id}/restore` | admin |
| POST/DELETE | `/{id}/poster` `/{id}/banner` | owner |

**Gallery** — `api/eventimages` · **Tickets** — `api/tickettypes`
**Refunds** — `api/refundpolicies` · **Seating** — `api/seating`

| Method | Route | Auth |
|---|---|---|
| GET | `/event/{eventId}` `/event/{eventId}/preview` | public |
| GET | `/zones/{seatZoneId}/seats?availableOnly=` | public |
| POST | `/event/{eventId}` (build/replace) | owner |
| POST | `/event/{eventId}/zones` | owner |
| PUT/DELETE | `/zones/{seatZoneId}` | owner |
| DELETE | `/event/{eventId}` | owner |

**Roles** — `POST http://localhost:5010/api/auth/users/{id}/roles` (admin, additive, idempotent)

---

## 10. Troubleshooting

| Symptom | Cause |
|---|---|
| `401` on EventAPI with a valid token | `JwtSettings` differ between the two services |
| Approve warns about the Organizer role | AuthenticationAPI not running, or `Services:AuthenticationApi` wrong |
| Roles unchanged after approval | Tokens are cached until expiry — log in again |
| `Cloudinary is not configured` | Fill `Cloudinary:*`; note `appsettings.Development.json` overrides with dummy values |
| `404` on your own draft | Use `GET /api/events/mine/{id}` — `GET /{id}` is public and Published-only |
| Can't edit quantity | Expected once a layout exists — change zone capacity instead |
