const MAX_PREVIEW_ROWS = 40;
const MAX_PREVIEW_COLS = 60;

function rowLabelSequence(prefix, count) {
  const base = (prefix || "A").toUpperCase().replace(/[^A-Z]/g, "") || "A";
  const labels = [];
  let current = base;

  for (let i = 0; i < count; i += 1) {
    labels.push(current);
    const chars = current.split("");
    let index = chars.length - 1;
    while (index >= 0 && chars[index] === "Z") {
      chars[index] = "A";
      index -= 1;
    }
    if (index < 0) chars.unshift("A");
    else chars[index] = String.fromCharCode(chars[index].charCodeAt(0) + 1);
    current = chars.join("");
  }
  return labels;
}

function seatLabel(seat) {
  return `${seat.rowLabel || ""}${seat.seatNumber || ""}`;
}

// EventAPI renders physical seats only. `selectable` is intentionally UI-only so
// Booking can reuse this grid later without moving hold/sold state into EventAPI.
export default function SeatGridPreview({
  mode,
  rows,
  seatsPerRow,
  rowLabelPrefix,
  seats,
  selectable = false,
  selectedSeatIds = [],
  onSeatSelect,
}) {
  const selected = new Set(selectedSeatIds);

  if (mode === "plan") {
    const totalRows = Number(rows) || 0;
    const totalCols = Number(seatsPerRow) || 0;
    if (!totalRows || !totalCols) return null;

    const rowCount = Math.min(totalRows, MAX_PREVIEW_ROWS);
    const colCount = Math.min(totalCols, MAX_PREVIEW_COLS);
    const labels = rowLabelSequence(rowLabelPrefix, rowCount);

    return (
      <div className="ow-seat-preview">
        <div className="ow-seat-preview-title">
          {totalRows} rows × {totalCols} seats = {totalRows * totalCols} physical seats
        </div>
        <div className="ow-seat-stage">STAGE</div>
        <div className="ow-seat-grid">
          {labels.map((label) => (
            <div className="ow-seat-row" key={label}>
              <span className="ow-seat-row-label">{label}</span>
              {Array.from({ length: colCount }, (_, index) => (
                <span key={index} className="ow-seat ow-seat-planned" title={`${label}${index + 1}`}>
                  <span className="ow-seat-label">{index + 1}</span>
                </span>
              ))}
              {totalCols > colCount && <span className="ow-seat-more">+{totalCols - colCount}</span>}
            </div>
          ))}
          {totalRows > rowCount && <div className="ow-seat-more-rows">+{totalRows - rowCount} more rows</div>}
        </div>
      </div>
    );
  }

  if (!seats?.length) return <div className="ow-hint">No physical seats in this zone.</div>;

  const byRow = new Map();
  [...seats]
    .sort((a, b) => (a.yCoordinate ?? 0) - (b.yCoordinate ?? 0) || (a.xCoordinate ?? 0) - (b.xCoordinate ?? 0))
    .forEach((seat) => {
      const key = seat.rowLabel || "?";
      if (!byRow.has(key)) byRow.set(key, []);
      byRow.get(key).push(seat);
    });

  const rowsToShow = [...byRow.entries()].slice(0, MAX_PREVIEW_ROWS);
  return (
    <div className="ow-seat-preview">
      <div className="ow-seat-stage">STAGE</div>
      {selectable && <div className="ow-hint">Click a seat to select it.</div>}
      <div className="ow-seat-grid">
        {rowsToShow.map(([rowLabel, rowSeats]) => (
          <div className="ow-seat-row" key={rowLabel}>
            <span className="ow-seat-row-label">{rowLabel}</span>
            {rowSeats.slice(0, MAX_PREVIEW_COLS).map((seat) => {
              const isSelected = selected.has(seat.seatId);
              const className = `ow-seat ow-seat-physical${isSelected ? " ow-seat-selected" : ""}${selectable ? " ow-seat-clickable" : ""}`;
              return selectable ? (
                <button
                  type="button"
                  key={seat.seatId}
                  className={className}
                  title={seatLabel(seat)}
                  aria-label={`Seat ${seatLabel(seat)}`}
                  aria-pressed={isSelected}
                  onClick={() => onSeatSelect?.(seat)}
                >
                  <span className="ow-seat-label">{seat.seatNumber}</span>
                </button>
              ) : (
                <span key={seat.seatId} className={className} title={seatLabel(seat)}>
                  <span className="ow-seat-label">{seat.seatNumber}</span>
                </span>
              );
            })}
            {rowSeats.length > MAX_PREVIEW_COLS && <span className="ow-seat-more">+{rowSeats.length - MAX_PREVIEW_COLS}</span>}
          </div>
        ))}
        {byRow.size > MAX_PREVIEW_ROWS && <div className="ow-seat-more-rows">+{byRow.size - MAX_PREVIEW_ROWS} more rows</div>}
      </div>
    </div>
  );
}
