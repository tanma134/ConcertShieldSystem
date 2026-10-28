// Venue scale convention shared by the organizer zone editor and the buyer seat map.
//
// Zones are stored as percentages of the canvas (x, y, w, h), so nothing changes
// in the database. This file only gives those percentages a real-world meaning:
//
//   the drawing area is 1600 x 1000 px and 1 px = 0.1 m  ->  160 m x 100 m
//
// Change metersPerPx here and every label, warning and rule text updates with it.

export const VENUE_SCALE = {
  canvasWidthPx: 1600,
  canvasHeightPx: 1000,
  metersPerPx: 0.1,
  // Space one seat needs: 0.5 m side to side, 0.9 m between rows.
  seatPitchM: 0.5,
  rowPitchM: 0.9,
  // No zone may be drawn smaller than this on either side.
  minZoneSideM: 2,
};

// Removes floating point noise such as 160.00000000000003.
const round6 = (value) => Math.round(value * 1e6) / 1e6;

// Real length in metres of the whole canvas along one axis ("x" or "y").
const axisLengthM = (axis) => {
  const px = axis === "x" ? VENUE_SCALE.canvasWidthPx : VENUE_SCALE.canvasHeightPx;
  return round6(px * VENUE_SCALE.metersPerPx);
};

// Converts a percentage of the canvas into metres along the given axis.
export function pctToMeters(pct, axis) {
  return round6((pct / 100) * axisLengthM(axis));
}

// Converts metres into a percentage of the canvas along the given axis.
export function metersToPct(meters, axis) {
  return round6((meters / axisLengthM(axis)) * 100);
}

// Real width, height and area of a zone rectangle given in canvas percentages.
export function zoneRealSize(rect) {
  const widthM = pctToMeters(rect.w, "x");
  const heightM = pctToMeters(rect.h, "y");
  return { widthM, heightM, areaM2: round6(widthM * heightM) };
}

// Space needed to place rows x seatsPerRow seats with the standard pitch.
export function requiredFootprint(rows, seatsPerRow) {
  return {
    widthM: round6(Math.max(seatsPerRow, 0) * VENUE_SCALE.seatPitchM),
    heightM: round6(Math.max(rows, 0) * VENUE_SCALE.rowPitchM),
  };
}

// Snaps a position or size (canvas percent) to the nearest half metre so zones
// line up on the grid instead of landing on arbitrary decimals.
export function snapPct(pct, axis, stepM = 0.5) {
  const snappedM = Math.round(pctToMeters(pct, axis) / stepM) * stepM;
  return metersToPct(snappedM, axis);
}

// Rows and seats per row of a zone, read from its generated seats.
// Standing zones have no seats. When the seat list was not loaded (summary
// endpoints) the zone is treated as a single row so the footprint is still checked.
export function seatGrid(zone) {
  if (!zone || zone.zoneType !== "Seated") return { rows: 0, seatsPerRow: 0 };

  const total = zone.totalSeats || zone.seats?.length || zone.capacity || 0;
  if (!total) return { rows: 0, seatsPerRow: 0 };

  const rows = new Set((zone.seats || []).map((seat) => seat.rowLabel)).size || 1;
  return { rows, seatsPerRow: Math.max(1, Math.round(total / rows)) };
}

// Formats a length for labels: one decimal, no trailing ".0" (16 -> "16 m").
export function formatMeters(meters) {
  return `${Number(Number(meters).toFixed(1))} m`;
}

// Checks that a drawn zone is big enough for its seats.
// Standing zones (no seats) always fit; the minimum side is enforced elsewhere.
export function checkZoneFit({ rect, rows, seatsPerRow }) {
  const real = zoneRealSize(rect);
  const required = requiredFootprint(rows, seatsPerRow);
  const result = {
    fits: true,
    widthM: real.widthM,
    heightM: real.heightM,
    requiredWidthM: required.widthM,
    requiredHeightM: required.heightM,
    message: "",
  };

  if (rows <= 0 || seatsPerRow <= 0) return result;

  const epsilon = 1e-6;
  result.fits =
    real.widthM + epsilon >= required.widthM && real.heightM + epsilon >= required.heightM;

  if (!result.fits) {
    result.message =
      `Zone too small for ${rows} x ${seatsPerRow} seats: needs at least ` +
      `${formatMeters(required.widthM)} x ${formatMeters(required.heightM)}, ` +
      `drawn ${formatMeters(real.widthM)} x ${formatMeters(real.heightM)}.`;
  }

  return result;
}

// Smallest width / height (in canvas percent) a zone may be resized to.
export function minZonePct(rows, seatsPerRow) {
  const required = requiredFootprint(rows, seatsPerRow);
  return {
    w: metersToPct(Math.max(VENUE_SCALE.minZoneSideM, required.widthM), "x"),
    h: metersToPct(Math.max(VENUE_SCALE.minZoneSideM, required.heightM), "y"),
  };
}

// The rules shown to organizers by the "Scale rules" button.
export function scaleRules() {
  const widthM = axisLengthM("x");
  const heightM = axisLengthM("y");

  return [
    `The drawing area is ${VENUE_SCALE.canvasWidthPx} x ${VENUE_SCALE.canvasHeightPx} px and 1 px = ${VENUE_SCALE.metersPerPx} m, so the whole venue is ${widthM} m x ${heightM} m.`,
    "Draw the stage and every zone at its real size: the label on each zone shows its width x depth in metres.",
    `A seated seat takes ${VENUE_SCALE.seatPitchM} m side to side and ${VENUE_SCALE.rowPitchM} m front to back. A zone that is too small for its seats is flagged in red.`,
    `No zone can be smaller than ${VENUE_SCALE.minZoneSideM} m x ${VENUE_SCALE.minZoneSideM} m.`,
    "Gridlines are drawn every 10 m so distances are easy to read; the ruler on the left and top uses the same scale.",
  ];
}
