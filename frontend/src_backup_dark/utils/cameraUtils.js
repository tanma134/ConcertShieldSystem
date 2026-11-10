// Tiện ích camera cho luồng eKYC: mở camera, cắt khung ảnh, kiểm tra chất lượng sơ bộ.

export const CAMERA_CONFIG = {
  MAX_SIDE: 1600,        // cạnh dài tối đa của ảnh gửi đi (px)
  JPEG_QUALITY: 0.92,
  CARD_PADDING: 0.06,    // chừa thêm 6% quanh khung thẻ khi cắt ảnh
  // Ngưỡng kiểm tra sơ bộ trên máy, chỉ là ước lượng: chỉnh theo thực tế
  MIN_BRIGHTNESS: 55,
  MAX_BRIGHTNESS: 215,
  MIN_SHARPNESS: 35,
};

export const CAPTURE_KINDS = {
  front: {
    label: "Mặt trước",
    title: "Chụp mặt trước CCCD",
    hint: "Đặt thẻ nằm gọn trong khung. Chụp nơi đủ sáng và tránh phản chiếu ánh đèn.",
    facing: "environment",
    guide: "card",
  },
  back: {
    label: "Mặt sau",
    title: "Chụp mặt sau CCCD",
    hint: "Lật thẻ lại và đặt mặt sau vào khung. Chữ trên thẻ phải đọc được.",
    facing: "environment",
    guide: "card",
  },
  selfie: {
    label: "Khuôn mặt",
    title: "Chụp khuôn mặt của bạn",
    hint: "Nhìn thẳng vào camera, bỏ khẩu trang và kính râm, để mặt nằm trong khung.",
    facing: "user",
    guide: "oval",
  },
};

export async function openCamera(facing) {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    const err = new Error("insecure");
    err.name = "InsecureContext";
    throw err;
  }
  try {
    return await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: { facingMode: { ideal: facing }, width: { ideal: 1920 }, height: { ideal: 1080 } },
    });
  } catch (e) {
    if (e.name === "OverconstrainedError" || e.name === "NotFoundError") {
      return navigator.mediaDevices.getUserMedia({ audio: false, video: true });
    }
    throw e;
  }
}

export function cameraErrorText(e) {
  switch (e && e.name) {
    case "InsecureContext":
      return "Camera chỉ hoạt động trên HTTPS hoặc localhost. Hãy mở trang bằng địa chỉ bắt đầu bằng https://.";
    case "NotAllowedError":
    case "SecurityError":
      return "Trình duyệt chưa được cấp quyền dùng camera. Cho phép camera trong cài đặt của trang rồi tải lại.";
    case "NotFoundError":
      return "Không tìm thấy camera trên thiết bị này.";
    case "NotReadableError":
      return "Camera đang được ứng dụng khác sử dụng. Đóng ứng dụng đó rồi thử lại.";
    default:
      return "Không mở được camera. Tải lại trang và thử lại.";
  }
}

// Tính vùng cần cắt trong hệ tọa độ của video.
// Video đang hiển thị dạng object-fit: cover trong khung (cw x ch) nên phải quy đổi ngược.
// guideType "card": cắt theo khung thẻ (+ padding). Còn lại: lấy toàn bộ vùng đang nhìn thấy.
export function computeCropRect({ cw, ch, vw, vh, guide, stage, guideType, padding = CAMERA_CONFIG.CARD_PADDING }) {
  const scale = Math.max(cw / vw, ch / vh);
  const offX = (vw * scale - cw) / 2;
  const offY = (vh * scale - ch) / 2;

  let x = 0, y = 0, w = cw, h = ch;
  if (guideType === "card" && guide && stage) {
    const px = guide.width * padding;
    const py = guide.height * padding;
    x = Math.max(0, guide.left - stage.left - px);
    y = Math.max(0, guide.top - stage.top - py);
    w = Math.min(cw - x, guide.width + 2 * px);
    h = Math.min(ch - y, guide.height + 2 * py);
  }

  return { sx: (x + offX) / scale, sy: (y + offY) / scale, sw: w / scale, sh: h / scale };
}

export function drawFrame(video, stageEl, guideEl, guideType) {
  const { sx, sy, sw, sh } = computeCropRect({
    cw: stageEl.clientWidth,
    ch: stageEl.clientHeight,
    vw: video.videoWidth,
    vh: video.videoHeight,
    guide: guideEl.getBoundingClientRect(),
    stage: stageEl.getBoundingClientRect(),
    guideType,
  });

  const k = Math.min(1, CAMERA_CONFIG.MAX_SIDE / Math.max(sw, sh));
  const canvas = document.createElement("canvas");
  canvas.width = Math.round(sw * k);
  canvas.height = Math.round(sh * k);
  canvas.getContext("2d").drawImage(video, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
  return canvas;
}

// Ước lượng độ sáng + độ nét (phương sai Laplacian) trên bản thu nhỏ
export function analyzeQuality(canvas) {
  const w = 320;
  const h = Math.max(1, Math.round(canvas.height * (w / canvas.width)));
  const c = document.createElement("canvas");
  c.width = w;
  c.height = h;
  const ctx = c.getContext("2d", { willReadFrequently: true });
  ctx.drawImage(canvas, 0, 0, w, h);
  const { data } = ctx.getImageData(0, 0, w, h);

  const gray = new Float32Array(w * h);
  let sum = 0;
  for (let i = 0, j = 0; i < data.length; i += 4, j++) {
    const g = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    gray[j] = g;
    sum += g;
  }
  const brightness = sum / (w * h);

  let lsum = 0, lsq = 0, n = 0;
  for (let y = 1; y < h - 1; y++) {
    for (let x = 1; x < w - 1; x++) {
      const i = y * w + x;
      const lap = gray[i - 1] + gray[i + 1] + gray[i - w] + gray[i + w] - 4 * gray[i];
      lsum += lap;
      lsq += lap * lap;
      n++;
    }
  }
  const mean = lsum / n;
  return { brightness, sharpness: lsq / n - mean * mean };
}

export function qualityChips(q) {
  const chips = [];
  if (q.brightness < CAMERA_CONFIG.MIN_BRIGHTNESS) chips.push({ tone: "warn", text: "Ảnh hơi tối" });
  else if (q.brightness > CAMERA_CONFIG.MAX_BRIGHTNESS) chips.push({ tone: "warn", text: "Ảnh hơi chói" });
  else chips.push({ tone: "ok", text: "Đủ sáng" });

  chips.push(
    q.sharpness < CAMERA_CONFIG.MIN_SHARPNESS
      ? { tone: "warn", text: "Ảnh hơi mờ" }
      : { tone: "ok", text: "Khá rõ nét" }
  );
  return chips;
}