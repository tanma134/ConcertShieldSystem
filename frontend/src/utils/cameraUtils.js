// Camera utilities for the eKYC flow: open the camera, crop the frame, run a basic quality check.

export const CAMERA_CONFIG = {
  MAX_SIDE: 1600,        // max length of the longest side of the uploaded image (px)
  JPEG_QUALITY: 0.92,
  CARD_PADDING: 0.06,    // add 6% padding around the card frame when cropping
  // On-device check thresholds, only an estimate: adjust based on real-world results
  MIN_BRIGHTNESS: 55,
  MAX_BRIGHTNESS: 215,
  MIN_SHARPNESS: 35,
};

export const CAPTURE_KINDS = {
  front: {
    label: "Front",
    title: "Capture the front of your ID card",
    hint: "Place the card fully inside the frame. Shoot in a well-lit place and avoid glare from lights.",
    facing: "environment",
    guide: "card",
  },
  back: {
    label: "Back",
    title: "Capture the back of your ID card",
    hint: "Flip the card and place the back inside the frame. The text on the card must be readable.",
    facing: "environment",
    guide: "card",
  },
  selfie: {
    label: "Face",
    title: "Capture your face",
    hint: "Look straight at the camera, remove any mask or sunglasses, and keep your face inside the frame.",
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
      return "The camera only works on HTTPS or localhost. Please open the page using an address that starts with https://.";
    case "NotAllowedError":
    case "SecurityError":
      return "Camera access is blocked in your browser. Allow camera access in the site settings, then reload the page.";
    case "NotFoundError":
      return "No camera was found on this device.";
    case "NotReadableError":
      return "The camera is being used by another application. Close that application and try again.";
    default:
      return "Couldn't open the camera. Reload the page and try again.";
  }
}

// Compute the crop area in the video's coordinate system.
// The video is displayed with object-fit: cover inside the frame (cw x ch), so the coordinates must be converted back.
// guideType "card": crop to the card frame (+ padding). Otherwise: take the whole visible area.
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

// Estimate brightness + sharpness (Laplacian variance) on a downscaled copy
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
  if (q.brightness < CAMERA_CONFIG.MIN_BRIGHTNESS) chips.push({ tone: "warn", text: "A bit dark" });
  else if (q.brightness > CAMERA_CONFIG.MAX_BRIGHTNESS) chips.push({ tone: "warn", text: "A bit too bright" });
  else chips.push({ tone: "ok", text: "Good lighting" });

  chips.push(
    q.sharpness < CAMERA_CONFIG.MIN_SHARPNESS
      ? { tone: "warn", text: "A bit blurry" }
      : { tone: "ok", text: "Fairly sharp" }
  );
  return chips;
}