import { useEffect, useMemo, useRef, useState } from "react";
import { Stage, Layer, Rect, Text, Transformer, Group, Circle, Line } from "react-konva";
import { formatPrice } from "../../../utils/format";

const PALETTE = ["#f59e0b", "#14b8a6", "#a855f7", "#3b82f6", "#ef4444", "#22c55e", "#ec4899", "#84cc16", "#06b6d4", "#f97316"];
const clamp = (v, min, max) => Math.min(Math.max(v, min), max);
const MIN_W_PCT = 6;
const MIN_H_PCT = 6;

function colorForTicketType(ticketTypeId) {
  return PALETTE[(Number(ticketTypeId) || 0) % PALETTE.length];
}

// Pha màu ticket-type thành nền trong suốt kiểu "card kính" thay vì tô đặc cả khối —
// giống card xám mờ của trang tham khảo, đỡ chói và làm ghế bên trong dễ đọc hơn.
function hexToRgba(hex, alpha) {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

// Chọn màu chữ đen/trắng theo độ sáng của màu nền zone để label luôn đọc được rõ.
function readableTextColor(hex) {
  const h = hex.replace("#", "");
  const r = parseInt(h.substring(0, 2), 16);
  const g = parseInt(h.substring(2, 4), 16);
  const b = parseInt(h.substring(4, 6), 16);
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance > 0.6 ? "#14151a" : "#f8fafc";
}

// Vẽ nền "sàn sân khấu" mờ + lưới nhẹ phía sau các zone, để canvas không bị trống trải.
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

// Đọc cả JSON rectangle cũ và polygon mới để các sơ đồ đã lưu trước đây vẫn dùng được.
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

// Tạo polygon chữ L từ rectangle hiện tại; organizer có thể kéo từng đỉnh để thành hình bất kỳ.
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

// Khoảng cách từ 1 điểm tới đoạn thẳng a-b (dùng % toạ độ) — để tìm cạnh gần nhất khi double-click thêm đỉnh.
function pointToSegmentDistance(p, a, b) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const lenSq = dx * dx + dy * dy;
  const t = lenSq === 0 ? 0 : clamp(((p.x - a.x) * dx + (p.y - a.y) * dy) / lenSq, 0, 1);
  const projX = a.x + t * dx;
  const projY = a.y + t * dy;
  return Math.hypot(p.x - projX, p.y - projY);
}

// Trả về index cạnh (điểm[i] -> điểm[i+1]) gần điểm p nhất; đỉnh mới sẽ được chèn ngay sau i.
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

// Trả về các đoạn nằm bên trong polygon tại một cao độ Y; dùng để đặt đủ ghế vào vùng chữ L/đa giác.
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

// Với polygon, vị trí hiển thị được tính theo từng hàng và luôn nằm trong biên zone.
// Nhận thẳng mảng points (không phải shape) để dùng lại được cả lúc vẽ tự do
// (điểm chưa đóng hình / chưa lưu) lẫn lúc hiển thị zone đã lưu.
//
// QUAN TRỌNG: cách cũ tính span (khoảng trống) riêng cho từng hàng tại độ cao y
// của hàng đó, rồi trải/căn giữa ghế trong đúng span ấy — mỗi hàng "trôi nổi"
// độc lập, không hàng nào neo vào cùng 1 mốc toạ độ, nên với zone lệch/chữ L,
// 2 hàng nhìn như 2 khối tách rời (giống bản đối chiếu người dùng gửi).
// Nền tảng vé chuyên nghiệp (ảnh mẫu) làm khác: dựng 1 LƯỚI CỘT CỐ ĐỊNH cho cả
// zone (dựa theo hàng rộng nhất), mọi hàng dùng chung các mốc cột x đó — hàng
// hẹp hơn thì chỉ chiếm 1 phần lưới và ép sát về phía cạnh mà nó thực sự chạm
// tới, chứ không giãn/co riêng. Nhờ vậy ghế các hàng luôn thẳng cột với nhau.
function seatPositionsForPoints(seats, points) {
  if (!seats.length || points.length < 3) return [];
  const rows = [...new Set(seats.map((s) => s.rowLabel))];
  const bounds = polygonBounds(points);
  const topPadding = Math.min(bounds.h * 0.22, 6);
  const bottomPadding = Math.min(bounds.h * 0.1, 3);
  const usableTop = bounds.y + topPadding;
  const usableHeight = Math.max(bounds.h - topPadding - bottomPadding, bounds.h * 0.5);

  // Bước 1: với mỗi hàng, xác định y và (các) đoạn nằm trong polygon tại y đó.
  const rowInfos = rows.map((rowLabel, rowIndex) => {
    const rowSeats = seats.filter((s) => s.rowLabel === rowLabel);
    const y = usableTop + ((rowIndex + 0.5) / rows.length) * usableHeight;
    let spans = horizontalSpansAtY(points, y);
    if (!spans.length) spans = [[bounds.x, bounds.x + bounds.w]];
    // Thu nhẹ 2 đầu mỗi đoạn vào trong, để ghế ngoài cùng không dính sát viền hình.
    spans = spans.map(([a, b]) => {
      const inset = Math.min(bounds.w * 0.025, 1.6, (b - a) / 2 - 0.05);
      return inset > 0 ? [a + inset, b - inset] : [a, b];
    });
    return { rowSeats, y, spans };
  });

  // Bước 2: lưới cột chung — số cột = số ghế của hàng đông nhất, trải đều trên
  // TOÀN BỘ bề rộng bounding box. Hàng rộng nhất sẽ dùng hết mọi cột (khớp mép
  // trái/phải), các hàng khác chỉ dùng một tập con của đúng các cột này.
  const maxCols = Math.max(...rowInfos.map((r) => r.rowSeats.length), 1);
  const pitch = bounds.w / maxCols;
  const colX = Array.from({ length: maxCols }, (_, c) => bounds.x + (c + 0.5) * pitch);
  const edgeTol = Math.max(pitch * 0.6, bounds.w * 0.02);

  const result = [];
  rowInfos.forEach(({ rowSeats, y, spans }) => {
    const n = rowSeats.length;
    if (!n) return;
    // Lấy đoạn rộng nhất tại hàng này để chọn cột (trường hợp nhiều đoạn rời
    // tại cùng độ cao là hiếm, ví dụ polygon có eo thắt).
    const [a, b] = spans.reduce((best, s) => (s[1] - s[0] > best[1] - best[0] ? s : best), spans[0]);
    const candidateCols = [];
    colX.forEach((x, c) => { if (x >= a - 1e-6 && x <= b + 1e-6) candidateCols.push(c); });

    if (candidateCols.length >= n) {
      const touchesLeft = a <= bounds.x + edgeTol;
      const touchesRight = b >= bounds.x + bounds.w - edgeTol;
      let chosen;
      if (touchesLeft && !touchesRight) chosen = candidateCols.slice(0, n); // ép sát trái
      else if (touchesRight && !touchesLeft) chosen = candidateCols.slice(candidateCols.length - n); // ép sát phải
      else {
        const start = Math.floor((candidateCols.length - n) / 2); // chạm cả 2 cạnh (hoặc không chạm cạnh nào) → căn giữa
        chosen = candidateCols.slice(start, start + n);
      }
      rowSeats.forEach((seat, i) => result.push({ seat, x: colX[chosen[i]], y }));
    } else {
      // Đoạn quá hẹp so với lưới chung (hình vẽ quá thắt lại) — không đủ cột để
      // xếp thẳng hàng, đành trải đều riêng trong đoạn này để không tràn ra ngoài.
      rowSeats.forEach((seat, i) => {
        const fraction = (i + 0.5) / n;
        result.push({ seat, x: a + fraction * (b - a), y });
      });
    }
  });
  return result;
}

function seatPositionsForPolygon(zone, shape) {
  return seatPositionsForPoints(zone.seats || [], shape.points);
}

// Overlay hiển thị khi organizer đang vẽ tự do: các điểm đã đặt, đoạn nối,
// đường "rubber band" chạy theo con trỏ, và bây giờ thêm cả ghế xem trước —
// ghế của chính zone đang vẽ được xếp lại ngay theo hình đang vẽ dở, để tự
// canh hình cho vừa mắt trước khi bấm "Done" thay vì phải lưu xong mới thấy.
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
      return <Circle key={i} x={x} y={y} radius={isFirst && canClose ? (nearFirst ? 8 : 6.5) : 5} fill={isFirst && canClose ? (nearFirst ? "#22c55e" : "#fff") : "#fff"} stroke="#111827" strokeWidth={2} listening={false} />;
    })}
  </>;
}

export default function ZoneMapCanvas({ zones, ticketTypes, readOnly, onZoneMove, onZoneShapeChange }) {
  const wrapperRef = useRef(null);
  const [stageSize, setStageSize] = useState({ width: 800, height: 500 });
  const [selectedId, setSelectedId] = useState(null);
  const [liveShapes, setLiveShapes] = useState({});
  // Chế độ vẽ tự do: draw = { seatZoneId, points: [{x,y}] } theo %; cursorPos = vị trí
  // chuột hiện tại (theo %) để vẽ đường "rubber band" xem trước cạnh sắp thêm.
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
  const shapeFor = (zone, index) => liveShapes[zone.seatZoneId] || parseZoneShape(zone.shapeJson) || autoRectangle(index, zones.length);
  const shapes = useMemo(() => zones.map((zone, index) => ({ zone, shape: shapeFor(zone, index) })), [zones, liveShapes]);
  const selected = shapes.find(({ zone }) => zone.seatZoneId === selectedId);

  // Lưu geometry mới theo một contract duy nhất; parent quyết định gọi API update zone.
  const commitShape = (seatZoneId, shape) => {
    setLiveShapes((prev) => ({ ...prev, [seatZoneId]: shape }));
    if (onZoneShapeChange) onZoneShapeChange(seatZoneId, shape);
    else if (onZoneMove) onZoneMove(seatZoneId, shape);
  };

  const convertSelected = (targetType) => {
    if (!selected || readOnly) return;
    // Chuyển polygon -> rectangle chỉ lấy bounding box, các đỉnh đã kéo chỉnh sẽ mất;
    // nếu đổi lại thành polygon sau đó, hình L sẽ được sinh lại từ đầu chứ không phục hồi được.
    if (targetType === "rectangle" && selected.shape.type === "polygon") {
      if (!window.confirm("Converting back to a rectangle will discard the custom polygon points you've dragged. Continue?")) return;
    }
    const next = targetType === "polygon"
      ? (selected.shape.type === "polygon" ? selected.shape : rectangleToPolygon(selected.shape))
      : (selected.shape.type === "rectangle" ? selected.shape : polygonToRectangle(selected.shape));
    commitShape(selected.zone.seatZoneId, next);
  };

  // Bắt đầu vẽ tự do cho zone đang chọn — như Ticketbox/Eventbrite: mỗi click đặt 1 đỉnh,
  // click lại gần đỉnh đầu (hoặc Enter) để đóng hình, Esc để huỷ, Backspace để xoá đỉnh vừa đặt.
  const startDrawing = () => {
    if (!selected || readOnly) return;
    if (selected.shape.type === "polygon" && !window.confirm("Drawing a new shape will replace this zone's current custom polygon. Continue?")) return;
    setDraw({ seatZoneId: selected.zone.seatZoneId, points: [] });
    setCursorPos(null);
  };

  const finishDrawing = () => {
    if (!draw || draw.points.length < 3) return;
    commitShape(draw.seatZoneId, { type: "polygon", points: draw.points });
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
      if (e.target === e.target.getStage()) setSelectedId(null);
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
  // Ghế xem trước trong lúc đang vẽ: dùng đúng số ghế thật của zone (đã sinh
  // sẵn từ rows/seatsPerRow lúc tạo zone), xếp lại theo hình đa giác đang vẽ
  // dở — cập nhật ngay mỗi khi thêm một đỉnh, không cần đợi bấm "Done".
  const drawSeatPositions = draw
    ? seatPositionsForPoints(drawingZone?.seats || [], draw.points)
    : [];

  return (
    <div className="ow-zonemap-wrap">
      {!readOnly && selected && !draw && (
        <div className="ow-zonemap-shape-tools">
          <span className="ow-hint">Zone shape:</span>
          <button type="button" className={"ow-shape-pill" + (selected.shape.type === "rectangle" ? " active" : "")} onClick={() => convertSelected("rectangle")}>▭ Rectangle</button>
          <button type="button" className="ow-shape-pill" onClick={startDrawing}>✏️ Draw custom shape</button>
          {selected.shape.type === "polygon" && <span className="ow-hint">Drag a point to move it • Double-click an edge to add a point • Right-click a point to remove it</span>}
        </div>
      )}

      {draw && (
        <div className="ow-zonemap-shape-tools ow-zonemap-drawing-bar">
          <span className="ow-hint">✏️ Drawing mode — click to place points, click the first point (or press Enter) to close the shape. Seats preview live inside the shape as you add points.</span>
          <button type="button" className="ow-shape-pill" disabled={draw.points.length < 3} onClick={finishDrawing}>✓ Done</button>
          <button type="button" className="ow-shape-pill" onClick={cancelDrawing}>✕ Cancel</button>
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
              <Rect
                x={stageWidth * 0.3} y={stageHeight * 0.04} width={stageWidth * 0.4} height={stageHeight * 0.14}
                cornerRadius={10}
                fillLinearGradientStartPoint={{ x: 0, y: 0 }} fillLinearGradientEndPoint={{ x: 0, y: stageHeight * 0.14 }}
                fillLinearGradientColorStops={[0, "#52525b", 1, "#27272a"]}
                stroke="rgba(255,255,255,.12)" strokeWidth={1}
                shadowColor="#000" shadowBlur={14} shadowOpacity={0.5} shadowOffset={{ x: 0, y: 4 }}
                listening={false}
              />
              <Text text="S Â N   K H Ấ U" x={stageWidth * 0.3} y={stageHeight * 0.04} width={stageWidth * 0.4} height={stageHeight * 0.14} align="center" verticalAlign="middle" fontSize={11} fontStyle="bold" fill="#e4e4e7" listening={false} />

              {shapes.filter(({ zone }) => !(draw && draw.seatZoneId === zone.seatZoneId)).map(({ zone, shape }) => shape.type === "polygon" ? (
                <PolygonZoneShape key={zone.seatZoneId} zone={zone} shape={shape} stageWidth={stageWidth} stageHeight={stageHeight} color={colorForTicketType(zone.ticketTypeId)} ticketType={ticketTypeById(zone.ticketTypeId)} readOnly={readOnly || !!draw} selected={selectedId === zone.seatZoneId} onSelect={() => setSelectedId(zone.seatZoneId)} onChange={(next) => commitShape(zone.seatZoneId, next)} />
              ) : (
                <RectangleZoneShape key={zone.seatZoneId} zone={zone} shape={shape} stageWidth={stageWidth} stageHeight={stageHeight} color={colorForTicketType(zone.ticketTypeId)} ticketType={ticketTypeById(zone.ticketTypeId)} readOnly={readOnly || !!draw} selected={selectedId === zone.seatZoneId} onSelect={() => setSelectedId(zone.seatZoneId)} onChange={(next) => commitShape(zone.seatZoneId, next)} />
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
  // Chấm sáng + viền trắng dày, nổi rõ trên nền card mờ, thay vì chấm tối chìm vào màu zone.
  const radius = clamp(Math.min(stepX, stepY) * 0.3, 2.4, 7);
  return zone.seats.map((seat) => {
    const x = padX + ((Number(seat.xCoordinate) || 1) - 0.5) * stepX;
    const y = headerHeight + ((Number(seat.yCoordinate) || 1) - 0.5) * stepY;
    return <Circle key={seat.seatId} x={x} y={y} radius={radius} fill={color} stroke="#fff" strokeWidth={Math.max(1, radius * .3)} shadowColor="#000" shadowBlur={radius * 0.6} shadowOpacity={0.45} listening={false} />;
  });
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
  // Chỉ hiện tên zone trong khối — loại vé/giá đã có ở bảng "Ticket prices" bên
  // cạnh rồi, nhét thêm vào đây chỉ làm label 2 dòng đè lên nhau, rối mắt.
  const label = zone.zoneName;
  const labelH = Math.min(22, Math.max(14, ph * .18));
  const labelFontSize = clamp(Math.min(pw, ph) * .2, 9, 13);
  const cardFill = hexToRgba(color, 0.32);

  return <>
    <Group ref={groupRef} x={px} y={py} rotation={shape.rot || 0} draggable={!readOnly} opacity={hovered && !selected ? 0.92 : 1}
      onClick={onSelect} onTap={onSelect}
      onMouseEnter={(e) => { setHovered(true); if (!readOnly) e.target.getStage().container().style.cursor = "grab"; }}
      onMouseLeave={(e) => { setHovered(false); e.target.getStage().container().style.cursor = "default"; }}
      onDragStart={(e) => { e.target.getStage().container().style.cursor = "grabbing"; }}
      onDragEnd={(e) => { e.target.getStage().container().style.cursor = "grab"; onChange({ ...shape, x: (e.target.x() / stageWidth) * 100, y: (e.target.y() / stageHeight) * 100 }); }}
      onTransformEnd={() => {
        const node = groupRef.current;
        const nextW = Math.max(20, pw * node.scaleX());
        const nextH = Math.max(20, ph * node.scaleY());
        node.scaleX(1); node.scaleY(1);
        onChange({ type: "rectangle", x: clamp((node.x() / stageWidth) * 100, 0, 100), y: clamp((node.y() / stageHeight) * 100, 0, 100), w: clamp((nextW / stageWidth) * 100, MIN_W_PCT, 100), h: clamp((nextH / stageHeight) * 100, MIN_H_PCT, 100), rot: node.rotation() });
      }}>
      <Rect
        width={pw} height={ph} fill={cardFill}
        stroke={selected ? "#fff" : hovered ? "rgba(255,255,255,.7)" : color}
        strokeWidth={selected ? 2.5 : hovered ? 1.8 : 1.4}
        dash={zone.zoneType === "Standing" ? [7, 5] : undefined}
        cornerRadius={8}
        shadowColor="#000" shadowBlur={selected ? 16 : 8} shadowOpacity={selected ? 0.45 : 0.28} shadowOffset={{ x: 0, y: 3 }}
      />
      <Rect x={0} y={0} width={pw} height={labelH} cornerRadius={[8, 8, 0, 0]} fill={color} listening={false} />
      <Text text={label} width={pw} height={labelH} align="center" verticalAlign="middle" fontSize={labelFontSize} fontStyle="bold" fill={readableTextColor(color)} listening={false} padding={3} />
      {renderPhysicalSeats(zone, pw, ph, color)}
    </Group>
    {selected && !readOnly && <Transformer ref={trRef} rotateEnabled keepRatio={false} borderStroke="#fff" borderStrokeWidth={1.5} anchorStroke="#fff" anchorFill="#111827" anchorCornerRadius={4} anchorSize={9} boundBoxFunc={(oldBox, newBox) => newBox.width < 20 || newBox.height < 20 ? oldBox : newBox} />}
  </>;
}

function PolygonZoneShape({ zone, shape, stageWidth, stageHeight, color, ticketType, readOnly, selected, onSelect, onChange }) {
  const [hovered, setHovered] = useState(false);
  const [hoverVertex, setHoverVertex] = useState(null);
  const pointsPx = shape.points.flatMap((p) => [(p.x / 100) * stageWidth, (p.y / 100) * stageHeight]);
  const bounds = polygonBounds(shape.points);
  // Chỉ tên zone trong nhãn — loại vé/giá đã có ở bảng "Ticket prices" bên cạnh.
  const label = zone.zoneName;
  const seatPositions = seatPositionsForPolygon(zone, shape);
  const textColor = readableTextColor(color);
  const cardFill = hexToRgba(color, 0.32);
  const labelWpx = (bounds.w / 100) * stageWidth;
  const labelXpx = (bounds.x / 100) * stageWidth;
  const labelYpx = (bounds.y / 100) * stageHeight;

  const onVertexDragEnd = (index, e) => {
    const nextPoints = shape.points.map((p, i) => i === index ? { x: clamp((e.target.x() / stageWidth) * 100, 0, 100), y: clamp((e.target.y() / stageHeight) * 100, 0, 100) } : p);
    onChange({ type: "polygon", points: nextPoints });
  };

  const movePolygon = (e) => {
    const dx = (e.target.x() / stageWidth) * 100;
    const dy = (e.target.y() / stageHeight) * 100;
    e.target.position({ x: 0, y: 0 });
    const moved = shape.points.map((p) => ({ x: clamp(p.x + dx, 0, 100), y: clamp(p.y + dy, 0, 100) }));
    onChange({ type: "polygon", points: moved });
  };

  return <>
    <Group draggable={!readOnly} opacity={hovered && !selected ? 0.92 : 1}
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
      <Rect x={labelXpx} y={labelYpx} width={labelWpx} height={20} cornerRadius={[6, 6, 0, 0]} fill={color} listening={false} />
      <Text text={label} x={labelXpx} y={labelYpx} width={labelWpx} height={20} align="center" verticalAlign="middle" fontSize={10} fontStyle="bold" fill={textColor} listening={false} />
      {zone.zoneType === "Seated" && seatPositions.map(({ seat, x, y }) => <Circle key={seat.seatId} x={(x / 100) * stageWidth} y={(y / 100) * stageHeight} radius={4} fill={color} stroke="#fff" strokeWidth={1.2} shadowColor="#000" shadowBlur={2.5} shadowOpacity={0.4} listening={false} />)}
    </Group>
    {selected && !readOnly && shape.points.map((p, index) => (
      <Circle
        key={`vertex-${index}`}
        x={(p.x / 100) * stageWidth} y={(p.y / 100) * stageHeight}
        radius={hoverVertex === index ? 7.5 : 6}
        fill="#fff" stroke="#111827" strokeWidth={2}
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
