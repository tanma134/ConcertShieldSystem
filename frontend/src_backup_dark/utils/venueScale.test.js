import test from "node:test";
import assert from "node:assert/strict";
import {
  VENUE_SCALE,
  pctToMeters,
  metersToPct,
  zoneRealSize,
  requiredFootprint,
  checkZoneFit,
  minZonePct,
  formatMeters,
  scaleRules,
  snapPct,
  seatGrid,
} from "./venueScale.js";

// ---------------------------------------------------------------------------
// Scale convention: the canvas is 1600 x 1000 px and 1 px = 0.1 m, so the whole
// drawable venue is 160 m x 100 m.
// ---------------------------------------------------------------------------

test("convention: canvas 1600x1000 px at 0.1 m per px", () => {
  assert.equal(VENUE_SCALE.canvasWidthPx, 1600);
  assert.equal(VENUE_SCALE.canvasHeightPx, 1000);
  assert.equal(VENUE_SCALE.metersPerPx, 0.1);
});

test("pctToMeters converts a percentage of the canvas to real metres", () => {
  assert.equal(pctToMeters(100, "x"), 160);
  assert.equal(pctToMeters(100, "y"), 100);
  assert.equal(pctToMeters(50, "x"), 80);
});

test("metersToPct is the inverse of pctToMeters", () => {
  assert.equal(metersToPct(160, "x"), 100);
  assert.equal(metersToPct(25, "y"), 25);
  assert.ok(Math.abs(metersToPct(pctToMeters(37.5, "x"), "x") - 37.5) < 1e-9);
});

test("zoneRealSize returns width, height and area in metres", () => {
  const size = zoneRealSize({ w: 10, h: 10 });
  assert.equal(size.widthM, 16);
  assert.equal(size.heightM, 10);
  assert.equal(size.areaM2, 160);
});

test("requiredFootprint grows with rows and seats per row", () => {
  // 10 seats x 0.5 m = 5 m wide, 5 rows x 0.9 m = 4.5 m deep.
  const fp = requiredFootprint(5, 10);
  assert.equal(fp.widthM, 5);
  assert.equal(fp.heightM, 4.5);
});

test("checkZoneFit accepts a zone that is large enough for its seats", () => {
  const result = checkZoneFit({ rect: { w: 10, h: 10 }, rows: 5, seatsPerRow: 10 });
  assert.equal(result.fits, true);
  assert.equal(result.message, "");
});

test("checkZoneFit rejects a zone that is too small and explains why", () => {
  // 30 x 40 seats needs 20 m x 27 m but 5% x 5% of the canvas is only 8 m x 5 m.
  const result = checkZoneFit({ rect: { w: 5, h: 5 }, rows: 30, seatsPerRow: 40 });
  assert.equal(result.fits, false);
  assert.equal(result.requiredWidthM, 20);
  assert.equal(result.requiredHeightM, 27);
  assert.match(result.message, /too small/i);
});

test("checkZoneFit never rejects a zone without seats (standing zones)", () => {
  const result = checkZoneFit({ rect: { w: 6, h: 6 }, rows: 0, seatsPerRow: 0 });
  assert.equal(result.fits, true);
});

test("minZonePct never goes below the minimum zone side", () => {
  const min = minZonePct(0, 0);
  // 2 m on a 160 m wide canvas is 1.25 %, on a 100 m tall canvas it is 2 %.
  assert.equal(min.w, 1.25);
  assert.equal(min.h, 2);
});

test("minZonePct covers the seat footprint for seated zones", () => {
  const min = minZonePct(10, 20);
  // 20 seats x 0.5 m = 10 m (6.25 %), 10 rows x 0.9 m = 9 m (9 %).
  assert.equal(min.w, 6.25);
  assert.equal(min.h, 9);
});

test("formatMeters prints one decimal and trims whole numbers", () => {
  assert.equal(formatMeters(12.345), "12.3 m");
  assert.equal(formatMeters(16), "16 m");
  assert.equal(formatMeters(0), "0 m");
});

test("scaleRules states the convention in plain words for organizers", () => {
  const rules = scaleRules();
  assert.ok(Array.isArray(rules) && rules.length >= 4);
  assert.ok(rules.some((line) => line.includes("1 px = 0.1 m")));
  assert.ok(rules.some((line) => line.includes("160 m x 100 m")));
});

test("snapPct snaps a position to the half-metre grid", () => {
  // 10.1 % of 160 m is 16.16 m -> snaps to 16 m -> exactly 10 %.
  assert.equal(snapPct(10.1, "x"), 10);
  // 25.2 % of 100 m is 25.2 m -> snaps to 25 m -> 25 %.
  assert.equal(snapPct(25.2, "y"), 25);
});

test("snapPct keeps a value that already sits on the grid", () => {
  assert.equal(snapPct(50, "x"), 50);
});

test("seatGrid of a standing zone has no rows or seats", () => {
  assert.deepEqual(seatGrid({ zoneType: "Standing", capacity: 300 }), { rows: 0, seatsPerRow: 0 });
});

test("seatGrid counts rows and seats per row from the generated seats", () => {
  const seats = [
    { rowLabel: "A", seatNumber: "1" }, { rowLabel: "A", seatNumber: "2" },
    { rowLabel: "A", seatNumber: "3" }, { rowLabel: "B", seatNumber: "1" },
    { rowLabel: "B", seatNumber: "2" }, { rowLabel: "B", seatNumber: "3" },
  ];
  assert.deepEqual(seatGrid({ zoneType: "Seated", seats, totalSeats: 6 }), { rows: 2, seatsPerRow: 3 });
});

test("seatGrid falls back to one row when the seats were not loaded", () => {
  assert.deepEqual(seatGrid({ zoneType: "Seated", totalSeats: 20 }), { rows: 1, seatsPerRow: 20 });
});

test("seatGrid of a seated zone without any seat is empty", () => {
  assert.deepEqual(seatGrid({ zoneType: "Seated" }), { rows: 0, seatsPerRow: 0 });
});
