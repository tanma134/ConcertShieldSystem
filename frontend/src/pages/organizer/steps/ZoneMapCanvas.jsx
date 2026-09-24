import { useEffect, useMemo, useRef, useState } from "react";
import { Stage, Layer, Rect, Text, Transformer, Group } from "react-konva";
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
      return { x: r.x, y: r.y, w: r.w, h: r.h, rot: typeof r.rot === "number" ? r.rot : 0 };
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
    rot: 0,
  };
}

const clamp = (v, min, max) => Math.min(Math.max(v, min), max);
const MIN_W_PCT = 6;
const MIN_H_PCT = 6;

/**
 * Venue layout editor drawn on a real HTML5 <canvas> (via Konva/react-konva),
 * with proper drag + resize (Transformer) handles — replacing the earlier
 * absolutely-positioned <div> hack. This is the "sơ đồ vé dùng canvas js"
 * upgrade requested by the mentor, modeled on how Ticketbox/Ticketmaster-style
 * editors let an organizer draw zones and see buyers' exact view underneath.
 *
 * Positions/sizes are still persisted as percentages (0-100) of the stage in
 * the same shapeJson shape the backend already stores ({x,y,w,h}) — no schema
 * change needed, and onZoneMove(seatZoneId, rect) keeps the exact same contract
 * the rest of the wizard already relies on.
 */
export default function ZoneMapCanvas({ zones, ticketTypes, readOnly, onZoneMove }) {
  const wrapperRef = useRef(null);
  const [stageSize, setStageSize] = useState({ width: 800, height: 500 });
  const [selectedId, setSelectedId] = useState(null);
  const [liveRects, setLiveRects] = useState({}); // zoneId -> rect while dragging/resizing

  useEffect(() => {
    const el = wrapperRef.current;
    if (!el) return undefined;

    const measure = () => {
      const rect = el.getBoundingClientRect();
      if (rect.width > 0 && rect.height > 0) {
        setStageSize({ width: rect.width, height: rect.height });
      }
    };

    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const ticketTypeById = (id) => ticketTypes.find((t) => t.ticketTypeId === id);

  const rectFor = (zone, index) =>
    liveRects[zone.seatZoneId] || parseRect(zone.shapeJson) || autoRect(index, zones.length);

  useEffect(() => {
    // Drop the live override once the parent's copy of the zone catches up with
    // what we just persisted, so future renders read from props again.
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

  const shapes = useMemo(
    () => zones.map((zone, index) => ({ zone, rect: rectFor(zone, index) })),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [zones, liveRects]
  );

  const commitRect = (seatZoneId, rect) => {
    const clamped = {
      x: clamp(rect.x, 0, 100 - rect.w),
      y: clamp(rect.y, 0, 100 - rect.h),
      w: clamp(rect.w, MIN_W_PCT, 100),
      h: clamp(rect.h, MIN_H_PCT, 100),
      rot: ((Math.round((rect.rot || 0) * 10) / 10) % 360 + 360) % 360,
    };
    setLiveRects((prev) => ({ ...prev, [seatZoneId]: clamped }));
    if (!readOnly) onZoneMove(seatZoneId, clamped);
  };

  const { width: stageWidth, height: stageHeight } = stageSize;

  return (
    <div className="ow-zonemap">
      <div className="ow-zonemap-canvas" ref={wrapperRef}>
        {stageWidth > 0 && stageHeight > 0 && (
          <Stage
            width={stageWidth}
            height={stageHeight}
            onMouseDown={(e) => {
              if (e.target === e.target.getStage()) setSelectedId(null);
            }}
          >
            <Layer>
              {/* Static "STAGE" bar for orientation, matching the buyer-facing view. */}
              <Rect
                x={stageWidth * 0.3}
                y={stageHeight * 0.04}
                width={stageWidth * 0.4}
                height={stageHeight * 0.14}
                fill="#3f3f46"
                cornerRadius={6}
                listening={false}
              />
              <Text
                text="STAGE"
                x={stageWidth * 0.3}
                y={stageHeight * 0.04}
                width={stageWidth * 0.4}
                height={stageHeight * 0.14}
                align="center"
                verticalAlign="middle"
                fontSize={12}
                fontStyle="bold"
                fill="#e5e7eb"
                listening={false}
              />

              {shapes.map(({ zone, rect }) => (
                <ZoneShape
                  key={zone.seatZoneId}
                  zone={zone}
                  rect={rect}
                  stageWidth={stageWidth}
                  stageHeight={stageHeight}
                  color={colorForTicketType(zone.ticketTypeId)}
                  ticketType={ticketTypeById(zone.ticketTypeId)}
                  readOnly={readOnly}
                  selected={selectedId === zone.seatZoneId}
                  onSelect={() => setSelectedId(zone.seatZoneId)}
                  onChange={(nextRect) => commitRect(zone.seatZoneId, nextRect)}
                />
              ))}
            </Layer>
          </Stage>
        )}

        {zones.length === 0 && (
          <div className="ow-zonemap-empty">
            Add a zone in the form below — it will appear here, and you can
            drag or resize it to arrange its position on the map.
          </div>
        )}
      </div>

      <div className="ow-zonemap-legend">
        <div className="ow-zonemap-legend-title">Ticket prices</div>
        {zones.length === 0 && <div className="ow-hint">No zones yet.</div>}
        {zones.map((zone) => {
          const tt = ticketTypeById(zone.ticketTypeId);
          const color = colorForTicketType(zone.ticketTypeId);
          return (
            <div key={zone.seatZoneId} className="ow-zonemap-legend-row">
              <span className="ow-zonemap-legend-swatch" style={{ background: color }} />
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

function ZoneShape({
  zone,
  rect,
  stageWidth,
  stageHeight,
  color,
  ticketType,
  readOnly,
  selected,
  onSelect,
  onChange,
}) {
  const groupRef = useRef(null);
  const trRef = useRef(null);

  useEffect(() => {
    if (selected && !readOnly && trRef.current && groupRef.current) {
      trRef.current.nodes([groupRef.current]);
      trRef.current.getLayer().batchDraw();
    }
  }, [selected, readOnly]);

  const px = (rect.x / 100) * stageWidth;
  const py = (rect.y / 100) * stageHeight;
  const pw = (rect.w / 100) * stageWidth;
  const ph = (rect.h / 100) * stageHeight;
  const rotation = rect.rot || 0;

  const label = ticketType ? `${zone.zoneName}\n${ticketType.typeName}` : zone.zoneName;
  // Small zones (a real venue map easily has 20-30 of them - see the CAT 1..15
  // L/R style layouts) still need readable text, so the font shrinks with the box
  // instead of staying fixed and overflowing/vanishing on tightly-packed charts.
  const fontSize = clamp(Math.min(pw, ph) * 0.22, 8, 13);

  return (
    <>
      <Group
        ref={groupRef}
        x={px}
        y={py}
        rotation={rotation}
        draggable={!readOnly}
        onClick={onSelect}
        onTap={onSelect}
        onDragEnd={(e) => {
          onChange({
            x: (e.target.x() / stageWidth) * 100,
            y: (e.target.y() / stageHeight) * 100,
            w: rect.w,
            h: rect.h,
            rot: rotation,
          });
        }}
        onTransformEnd={() => {
          const node = groupRef.current;
          const scaleX = node.scaleX();
          const scaleY = node.scaleY();

          const newPw = Math.max(20, pw * scaleX);
          const newPh = Math.max(20, ph * scaleY);
          const newRotation = node.rotation();

          // Reset the Group's scale (Transformer only ever changes scale/rotation,
          // never width/height directly) - the Rect/Text below get their real
          // pixel size from the next render's `rect.w`/`rect.h`, once onChange
          // below updates the parent's percentage-based state.
          node.scaleX(1);
          node.scaleY(1);

          onChange({
            x: (node.x() / stageWidth) * 100,
            y: (node.y() / stageHeight) * 100,
            w: (newPw / stageWidth) * 100,
            h: (newPh / stageHeight) * 100,
            rot: newRotation,
          });
        }}
      >
        <Rect
          width={pw}
          height={ph}
          fill={color}
          stroke={selected ? "#ffffff" : "rgba(255,255,255,0.35)"}
          strokeWidth={selected ? 2 : 1}
          dash={zone.zoneType === "Standing" ? [6, 4] : undefined}
          cornerRadius={6}
        />
        <Text
          text={label}
          width={pw}
          height={ph}
          align="center"
          verticalAlign="middle"
          fontSize={fontSize}
          fontStyle="bold"
          fill="#0b0d12"
          wrap="word"
          ellipsis
          listening={false}
          padding={3}
        />
      </Group>

      {selected && !readOnly && (
        <Transformer
          ref={trRef}
          rotateEnabled
          rotationSnaps={[0, 15, 30, 45, 60, 75, 90, 105, 120, 135, 150, 165, 180, 195, 210, 225, 240, 255, 270, 285, 300, 315, 330, 345]}
          keepRatio={false}
          borderStroke="#ffffff"
          anchorStroke="#ffffff"
          anchorFill="#111827"
          boundBoxFunc={(oldBox, newBox) => {
            if (newBox.width < 20 || newBox.height < 20) return oldBox;
            return newBox;
          }}
        />
      )}
    </>
  );
}
