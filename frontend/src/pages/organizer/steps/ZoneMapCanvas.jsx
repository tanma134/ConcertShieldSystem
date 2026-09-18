import { useEffect, useRef, useState } from "react";
import { formatPrice } from "../../../utils/format";

// Ticketbox-style palette: one color per ticket type, shared by every zone that
// uses it (so the canvas and the price legend always agree on what a color means).
const PALETTE = [
  "#f59e0b", // amber
  "#14b8a6", // teal
  "#a855f7", // purple
  "#3b82f6", // blue
  "#ef4444", // red
  "#22c55e", // green
  "#ec4899", // pink
  "#84cc16", // lime
  "#06b6d4", // cyan
  "#f97316", // orange
];

function colorForTicketType(ticketTypeId) {
  const n = Number(ticketTypeId) || 0;
  return PALETTE[n % PALETTE.length];
}

function parseRect(shapeJson) {
  if (!shapeJson) return null;
  try {
    const r = JSON.parse(shapeJson);
    if (
      typeof r.x === "number" &&
      typeof r.y === "number" &&
      typeof r.w === "number" &&
      typeof r.h === "number"
    ) {
      return r;
    }
  } catch {
    // fall through to auto layout
  }
  return null;
}

// Default grid position (percent of canvas) for a zone that hasn't been placed
// yet, so it's visible immediately instead of stacking at 0,0.
function autoRect(index, total) {
  const cols = Math.min(4, Math.max(total, 1));
  const col = index % cols;
  const row = Math.floor(index / cols);
  const w = 90 / cols - 3;
  const h = 16;
  return {
    x: 5 + col * (90 / cols),
    y: 28 + row * (h + 6),
    w,
    h,
  };
}

const clamp = (v, min, max) => Math.min(Math.max(v, min), max);

export default function ZoneMapCanvas({ zones, ticketTypes, readOnly, onZoneMove }) {
  const canvasRef = useRef(null);
  const [liveRects, setLiveRects] = useState({}); // zoneId -> rect while dragging/resizing
  const dragState = useRef(null);

  const ticketTypeById = (id) => ticketTypes.find((t) => t.ticketTypeId === id);

  const rectFor = (zone, index) =>
    liveRects[zone.seatZoneId] || parseRect(zone.shapeJson) || autoRect(index, zones.length);

  useEffect(() => {
    // Drop the live override once the parent's copy of the zone catches up
    // with what we just persisted, so future renders read from props again.
    setLiveRects((prev) => {
      const next = { ...prev };
      let changed = false;
      for (const zone of zones) {
        const saved = parseRect(zone.shapeJson);
        const live = prev[zone.seatZoneId];
        if (
          live &&
          saved &&
          Math.abs(saved.x - live.x) < 0.5 &&
          Math.abs(saved.y - live.y) < 0.5 &&
          Math.abs(saved.w - live.w) < 0.5 &&
          Math.abs(saved.h - live.h) < 0.5
        ) {
          delete next[zone.seatZoneId];
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [zones]);

  const startDrag = (e, zone, index, mode) => {
    if (readOnly) return;
    e.preventDefault();
    e.stopPropagation();

    const canvasEl = canvasRef.current;
    if (!canvasEl) return;
    const canvasBox = canvasEl.getBoundingClientRect();
    const startRect = rectFor(zone, index);

    dragState.current = {
      zoneId: zone.seatZoneId,
      mode, // "move" | "resize"
      startX: e.clientX,
      startY: e.clientY,
      startRect,
      canvasBox,
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
  };

  const handleMouseMove = (e) => {
    const d = dragState.current;
    if (!d) return;

    const dxPct = ((e.clientX - d.startX) / d.canvasBox.width) * 100;
    const dyPct = ((e.clientY - d.startY) / d.canvasBox.height) * 100;

    let rect = { ...d.startRect };

    if (d.mode === "move") {
      rect.x = clamp(d.startRect.x + dxPct, 0, 100 - rect.w);
      rect.y = clamp(d.startRect.y + dyPct, 0, 100 - rect.h);
    } else {
      rect.w = clamp(d.startRect.w + dxPct, 6, 100 - rect.x);
      rect.h = clamp(d.startRect.h + dyPct, 6, 100 - rect.y);
    }

    setLiveRects((prev) => ({ ...prev, [d.zoneId]: rect }));
  };

  const handleMouseUp = () => {
    const d = dragState.current;
    window.removeEventListener("mousemove", handleMouseMove);
    window.removeEventListener("mouseup", handleMouseUp);
    dragState.current = null;

    if (!d) return;
    const finalRect = liveRects[d.zoneId];
    if (finalRect) onZoneMove(d.zoneId, finalRect);
  };

  return (
    <div className="ow-zonemap">
      <div className="ow-zonemap-canvas" ref={canvasRef}>
        <div className="ow-zonemap-stage">STAGE</div>

        {zones.map((zone, index) => {
          const rect = rectFor(zone, index);
          const color = colorForTicketType(zone.ticketTypeId);
          const tt = ticketTypeById(zone.ticketTypeId);

          return (
            <div
              key={zone.seatZoneId}
              className={
                "ow-zonemap-zone" +
                (zone.zoneType === "Standing" ? " ow-zonemap-zone-standing" : "")
              }
              style={{
                left: `${rect.x}%`,
                top: `${rect.y}%`,
                width: `${rect.w}%`,
                height: `${rect.h}%`,
                background: color,
                cursor: readOnly ? "default" : "move",
              }}
              onMouseDown={(e) => startDrag(e, zone, index, "move")}
              title={tt ? `${zone.zoneName} — ${tt.typeName}` : zone.zoneName}
            >
              <span className="ow-zonemap-zone-label">{zone.zoneName}</span>
              <span className="ow-zonemap-zone-type">
                {zone.zoneType === "Seated" ? "🎫 Seated" : "🧍 Standing"}
              </span>

              {!readOnly && (
                <span
                  className="ow-zonemap-resize-handle"
                  onMouseDown={(e) => startDrag(e, zone, index, "resize")}
                />
              )}
            </div>
          );
        })}

        {zones.length === 0 && (
          <div className="ow-zonemap-empty">
            Add a zone in the form below — it will appear here, and you can
            drag it to arrange its position on the map.
          </div>
        )}
      </div>

      <div className="ow-zonemap-legend">
        <div className="ow-zonemap-legend-title">Ticket prices</div>
        {zones.length === 0 && (
          <div className="ow-hint">No zones yet.</div>
        )}
        {zones.map((zone) => {
          const tt = ticketTypeById(zone.ticketTypeId);
          const color = colorForTicketType(zone.ticketTypeId);
          return (
            <div key={zone.seatZoneId} className="ow-zonemap-legend-row">
              <span
                className="ow-zonemap-legend-swatch"
                style={{ background: color }}
              />
              <span className="ow-zonemap-legend-name">
                {zone.zoneName}{" "}
                <span className="ow-hint">
                  ({zone.zoneType === "Seated" ? "Seated" : "Standing"})
                </span>
              </span>
              <span className="ow-zonemap-legend-price">
                {tt ? formatPrice(tt.price) : "—"}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
