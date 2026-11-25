import { useEffect, useMemo, useRef, useState } from "react";
import { Stage, Layer, Rect, Text, Transformer, Group, Circle, Line } from "react-konva";
import { formatPrice } from "../../../utils/format";

const PALETTE = ["#d98f14", "#14a89a", "#a855f7", "#3b82f6", "#d63a3a", "#1eaa55", "#e8439c", "#78b014", "#1da8c8", "#e06a1a"];
const clamp = (v, min, max) => Math.min(Math.max(v, min), max);
const MIN_W_PCT = 6;
const MIN_H_PCT = 6;

function colorForTicketType(ticketTypeId) {
  return PALETTE[(Number(ticketTypeId) || 0) % PALETTE.length];
}

// Tint ticket-type colors into translucent glass cards so seat markers stay legible.
function hexToRgba(hex, alpha) {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

// Pick black or white text based on background luminance for contrast.
function readableTextColor(hex) {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance > 0.6 ? "#14151a" : "#ffffff";
}

function getStageHitboxes(stage) {
  if (!stage) return [];
  const x = Number(stage.x) || 0;
  const y = Number(stage.y) || 0;
  const w = Number(stage.w) || 0;
  const h = Number(stage.h) || 0;
  const type = stage.stageType || "standard";

  if (type === "t-stage") {
    return [
      { x, y, w, h: h * 0.44 }, // Main Stage
      { x: x + w * 0.41, y: y + h * 0.44, w: w * 0.18, h: h * 0.36 }, // Runway
      { x: x + w * 0.28, y: y + h * 0.80, w: w * 0.44, h: h * 0.20 }, // Stage B
    ];
  }
  if (type === "wings") {
    return [
      { x: x + w * 0.2, y, w: w * 0.6, h: h * 0.55 }, // Main center
      { x, y, w: w * 0.18, h: h * 0.85 }, // Left wing
      { x: x + w * 0.82, y, w: w * 0.18, h: h * 0.85 }, // Right wing
    ];
  }
  return [{ x, y, w, h }];
}

function rectsOverlap(a, b) {
  const pad = 0.2; // tiny tolerance in %
  return (
    a.x < b.x + b.w - pad &&
    a.x + a.w > b.x + pad &&
    a.y < b.y + b.h - pad &&
    a.y + a.h > b.y + pad
  );
}

function zoneShapeBounds(shape) {
  if (!shape) return { x: 0, y: 0, w: 0, h: 0 };
  if (shape.type === "polygon" && Array.isArray(shape.points) && shape.points.length) {
    const xs = shape.points.map((p) => p.x);
    const ys = shape.points.map((p) => p.y);
    const minX = Math.min(...xs);
    const minY = Math.min(...ys);
    return { x: minX, y: minY, w: Math.max(...xs) - minX, h: Math.max(...ys) - minY };
  }
  return { x: Number(shape.x) || 0, y: Number(shape.y) || 0, w: Number(shape.w) || 0, h: Number(shape.h) || 0 };
}

function doesZoneOverlapStage(zoneShape, stage) {
  const zb = zoneShapeBounds(zoneShape);
  return getStageHitboxes(stage).some((sb) => rectsOverlap(zb, sb));
}

function clampZoneInsideCanvas(shape) {
  if (!shape) return shape;
  if (shape.type === "polygon" && Array.isArray(shape.points)) {
    return {
      ...shape,
      points: shape.points.map((p) => ({
        x: clamp(p.x, 0, 100),
        y: clamp(p.y, 0, 100),
      })),
    };
  }
  const w = clamp(Number(shape.w) || MIN_W_PCT, MIN_W_PCT, 100);
  const h = clamp(Number(shape.h) || MIN_H_PCT, MIN_H_PCT, 100);
  return {
    ...shape,
    w,
    h,
    x: clamp(Number(shape.x) || 0, 0, Math.max(0, 100 - w)),
    y: clamp(Number(shape.y) || 0, 0, Math.max(0, 100 - h)),
  };
}

function StageFloor({ width, height }) {
  const cols = 12;
  const rows = 8;
  const lines = [];
  for (let i = 1; i < cols; i += 1) {
    const x = (i / cols) * width;
    lines.push(<Line key={`v${i}`} points={[x, 0, x, height]} stroke="rgba(255,255,255,.035)" strokeWidth={1} listening={false} />);
  }
  for (let i = 1; i < rows; i += 1) {
    const y = (i / rows) * height;
    lines.push(<Line key={`h${i}`} points={[0, y, width, y]} stroke="rgba(255,255,255,.035)" strokeWidth={1} listening={false} />);
  }
  return <>
    <Rect x={0} y={0} width={width} height={height} fillRadialGradientStartPoint={{ x: width / 2, y: height * 0.3 }} fillRadialGradientEndPoint={{ x: width / 2, y: height * 0.3 }} fillRadialGradientStartRadius={0} fillRadialGradientEndRadius={Math.max(width, height) * 0.75} fillRadialGradientColorStops={[0, "rgba(99,102,241,.10)", 1, "rgba(11,13,18,0)"]} listening={false} />
    {lines}
  </>;
}

function renderStageVisual(stageType, pw, ph, stageWidthM, stageHeightM, stageName, selected, hovered) {
  const gradientStops = [0, "#cfc4de", 0.6, "#e9d5ff", 1, "#f3e8ff"];
  const title = stageName || "S T A G E";

  if (stageType === "t-stage") {
    // T-stage geometry: main stage, center catwalk, and sub-stage with open side flanks for audience zones.
    const mainH = ph * 0.44;
    const runwayW = pw * 0.18;
    const runwayH = ph * 0.36;
    const runwayX = (pw - runwayW) / 2;
    const subW = pw * 0.44;
    const subH = ph * 0.20;
    const subX = (pw - subW) / 2;
    const subY = mainH + runwayH;

    const tPoints = [
      0, 0,                      // Top-Left Main Stage
      pw, 0,                     // Top-Right Main Stage
      pw, mainH,                 // Bottom-Right Main Stage
      runwayX + runwayW, mainH,  // Runway Top-Right
      runwayX + runwayW, subY,   // Runway Bottom-Right / Sub-Stage Top
      subX + subW, subY,         // Sub-Stage Top-Right
      subX + subW, ph,           // Sub-Stage Bottom-Right
      subX, ph,                  // Sub-Stage Bottom-Left
      subX, subY,                // Sub-Stage Top-Left
      runwayX, subY,             // Runway Bottom-Left
      runwayX, mainH,            // Runway Top-Left
      0, mainH,                  // Bottom-Left Main Stage
    ];

    return (
      <>
        <Line
          points={tPoints}
          closed
          fillLinearGradientStartPoint={{ x: 0, y: 0 }}
          fillLinearGradientEndPoint={{ x: 0, y: ph }}
          fillLinearGradientColorStops={gradientStops}
          stroke={selected ? "#7e22ce" : hovered ? "rgba(126,34,206,.8)" : "#9333ea"}
          strokeWidth={selected ? 2.5 : 1.5}
          shadowColor="#000"
          shadowBlur={selected ? 16 : 8}
          shadowOpacity={selected ? 0.4 : 0.2}
          shadowOffset={{ x: 0, y: 3 }}
        />

        <Line
          points={[runwayX, mainH, runwayX + runwayW, mainH]}
          stroke="rgba(126, 34, 206, 0.35)"
          strokeWidth={1.2}
          dash={[3, 3]}
        />
        <Line
          points={[subX, subY, subX + subW, subY]}
          stroke="rgba(126, 34, 206, 0.35)"
          strokeWidth={1.2}
          dash={[3, 3]}
        />

        <Text
          text={`${title}\n(MAIN STAGE)`}
          x={0}
          y={0}
          width={pw}
          height={mainH}
          align="center"
          verticalAlign="middle"
          fontSize={clamp(Math.min(pw, mainH) * 0.22, 10, 14)}
          fontStyle="bold"
          fill="#3b0764"
          listening={false}
        />

        <Text
          text={"R\nU\nN\nW\nA\nY"}
          x={runwayX}
          y={mainH}
          width={runwayW}
          height={runwayH}
          align="center"
          verticalAlign="middle"
          fontSize={8.5}
          fontStyle="bold"
          fill="#7e22ce"
          listening={false}
          lineHeight={1.15}
        />

        <Text
          text={`STAGE B\n(${stageWidthM}m × ${stageHeightM}m)`}
          x={subX}
          y={subY}
          width={subW}
          height={subH}
          align="center"
          verticalAlign="middle"
          fontSize={9}
          fontStyle="bold"
          fill="#3b0764"
          listening={false}
        />
      </>
    );
  }

  if (stageType === "wings") {
    const mainW = pw * 0.6;
    const mainX = (pw - mainW) / 2;
    const wingW = pw * 0.18;
    const wingH = ph * 0.85;

    return (
      <>
        <Rect
          x={mainX}
          y={0}
          width={mainW}
          height={ph * 0.55}
          cornerRadius={8}
          fillLinearGradientStartPoint={{ x: 0, y: 0 }}
          fillLinearGradientEndPoint={{ x: 0, y: ph * 0.55 }}
          fillLinearGradientColorStops={gradientStops}
          stroke={selected ? "#7e22ce" : "#9333ea"}
          strokeWidth={selected ? 2.5 : 1.5}
        />
        <Rect
          x={0}
          y={0}
          width={wingW}
          height={wingH}
          cornerRadius={6}
          fill="#d8b4fe"
          stroke={selected ? "#7e22ce" : "#9333ea"}
          strokeWidth={1.5}
        />
        <Rect
          x={pw - wingW}
          y={0}
          width={wingW}
          height={wingH}
          cornerRadius={6}
          fill="#d8b4fe"
          stroke={selected ? "#7e22ce" : "#9333ea"}
          strokeWidth={1.5}
        />
        <Text
          text={`${title}\n(${stageWidthM}m × ${stageHeightM}m)`}
          x={mainX}
          y={0}
          width={mainW}
          height={ph * 0.55}
          align="center"
          verticalAlign="middle"
          fontSize={clamp(Math.min(mainW, ph) * 0.18, 9, 13)}
          fontStyle="bold"
          fill="#3b0764"
          listening={false}
        />
        <Text text="WING L" x={0} y={wingH * 0.4} width={wingW} align="center" fontSize={8} fontStyle="bold" fill="#6b21a8" listening={false} />
        <Text text="WING R" x={pw - wingW} y={wingH * 0.4} width={wingW} align="center" fontSize={8} fontStyle="bold" fill="#6b21a8" listening={false} />
      </>
    );
  }

  if (stageType === "center") {
    return (
      <>
        <Rect
          width={pw}
          height={ph}
          cornerRadius={Math.min(pw, ph) * 0.45}
          fillLinearGradientStartPoint={{ x: 0, y: 0 }}
          fillLinearGradientEndPoint={{ x: pw, y: ph }}
          fillLinearGradientColorStops={gradientStops}
          stroke={selected ? "#7e22ce" : hovered ? "rgba(126,34,206,.7)" : "rgba(126,34,206,.3)"}
          strokeWidth={selected ? 2.5 : 1.4}
        />
        <Text
          text={`${title}\n(360° CENTER STAGE)\n${stageWidthM}m × ${stageHeightM}m`}
          width={pw}
          height={ph}
          align="center"
          verticalAlign="middle"
          fontSize={clamp(Math.min(pw, ph) * 0.2, 9, 13)}
          fontStyle="bold"
          fill="#3b0764"
          listening={false}
          lineHeight={1.25}
        />
      </>
    );
  }

  return (
    <>
      <Rect
        width={pw}
        height={ph}
        cornerRadius={10}
        fillLinearGradientStartPoint={{ x: 0, y: 0 }}
        fillLinearGradientEndPoint={{ x: 0, y: ph }}
        fillLinearGradientColorStops={gradientStops}
        stroke={selected ? "#7e22ce" : hovered ? "rgba(126,34,206,.7)" : "rgba(126,34,206,.3)"}
        strokeWidth={selected ? 2.5 : 1.4}
        shadowColor="#000"
        shadowBlur={selected ? 16 : 8}
        shadowOpacity={selected ? 0.45 : 0.22}
        shadowOffset={{ x: 0, y: 3 }}
      />
      <Text
        text={`${title}\n(${stageWidthM} m × ${stageHeightM} m)`}
        width={pw}
        height={ph}
        align="center"
        verticalAlign="middle"
        fontSize={clamp(Math.min(pw, ph) * 0.22, 10, 14)}
        fontStyle="bold"
        fill="#3b0764"
        listening={false}
        lineHeight={1.3}
      />
    </>
  );
}

function StageItem({ shape, stageWidth, stageHeight, readOnly, selected, onSelect, onChange }) {
  const groupRef = useRef(null);
  const trRef = useRef(null);
  const [hovered, setHovered] = useState(false);

  useEffect(() => {
    if (selected && !readOnly && trRef.current && groupRef.current) {
      trRef.current.nodes([groupRef.current]);
      trRef.current.getLayer()?.batchDraw();
    }
  }, [selected, readOnly]);

  const px = (shape.x / 100) * stageWidth;
  const py = (shape.y / 100) * stageHeight;
  const pw = (shape.w / 100) * stageWidth;
  const ph = (shape.h / 100) * stageHeight;
  const stageWidthM = ((shape.w / 100) * 160).toFixed(0);
  const stageHeightM = ((shape.h / 100) * 100).toFixed(0);

  return (
    <>
      <Group
        ref={groupRef}
        x={px}
        y={py}
        rotation={shape.rot || 0}
        draggable={!readOnly}
        dragBoundFunc={(pos) => ({
          x: clamp(pos.x, 0, Math.max(0, stageWidth - pw)),
          y: clamp(pos.y, 0, Math.max(0, stageHeight - ph)),
        })}
        onClick={onSelect}
        onTap={onSelect}
        onMouseEnter={(e) => {
          setHovered(true);
          if (!readOnly) e.target.getStage().container().style.cursor = "grab";
        }}
        onMouseLeave={(e) => {
          setHovered(false);
          e.target.getStage().container().style.cursor = "default";
        }}
        onDragStart={(e) => {
          e.target.getStage().container().style.cursor = "grabbing";
        }}
        onDragEnd={(e) => {
          e.target.getStage().container().style.cursor = "grab";
          const next = {
            ...shape,
            x: clamp((e.target.x() / stageWidth) * 100, 0, Math.max(0, 100 - shape.w)),
            y: clamp((e.target.y() / stageHeight) * 100, 0, Math.max(0, 100 - shape.h)),
          };
          const ok = onChange(next);
          if (ok === false) {
            e.target.position({ x: px, y: py });
          }
        }}
        onTransformEnd={() => {
          const node = groupRef.current;
          if (!node) return;
          const nextW = Math.max(40, pw * node.scaleX());
          const nextH = Math.max(20, ph * node.scaleY());
          node.scaleX(1);
          node.scaleY(1);
          const wPct = clamp((nextW / stageWidth) * 100, 5, 95);
          const hPct = clamp((nextH / stageHeight) * 100, 3, 60);
          const next = {
            ...shape,
            x: clamp((node.x() / stageWidth) * 100, 0, Math.max(0, 100 - wPct)),
            y: clamp((node.y() / stageHeight) * 100, 0, Math.max(0, 100 - hPct)),
            w: wPct,
            h: hPct,
            rot: node.rotation(),
          };
          const ok = onChange(next);
          if (ok === false) {
            node.position({ x: px, y: py });
          }
        }}
      >
        {renderStageVisual(shape.stageType || "standard", pw, ph, stageWidthM, stageHeightM, shape.name, selected, hovered)}
      </Group>
      {selected && !readOnly && (
        <Transformer
          ref={trRef}
          rotateEnabled
          keepRatio={false}
          borderStroke="#7e22ce"
          borderStrokeWidth={1.5}
          anchorStroke="#7e22ce"
          anchorFill="#ffffff"
          anchorCornerRadius={4}
          anchorSize={9}
          boundBoxFunc={(oldBox, newBox) =>
            newBox.width < 40 || newBox.height < 20 ? oldBox : newBox
          }
        />
      )}
    </>
  );
}

// Support both legacy rectangle JSON and polygon shapes for backward compatibility.
function parseZoneShape(shapeJson) {
  if (!shapeJson) return null;
  try {
    const shape = JSON.parse(shapeJson);
    if (shape.type === "polygon" && Array.isArray(shape.points) && shape.points.length >= 3) {
      const points = shape.points
        .filter((p) => Number.isFinite(Number(p.x)) && Number.isFinite(Number(p.y)))
        .map((p) => ({ x: Number(p.x), y: Number(p.y) }));
      if (points.length >= 3) return { type: "polygon", points };
    }
    if ([shape.x, shape.y, shape.w, shape.h].every((v) => typeof v === "number")) {
      return { type: "rectangle", x: shape.x, y: shape.y, w: shape.w, h: shape.h, rot: Number(shape.rot) || 0 };
    }
  } catch {
    return null;
  }
  return null;
}

function autoRectangle(index, total) {
  const cols = Math.min(4, Math.max(total, 1));
  const col = index % cols;
  const row = Math.floor(index / cols);
  const w = 90 / cols - 3;
  return { type: "rectangle", x: 5 + col * (90 / cols), y: 28 + row * 22, w, h: 16, rot: 0 };
}

function polygonBounds(points) {
  const xs = points.map((p) => p.x);
  const ys = points.map((p) => p.y);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  return { x: minX, y: minY, w: Math.max(maxX - minX, 1), h: Math.max(maxY - minY, 1) };
}

// Seed an L-shaped polygon from a rectangle so vertices can be reshaped freely.
function rectangleToPolygon(rect) {
  const cutX = rect.x + rect.w * 0.55;
  const cutY = rect.y + rect.h * 0.5;
  return {
    type: "polygon",
    points: [
      { x: rect.x, y: rect.y },
      { x: rect.x + rect.w, y: rect.y },
      { x: rect.x + rect.w, y: cutY },
      { x: cutX, y: cutY },
      { x: cutX, y: rect.y + rect.h },
      { x: rect.x, y: rect.y + rect.h },
    ],
  };
}

function polygonToRectangle(shape) {
  const b = polygonBounds(shape.points);
  return { type: "rectangle", ...b, rot: 0 };
}

function pointToSegmentDistance(p, a, b) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const lenSq = dx * dx + dy * dy;
  const t = lenSq === 0 ? 0 : clamp(((p.x - a.x) * dx + (p.y - a.y) * dy) / lenSq, 0, 1);
  const projX = a.x + t * dx;
  const projY = a.y + t * dy;
  return Math.hypot(p.x - projX, p.y - projY);
}

function nearestEdgeIndex(points, p) {
  let bestIdx = 0;
  let bestDist = Infinity;
  for (let i = 0; i < points.length; i += 1) {
    const a = points[i];
    const b = points[(i + 1) % points.length];
    const d = pointToSegmentDistance(p, a, b);
    if (d < bestDist) { bestDist = d; bestIdx = i; }
  }
  return bestIdx;
}

function horizontalSpansAtY(points, y) {
  const intersections = [];
  for (let i = 0; i < points.length; i += 1) {
    const a = points[i];
    const b = points[(i + 1) % points.length];
    if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y)) {
      intersections.push(a.x + ((y - a.y) * (b.x - a.x)) / (b.y - a.y));
    }
  }
  intersections.sort((a, b) => a - b);
  const spans = [];
  for (let i = 0; i + 1 < intersections.length; i += 2) {
    spans.push([intersections[i], intersections[i + 1]]);
  }
  return spans;
}

function pointAcrossSpans(spans, fraction) {
  const lengths = spans.map(([a, b]) => Math.max(b - a, 0));
  const total = lengths.reduce((sum, n) => sum + n, 0);
  if (total <= 0) return spans[0]?.[0] ?? 0;
  let target = clamp(fraction, 0, 1) * total;
  for (let i = 0; i < spans.length; i += 1) {
    if (target <= lengths[i]) return spans[i][0] + target;
    target -= lengths[i];
  }
  return spans[spans.length - 1][1];
}

// Use a shared column grid across the polygon bounding box so narrower rows align vertically with wider rows.
function seatPositionsForPoints(seats, points) {
  if (!seats.length || points.length < 3) return [];
  const rows = [...new Set(seats.map((s) => s.rowLabel))];
  const bounds = polygonBounds(points);
  const topPadding = Math.min(bounds.h * 0.22, 6);
  const bottomPadding = Math.min(bounds.h * 0.1, 3);
  const usableTop = bounds.y + topPadding;
  const usableHeight = Math.max(bounds.h - topPadding - bottomPadding, bounds.h * 0.5);

  const rowInfos = rows.map((rowLabel, rowIndex) => {
    const rowSeats = seats.filter((s) => s.rowLabel === rowLabel);
    const y = usableTop + ((rowIndex + 0.5) / rows.length) * usableHeight;
    let spans = horizontalSpansAtY(points, y);
    if (!spans.length) spans = [[bounds.x, bounds.x + bounds.w]];
    // Inset segment ends slightly so outer seats do not touch the polygon edge.
    spans = spans.map(([a, b]) => {
      const inset = Math.min(bounds.w * 0.025, 1.6, (b - a) / 2 - 0.05);
      return inset > 0 ? [a + inset, b - inset] : [a, b];
    });
    return { rowSeats, y, spans };
  });

  const maxCols = Math.max(...rowInfos.map((r) => r.rowSeats.length), 1);
  const pitch = bounds.w / maxCols;
  const colX = Array.from({ length: maxCols }, (_, c) => bounds.x + (c + 0.5) * pitch);
  const edgeTol = Math.max(pitch * 0.6, bounds.w * 0.02);

  const result = [];
  rowInfos.forEach(({ rowSeats, y, spans }) => {
    const n = rowSeats.length;
    if (!n) return;
    const [a, b] = spans.reduce((best, s) => (s[1] - s[0] > best[1] - best[0] ? s : best), spans[0]);
    const candidateCols = [];
    colX.forEach((x, c) => { if (x >= a - 1e-6 && x <= b + 1e-6) candidateCols.push(c); });

    if (candidateCols.length >= n) {
      const touchesLeft = a <= bounds.x + edgeTol;
      const touchesRight = b >= bounds.x + bounds.w - edgeTol;
      let chosen;
      if (touchesLeft && !touchesRight) chosen = candidateCols.slice(0, n);
      else if (touchesRight && !touchesLeft) chosen = candidateCols.slice(candidateCols.length - n);
      else {
        const start = Math.floor((candidateCols.length - n) / 2);
        chosen = candidateCols.slice(start, start + n);
      }
      rowSeats.forEach((seat, i) => result.push({ seat, x: colX[chosen[i]], y }));
    } else {
      // Safeguard for narrow spans: enforce a minimum pitch so seats never collapse/overlap
      const minPitch = Math.max(pitch * 0.7, 2.0);
      const spanW = b - a;
      const neededW = n * minPitch;
      const startX = spanW >= neededW ? a : (a + b) / 2 - neededW / 2;
      const effPitch = spanW >= neededW ? spanW / n : minPitch;
      rowSeats.forEach((seat, i) => {
        result.push({ seat, x: startX + (i + 0.5) * effPitch, y });
      });
    }
  });
  return result;
}

function seatPositionsForPolygon(zone, shape) {
  return seatPositionsForPoints(zone.seats || [], shape.points);
}

// Live freehand drawing overlay with rubber-band edge and real-time seat placement preview.
function DrawingOverlay({ points, cursor, stageWidth, stageHeight, color, seatPositions }) {
  const toPx = (p) => [(p.x / 100) * stageWidth, (p.y / 100) * stageHeight];
  const placedPx = points.flatMap(toPx);
  const previewPx = cursor ? [...placedPx, ...toPx(cursor)] : placedPx;
  const canClose = points.length >= 3;
  const firstPx = points.length ? toPx(points[0]) : null;
  const nearFirst = canClose && cursor && firstPx && Math.hypot(toPx(cursor)[0] - firstPx[0], toPx(cursor)[1] - firstPx[1]) < 12;

  return <>
    {points.length >= 2 && <Line points={placedPx} stroke="#fff" strokeWidth={2} lineJoin="round" listening={false} />}
    {cursor && points.length >= 1 && <Line points={[...toPx(points[points.length - 1]), ...toPx(cursor)]} stroke="rgba(255,255,255,.65)" strokeWidth={1.5} dash={[5, 4]} listening={false} />}
    {points.length >= 3 && <Line points={previewPx} closed fill={color} opacity={0.28} listening={false} />}
    {seatPositions.map(({ seat, x, y }) => (
      <Circle key={seat.seatId} x={(x / 100) * stageWidth} y={(y / 100) * stageHeight} radius={3.4} fill={color} stroke="#fff" strokeWidth={1} opacity={0.85} listening={false} />
    ))}
    {points.map((p, i) => {
      const [x, y] = toPx(p);
      const isFirst = i === 0;
      return <Circle key={i} x={x} y={y} radius={isFirst && canClose ? (nearFirst ? 8 : 6.5) : 5} fill={isFirst && canClose ? (nearFirst ? "#1eaa55" : "#fff") : "#fff"} stroke="#1e1b2e" strokeWidth={2} listening={false} />;
    })}
  </>;
}

export default function ZoneMapCanvas({
  zones,
  ticketTypes,
  readOnly,
  layoutJson,
  onZoneMove,
  onZoneShapeChange,
  onLayoutChange,
}) {
  const wrapperRef = useRef(null);
  const [stageSize, setStageSize] = useState({ width: 800, height: 500 });
  const [selectedId, setSelectedId] = useState(null);
  const [selectedSpecial, setSelectedSpecial] = useState(null); // 'stage' | null

  const defaultStage = { x: 30, y: 4, w: 40, h: 14, rot: 0 };

  const parsedLayout = useMemo(() => {
    if (!layoutJson) return null;
    if (typeof layoutJson === "object") return layoutJson;
    try {
      return JSON.parse(layoutJson);
    } catch {
      return null;
    }
  }, [layoutJson]);

  const [stageShape, setStageShape] = useState(() => {
    if (parsedLayout?.stageShape) return parsedLayout.stageShape;
    try {
      const saved = localStorage.getItem("concertshield_venue_stage");
      return saved ? JSON.parse(saved) : defaultStage;
    } catch {
      return defaultStage;
    }
  });

  // Sync state whenever parsedLayout updates (e.g. API fetch / template switch)
  useEffect(() => {
    if (parsedLayout?.stageShape) {
      setStageShape(parsedLayout.stageShape);
    }
  }, [parsedLayout]);

  const [liveShapes, setLiveShapes] = useState({});
  const [stageWarning, setStageWarning] = useState("");
  const warningTimerRef = useRef(null);

  const showWarning = (msg) => {
    setStageWarning(msg);
    if (warningTimerRef.current) clearTimeout(warningTimerRef.current);
    warningTimerRef.current = setTimeout(() => setStageWarning(""), 3500);
  };

  const handleStageChange = (next) => {
    const overlapsAnyZone = shapes.some(({ shape }) => doesZoneOverlapStage(shape, next));
    if (overlapsAnyZone) {
      showWarning("STAGE cannot overlap existing ticket zones.");
      return false;
    }
    setStageShape(next);
    try {
      localStorage.setItem("concertshield_venue_stage", JSON.stringify(next));
    } catch { }
    if (onLayoutChange) {
      const curr = parsedLayout || {};
      onLayoutChange({
        ...curr,
        stageShape: next,
      });
    }
    return true;
  };
  const [draw, setDraw] = useState(null);
  const [cursorPos, setCursorPos] = useState(null);

  useEffect(() => {
    const el = wrapperRef.current;
    if (!el) return undefined;
    const measure = () => {
      const rect = el.getBoundingClientRect();
      if (rect.width > 0 && rect.height > 0) setStageSize({ width: rect.width, height: rect.height });
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const ticketTypeById = (id) => ticketTypes.find((t) => t.ticketTypeId === id);
  const shapeFor = (zone, index) => {
    const raw = liveShapes[zone.seatZoneId] || parseZoneShape(zone.shapeJson) || autoRectangle(index, zones.length);
    // Seated zones are strictly rectangular blocks so seat rows and columns never deform
    if (zone.zoneType === "Seated" && raw.type === "polygon") {
      return polygonToRectangle(raw);
    }
    return raw;
  };
  const shapes = useMemo(() => zones.map((zone, index) => ({ zone, shape: shapeFor(zone, index) })), [zones, liveShapes]);
  const selected = shapes.find(({ zone }) => zone.seatZoneId === selectedId);

  const commitShape = (seatZoneId, rawShape) => {
    const shape = clampZoneInsideCanvas(rawShape);
    if (doesZoneOverlapStage(shape, stageShape)) {
      showWarning("Ticket zones cannot overlap with STAGE.");
      return false;
    }
    setLiveShapes((prev) => ({ ...prev, [seatZoneId]: shape }));
    if (onZoneShapeChange) onZoneShapeChange(seatZoneId, shape);
    else if (onZoneMove) onZoneMove(seatZoneId, shape);
    return true;
  };

  const convertSelected = (targetType) => {
    if (!selected || readOnly || selected.zone.zoneType === "Seated") return;
    // Converting polygon -> rectangle discards custom vertices and cannot be undone.
    if (targetType === "rectangle" && selected.shape.type === "polygon") {
      if (!window.confirm("Converting back to a rectangle will discard the custom polygon points you've dragged. Continue?")) return;
    }
    const next = targetType === "polygon"
      ? (selected.shape.type === "polygon" ? selected.shape : rectangleToPolygon(selected.shape))
      : (selected.shape.type === "rectangle" ? selected.shape : polygonToRectangle(selected.shape));
    commitShape(selected.zone.seatZoneId, next);
  };

  const startDrawing = () => {
    if (!selected || readOnly || selected.zone.zoneType === "Seated") return;
    if (selected.shape.type === "polygon" && !window.confirm("Drawing a new shape will replace this zone's current custom polygon. Continue?")) return;
    setDraw({ seatZoneId: selected.zone.seatZoneId, points: [] });
    setCursorPos(null);
  };

  const finishDrawing = () => {
    if (!draw || draw.points.length < 3) return;
    const ok = commitShape(draw.seatZoneId, { type: "polygon", points: draw.points });
    if (ok === false) return;
    setDraw(null);
    setCursorPos(null);
  };

  const cancelDrawing = () => {
    setDraw(null);
    setCursorPos(null);
  };

  useEffect(() => {
    if (!draw) return undefined;
    const onKeyDown = (e) => {
      if (e.key === "Enter") finishDrawing();
      else if (e.key === "Escape") cancelDrawing();
      else if (e.key === "Backspace" || e.key === "Delete") {
        setDraw((prev) => (prev && prev.points.length ? { ...prev, points: prev.points.slice(0, -1) } : prev));
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draw]);

  const { width: stageWidth, height: stageHeight } = stageSize;

  const stagePointToPercent = (stage) => {
    const pos = stage.getPointerPosition();
    if (!pos) return null;
    return { x: clamp((pos.x / stageWidth) * 100, 0, 100), y: clamp((pos.y / stageHeight) * 100, 0, 100) };
  };

  const handleStageMouseMove = (e) => {
    if (!draw) return;
    setCursorPos(stagePointToPercent(e.target.getStage()));
  };

  const handleStageClick = (e) => {
    if (!draw) {
      if (e.target === e.target.getStage()) {
        setSelectedId(null);
        setSelectedSpecial(null);
      }
      return;
    }
    const pt = stagePointToPercent(e.target.getStage());
    if (!pt) return;
    if (draw.points.length >= 3) {
      const first = draw.points[0];
      const firstPx = { x: (first.x / 100) * stageWidth, y: (first.y / 100) * stageHeight };
      const curPx = { x: (pt.x / 100) * stageWidth, y: (pt.y / 100) * stageHeight };
      if (Math.hypot(firstPx.x - curPx.x, firstPx.y - curPx.y) < 12) {
        finishDrawing();
        return;
      }
    }
    setDraw((prev) => (prev ? { ...prev, points: [...prev.points, pt] } : prev));
  };

  const drawingZone = draw ? zones.find((z) => z.seatZoneId === draw.seatZoneId) : null;
  const drawingColor = drawingZone ? colorForTicketType(drawingZone.ticketTypeId) : "#fff";
  const drawSeatPositions = draw
    ? seatPositionsForPoints(drawingZone?.seats || [], draw.points)
    : [];

  return (
    <div className="ow-zonemap-wrap">
      <div className="ow-zonemap-scale-bar">
        <span className="ow-scale-badge">Scale convention:</span>
        <span className="ow-scale-desc">
          1 px = 0.1 m · venue 160 m × 100 m · positions are saved as %
        </span>
        {!readOnly && (
          <div style={{ marginLeft: "auto", display: "flex", gap: "8px", alignItems: "center" }}>
            <button
              type="button"
              className={"ow-shape-pill" + (selectedSpecial === "stage" ? " active" : "")}
              onClick={() => {
                setSelectedId(null);
                setSelectedSpecial((curr) => (curr === "stage" ? null : "stage"));
              }}
              title="Select STAGE to drag or resize"
            >
              STAGE
            </button>
            <button
              type="button"
              className="ow-shape-pill"
              onClick={() => {
                handleStageChange(defaultStage);
              }}
              title="Reset STAGE to default position"
            >
              Reset
            </button>
          </div>
        )}
      </div>

      {!readOnly && selectedSpecial === "stage" && (
        <div className="ow-zonemap-shape-tools" style={{ background: "#faf5ff", borderColor: "#d8b4fe", flexDirection: "column", alignItems: "stretch", gap: "8px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap", width: "100%" }}>
            <span className="ow-shape-label" style={{ color: "#7e22ce" }}>Stage Style:</span>
            <button
              type="button"
              className={"ow-shape-pill" + (!stageShape.stageType || stageShape.stageType === "standard" ? " active" : "")}
              onClick={() => handleStageChange({ ...stageShape, stageType: "standard" })}
            >
              Standard Box
            </button>
            <button
              type="button"
              className={"ow-shape-pill" + (stageShape.stageType === "t-stage" ? " active" : "")}
              onClick={() => handleStageChange({ ...stageShape, stageType: "t-stage", h: Math.max(stageShape.h, 24) })}
            >
              T-Stage (Runway + Catwalk)
            </button>
            <button
              type="button"
              className={"ow-shape-pill" + (stageShape.stageType === "wings" ? " active" : "")}
              onClick={() => handleStageChange({ ...stageShape, stageType: "wings", h: Math.max(stageShape.h, 20) })}
            >
              U-Stage (Extended Wings)
            </button>
            <button
              type="button"
              className={"ow-shape-pill" + (stageShape.stageType === "center" ? " active" : "")}
              onClick={() => handleStageChange({ ...stageShape, stageType: "center" })}
            >
              360° Arena Center
            </button>
            <button
              type="button"
              className="ow-shape-pill"
              style={{ marginLeft: "auto" }}
              onClick={() => handleStageChange(defaultStage)}
            >
              Center Stage
            </button>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: "10px", width: "100%", fontSize: "12px" }}>
            <span className="ow-hint" style={{ color: "#581c87" }}>
              Drag to move anywhere. Drag handles to resize runway depth or stage width. Non-ticketing landmark.
            </span>
          </div>
        </div>
      )}

      {!readOnly && selected && !draw && (
        <div className="ow-zonemap-shape-tools">
          <span className="ow-shape-label">Zone shape:</span>
          {selected.zone.zoneType === "Seated" ? (
            <span className="ow-hint" style={{ color: "#0369a1", fontWeight: 500 }}>
              Seated zone: Fixed rectangular block (drag to position, use corners to resize or rotate).
            </span>
          ) : (
            <div className="ow-shape-button-group">
              <button
                type="button"
                className={"ow-shape-pill" + (selected.shape.type === "rectangle" ? " active" : "")}
                onClick={() => convertSelected("rectangle")}
              >
                Rectangle
              </button>
              <button
                type="button"
                className={"ow-shape-pill" + (selected.shape.type === "polygon" ? " active" : "")}
                onClick={startDrawing}
              >
                Draw custom shape
              </button>
              {selected.shape.type === "polygon" && (
                <span className="ow-hint">Drag a point to move it • Double-click an edge to add a point • Right-click a point to remove it</span>
              )}
            </div>
          )}
        </div>
      )}

      {draw && (
        <div className="ow-zonemap-shape-tools ow-zonemap-drawing-bar">
          <span className="ow-hint">Drawing mode — click to place points, click the first point (or press Enter) to close the shape.</span>
          <button type="button" className="ow-shape-pill" disabled={draw.points.length < 3} onClick={finishDrawing}>Done</button>
          <button type="button" className="ow-shape-pill" onClick={cancelDrawing}>Cancel</button>
        </div>
      )}

      {stageWarning && (
        <div style={{ background: "#fef2f2", border: "1px solid #fecaca", color: "#b91c1c", borderRadius: "8px", padding: "8px 12px", fontSize: "12.5px", fontWeight: 600, marginBottom: "8px" }}>
          {stageWarning}
        </div>
      )}

      <div className="ow-zonemap">
        <div className="ow-zonemap-canvas" ref={wrapperRef}>
          {stageWidth > 0 && stageHeight > 0 && (
            <Stage
              width={stageWidth} height={stageHeight}
              onMouseMove={handleStageMouseMove}
              onClick={handleStageClick}
              onTap={handleStageClick}
              style={{ cursor: draw ? "crosshair" : "default" }}
            >
              <Layer>
                <StageFloor width={stageWidth} height={stageHeight} />

                <StageItem
                  shape={stageShape}
                  stageWidth={stageWidth}
                  stageHeight={stageHeight}
                  readOnly={readOnly || !!draw}
                  selected={selectedSpecial === "stage"}
                  onSelect={() => {
                    setSelectedId(null);
                    setSelectedSpecial("stage");
                  }}
                  onChange={handleStageChange}
                />

                {shapes.filter(({ zone }) => !(draw && draw.seatZoneId === zone.seatZoneId)).map(({ zone, shape }) => shape.type === "polygon" ? (
                  <PolygonZoneShape
                    key={zone.seatZoneId}
                    zone={zone}
                    shape={shape}
                    stageWidth={stageWidth}
                    stageHeight={stageHeight}
                    color={colorForTicketType(zone.ticketTypeId)}
                    ticketType={ticketTypeById(zone.ticketTypeId)}
                    readOnly={readOnly || !!draw}
                    selected={selectedId === zone.seatZoneId}
                    onSelect={() => {
                      setSelectedId(zone.seatZoneId);
                      setSelectedSpecial(null);
                    }}
                    onChange={(next) => commitShape(zone.seatZoneId, next)}
                  />
                ) : (
                  <RectangleZoneShape
                    key={zone.seatZoneId}
                    zone={zone}
                    shape={shape}
                    stageWidth={stageWidth}
                    stageHeight={stageHeight}
                    color={colorForTicketType(zone.ticketTypeId)}
                    ticketType={ticketTypeById(zone.ticketTypeId)}
                    readOnly={readOnly || !!draw}
                    selected={selectedId === zone.seatZoneId}
                    onSelect={() => {
                      setSelectedId(zone.seatZoneId);
                      setSelectedSpecial(null);
                    }}
                    onChange={(next) => commitShape(zone.seatZoneId, next)}
                  />
                ))}

                {draw && <DrawingOverlay points={draw.points} cursor={cursorPos} stageWidth={stageWidth} stageHeight={stageHeight} color={drawingColor} seatPositions={drawSeatPositions} />}
              </Layer>
            </Stage>
          )}
          {zones.length === 0 && <div className="ow-zonemap-empty">Add a zone below, then select it here to draw its shape freely on the map.</div>}
        </div>

        <div className="ow-zonemap-legend">
          <div className="ow-zonemap-legend-title">Ticket prices</div>
          {zones.length === 0 && <div className="ow-hint">No zones yet.</div>}
          {zones.map((zone) => {
            const tt = ticketTypeById(zone.ticketTypeId);
            return <div key={zone.seatZoneId} className="ow-zonemap-legend-row"><span className="ow-zonemap-legend-swatch" style={{ background: colorForTicketType(zone.ticketTypeId) }} /><span className="ow-zonemap-legend-name">{zone.zoneName} <span className="ow-hint">({zone.zoneType})</span></span><span className="ow-zonemap-legend-price">{tt ? formatPrice(tt.price) : "—"}</span></div>;
          })}
        </div>
      </div>
    </div>
  );
}

function renderPhysicalSeats(zone, width, height, color) {
  if (zone.zoneType !== "Seated" || !zone.seats?.length) return null;
  const maxX = Math.max(...zone.seats.map((s) => Number(s.xCoordinate) || 1), 1);
  const maxY = Math.max(...zone.seats.map((s) => Number(s.yCoordinate) || 1), 1);
  const headerHeight = Math.min(26, Math.max(16, height * 0.22));
  const padX = Math.min(14, Math.max(6, width * 0.06));
  const padBottom = Math.min(10, Math.max(5, height * 0.05));
  const stepX = Math.max(1, width - padX * 2) / maxX;
  const stepY = Math.max(1, height - headerHeight - padBottom) / maxY;
  const radius = clamp(Math.min(stepX, stepY) * 0.3, 2.4, 7);
  return zone.seats.map((seat) => {
    const x = padX + ((Number(seat.xCoordinate) || 1) - 0.5) * stepX;
    const y = headerHeight + ((Number(seat.yCoordinate) || 1) - 0.5) * stepY;
    return <Circle key={seat.seatId} x={x} y={y} radius={radius} fill={color} stroke="#fff" strokeWidth={Math.max(1, radius * .3)} shadowColor="#000" shadowBlur={radius * 0.6} shadowOpacity={0.45} listening={false} />;
  });
}

function zoneCapacityText(zone) {
  const capacity = Number(zone.capacity ?? zone.totalSeats ?? zone.seats?.length ?? 0);
  return zone.zoneType === "Standing" ? `${capacity} standing` : `${capacity} seats`;
}

function zoneDimensionsText(bounds, stageWidth, stageHeight) {
  // Venue convention: canvas represents 160 m x 100 m. Geometry is stored in percent.
  const widthM = (Number(bounds.w || 0) / 100) * 160;
  const heightM = (Number(bounds.h || 0) / 100) * 100;
  const fmt = (v) => Number.isInteger(v) ? String(v) : v.toFixed(1);
  return `${fmt(widthM)} m × ${fmt(heightM)} m`;
}

function RectangleZoneShape({ zone, shape, stageWidth, stageHeight, color, ticketType, readOnly, selected, onSelect, onChange }) {
  const groupRef = useRef(null);
  const trRef = useRef(null);
  const [hovered, setHovered] = useState(false);
  useEffect(() => {
    if (selected && !readOnly && trRef.current && groupRef.current) {
      trRef.current.nodes([groupRef.current]);
      trRef.current.getLayer().batchDraw();
    }
  }, [selected, readOnly]);

  const px = (shape.x / 100) * stageWidth;
  const py = (shape.y / 100) * stageHeight;
  const pw = (shape.w / 100) * stageWidth;
  const ph = (shape.h / 100) * stageHeight;
  // Show only zone name and metrics here; ticket tier and price live in the adjacent Ticket prices table.
  const label = `${zone.zoneName}\n${zoneCapacityText(zone)}\n${zoneDimensionsText(shape, stageWidth, stageHeight)}`;
  const labelH = Math.min(ph, Math.max(42, ph * .58));
  const labelFontSize = clamp(Math.min(pw, ph) * .12, 9, 13);
  const cardFill = hexToRgba(color, 0.32);

  return <>
    <Group ref={groupRef} x={px} y={py} rotation={shape.rot || 0} draggable={!readOnly} opacity={hovered && !selected ? 0.92 : 1}
      dragBoundFunc={(pos) => ({
        x: clamp(pos.x, 0, Math.max(0, stageWidth - pw)),
        y: clamp(pos.y, 0, Math.max(0, stageHeight - ph)),
      })}
      onClick={onSelect} onTap={onSelect}
      onMouseEnter={(e) => { setHovered(true); if (!readOnly) e.target.getStage().container().style.cursor = "grab"; }}
      onMouseLeave={(e) => { setHovered(false); e.target.getStage().container().style.cursor = "default"; }}
      onDragStart={(e) => { e.target.getStage().container().style.cursor = "grabbing"; }}
      onDragEnd={(e) => {
        e.target.getStage().container().style.cursor = "grab";
        const next = {
          ...shape,
          x: clamp((e.target.x() / stageWidth) * 100, 0, Math.max(0, 100 - shape.w)),
          y: clamp((e.target.y() / stageHeight) * 100, 0, Math.max(0, 100 - shape.h)),
        };
        const ok = onChange(next);
        if (ok === false) e.target.position({ x: px, y: py });
      }}
      onTransformEnd={() => {
        const node = groupRef.current;
        const nextW = Math.max(20, pw * node.scaleX());
        const nextH = Math.max(20, ph * node.scaleY());
        node.scaleX(1); node.scaleY(1);
        const wPct = clamp((nextW / stageWidth) * 100, MIN_W_PCT, 100);
        const hPct = clamp((nextH / stageHeight) * 100, MIN_H_PCT, 100);
        const next = {
          type: "rectangle",
          x: clamp((node.x() / stageWidth) * 100, 0, Math.max(0, 100 - wPct)),
          y: clamp((node.y() / stageHeight) * 100, 0, Math.max(0, 100 - hPct)),
          w: wPct,
          h: hPct,
          rot: node.rotation(),
        };
        const ok = onChange(next);
        if (ok === false) node.position({ x: px, y: py });
      }}>
      <Rect
        width={pw} height={ph} fill={cardFill}
        stroke={selected ? "#fff" : hovered ? "rgba(255,255,255,.7)" : color}
        strokeWidth={selected ? 2.5 : hovered ? 1.8 : 1.4}
        dash={zone.zoneType === "Standing" ? [7, 5] : undefined}
        cornerRadius={8}
        shadowColor="#000" shadowBlur={selected ? 16 : 8} shadowOpacity={selected ? 0.45 : 0.28} shadowOffset={{ x: 0, y: 3 }}
      />
      <Text text={label} width={pw} height={ph} align="center" verticalAlign="middle" fontSize={labelFontSize} fontStyle="bold" fill="#ffffff" listening={false} padding={8} shadowColor="#000" shadowBlur={3} shadowOpacity={0.8} />
      {renderPhysicalSeats(zone, pw, ph, color)}
    </Group>
    {selected && !readOnly && <Transformer ref={trRef} rotateEnabled keepRatio={false} borderStroke="#fff" borderStrokeWidth={1.5} anchorStroke="#fff" anchorFill="#1e1b2e" anchorCornerRadius={4} anchorSize={9} boundBoxFunc={(oldBox, newBox) => newBox.width < 20 || newBox.height < 20 ? oldBox : newBox} />}
  </>;
}

function PolygonZoneShape({ zone, shape, stageWidth, stageHeight, color, ticketType, readOnly, selected, onSelect, onChange }) {
  const [hovered, setHovered] = useState(false);
  const [hoverVertex, setHoverVertex] = useState(null);
  const pointsPx = shape.points.flatMap((p) => [(p.x / 100) * stageWidth, (p.y / 100) * stageHeight]);
  const bounds = polygonBounds(shape.points);
  const label = `${zone.zoneName}\n${zoneCapacityText(zone)}\n${zoneDimensionsText(bounds, stageWidth, stageHeight)}`;
  const seatPositions = seatPositionsForPolygon(zone, shape);
  const textColor = readableTextColor(color);
  const cardFill = hexToRgba(color, 0.32);
  const labelWpx = (bounds.w / 100) * stageWidth;
  const labelXpx = (bounds.x / 100) * stageWidth;
  const labelYpx = (bounds.y / 100) * stageHeight;

  const onVertexDragEnd = (index, e) => {
    const nextPoints = shape.points.map((p, i) => i === index ? { x: clamp((e.target.x() / stageWidth) * 100, 0, 100), y: clamp((e.target.y() / stageHeight) * 100, 0, 100) } : p);
    const ok = onChange({ type: "polygon", points: nextPoints });
    if (ok === false) {
      const orig = shape.points[index];
      e.target.position({ x: (orig.x / 100) * stageWidth, y: (orig.y / 100) * stageHeight });
    }
  };

  const movePolygon = (e) => {
    const rawDx = (e.target.x() / stageWidth) * 100;
    const rawDy = (e.target.y() / stageHeight) * 100;
    e.target.position({ x: 0, y: 0 });
    const dx = clamp(rawDx, -bounds.x, 100 - (bounds.x + bounds.w));
    const dy = clamp(rawDy, -bounds.y, 100 - (bounds.y + bounds.h));
    const moved = shape.points.map((p) => ({ x: clamp(p.x + dx, 0, 100), y: clamp(p.y + dy, 0, 100) }));
    onChange({ type: "polygon", points: moved });
  };

  const minPxX = (bounds.x / 100) * stageWidth;
  const maxPxX = ((bounds.x + bounds.w) / 100) * stageWidth;
  const minPxY = (bounds.y / 100) * stageHeight;
  const maxPxY = ((bounds.y + bounds.h) / 100) * stageHeight;

  return <>
    <Group draggable={!readOnly} opacity={hovered && !selected ? 0.92 : 1}
      dragBoundFunc={(pos) => ({
        x: clamp(pos.x, -minPxX, Math.max(-minPxX, stageWidth - maxPxX)),
        y: clamp(pos.y, -minPxY, Math.max(-minPxY, stageHeight - maxPxY)),
      })}
      onDragStart={(e) => { e.target.getStage().container().style.cursor = "grabbing"; }}
      onDragEnd={(e) => { e.target.getStage().container().style.cursor = "grab"; movePolygon(e); }}
      onClick={onSelect} onTap={onSelect}
      onMouseEnter={(e) => { setHovered(true); if (!readOnly) e.target.getStage().container().style.cursor = "grab"; }}
      onMouseLeave={(e) => { setHovered(false); e.target.getStage().container().style.cursor = "default"; }}>
      <Line
        points={pointsPx} closed fill={cardFill}
        stroke={selected ? "#fff" : hovered ? "rgba(255,255,255,.7)" : color}
        strokeWidth={selected ? 2.5 : hovered ? 1.8 : 1.4}
        lineJoin="round" dash={zone.zoneType === "Standing" ? [7, 5] : undefined}
        shadowColor="#000" shadowBlur={selected ? 16 : 8} shadowOpacity={selected ? 0.45 : 0.28} shadowOffset={{ x: 0, y: 3 }}
        onDblClick={(e) => {
          if (readOnly || !selected) return;
          e.cancelBubble = true;
          const stage = e.target.getStage();
          const pos = stage.getPointerPosition();
          const pt = { x: clamp((pos.x / stageWidth) * 100, 0, 100), y: clamp((pos.y / stageHeight) * 100, 0, 100) };
          const idx = nearestEdgeIndex(shape.points, pt);
          const nextPoints = [...shape.points.slice(0, idx + 1), pt, ...shape.points.slice(idx + 1)];
          onChange({ type: "polygon", points: nextPoints });
        }}
        onDblTap={(e) => {
          if (readOnly || !selected) return;
          e.cancelBubble = true;
          const stage = e.target.getStage();
          const pos = stage.getPointerPosition();
          if (!pos) return;
          const pt = { x: clamp((pos.x / stageWidth) * 100, 0, 100), y: clamp((pos.y / stageHeight) * 100, 0, 100) };
          const idx = nearestEdgeIndex(shape.points, pt);
          const nextPoints = [...shape.points.slice(0, idx + 1), pt, ...shape.points.slice(idx + 1)];
          onChange({ type: "polygon", points: nextPoints });
        }}
      />
      <Text text={label} x={labelXpx} y={labelYpx} width={labelWpx} height={(bounds.h / 100) * stageHeight} align="center" verticalAlign="middle" fontSize={11} fontStyle="bold" fill="#ffffff" listening={false} padding={8} shadowColor="#000" shadowBlur={3} shadowOpacity={0.8} />
      {zone.zoneType === "Seated" && seatPositions.map(({ seat, x, y }) => <Circle key={seat.seatId} x={(x / 100) * stageWidth} y={(y / 100) * stageHeight} radius={4} fill={color} stroke="#fff" strokeWidth={1.2} shadowColor="#000" shadowBlur={2.5} shadowOpacity={0.4} listening={false} />)}
    </Group>
    {selected && !readOnly && shape.points.map((p, index) => (
      <Circle
        key={`vertex-${index}`}
        x={(p.x / 100) * stageWidth} y={(p.y / 100) * stageHeight}
        radius={hoverVertex === index ? 7.5 : 6}
        fill="#fff" stroke="#1e1b2e" strokeWidth={2}
        shadowColor="#000" shadowBlur={6} shadowOpacity={0.4}
        draggable
        onDragStart={(e) => { e.target.getStage().container().style.cursor = "grabbing"; }}
        onDragEnd={(e) => { e.target.getStage().container().style.cursor = "pointer"; onVertexDragEnd(index, e); }}
        onMouseEnter={(e) => { setHoverVertex(index); e.target.getStage().container().style.cursor = "pointer"; }}
        onMouseLeave={(e) => { setHoverVertex(null); e.target.getStage().container().style.cursor = "default"; }}
        onContextMenu={(e) => {
          e.evt.preventDefault();
          if (readOnly || shape.points.length <= 3) return;
          const nextPoints = shape.points.filter((_, i) => i !== index);
          onChange({ type: "polygon", points: nextPoints });
        }}
      />
    ))}
  </>;
}
