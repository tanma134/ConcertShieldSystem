import { useEffect, useRef, useState } from "react";
import {
  CAMERA_CONFIG,
  CAPTURE_KINDS,
  analyzeQuality,
  cameraErrorText,
  drawFrame,
  openCamera,
} from "../../utils/cameraUtils";

// Mở camera, hiển thị khung ngắm và trả về ảnh vừa chụp qua onCapture(blob, quality).
// Không có ô chọn file: ảnh chỉ có thể lấy trực tiếp từ camera.
export default function CameraCapture({ kind, onCapture }) {
  const info = CAPTURE_KINDS[kind];
  const videoRef = useRef(null);
  const stageRef = useRef(null);
  const guideRef = useRef(null);

  const [facing, setFacing] = useState(info.facing);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [flashKey, setFlashKey] = useState(0);

  useEffect(() => {
    // Có cờ cancelled vì React StrictMode (dev) chạy effect 2 lần và getUserMedia trả về bất đồng bộ
    let cancelled = false;
    let stream = null;
    setReady(false);
    setError("");

    (async () => {
      try {
        stream = await openCamera(facing);
        if (cancelled) {
          stream.getTracks().forEach((t) => t.stop());
          return;
        }
        const video = videoRef.current;
        video.srcObject = stream;
        await video.play().catch(() => {});
        if (!video.videoWidth) {
          await new Promise((resolve) => video.addEventListener("loadedmetadata", resolve, { once: true }));
        }
        if (!cancelled) setReady(true);
      } catch (e) {
        if (!cancelled) setError(cameraErrorText(e));
      }
    })();

    return () => {
      cancelled = true;
      if (stream) stream.getTracks().forEach((t) => t.stop());
      if (videoRef.current) videoRef.current.srcObject = null;
    };
  }, [facing]);

  const handleShoot = async () => {
    const video = videoRef.current;
    if (!video || !video.videoWidth) return;

    setFlashKey((k) => k + 1);
    const canvas = drawFrame(video, stageRef.current, guideRef.current, info.guide);
    const quality = analyzeQuality(canvas);
    const blob = await new Promise((resolve) => canvas.toBlob(resolve, "image/jpeg", CAMERA_CONFIG.JPEG_QUALITY));

    if (!blob) {
      setError("Không lưu được ảnh. Hãy chụp lại.");
      return;
    }
    onCapture(blob, quality);
  };

  return (
    <div>
      <div className="kyc-stage" ref={stageRef}>
        <video
          ref={videoRef}
          className={`kyc-video${facing === "user" ? " kyc-video--mirror" : ""}`}
          playsInline
          muted
          autoPlay
        />
        <div ref={guideRef} className={`kyc-guide kyc-guide--${info.guide}`}>
          <span className="kyc-corner kyc-corner--tl" />
          <span className="kyc-corner kyc-corner--tr" />
          <span className="kyc-corner kyc-corner--bl" />
          <span className="kyc-corner kyc-corner--br" />
        </div>
        {flashKey > 0 && <div key={flashKey} className="kyc-flash" />}
      </div>

      <div className="kyc-controls">
        <button
          type="button"
          className="kyc-link"
          onClick={() => setFacing((f) => (f === "user" ? "environment" : "user"))}
        >
          Đổi camera
        </button>
        <button
          type="button"
          className="kyc-shutter"
          aria-label="Chụp ảnh"
          disabled={!ready}
          onClick={handleShoot}
        />
        <span />
      </div>

      {error && (
        <div className="kyc-alert kyc-alert--error" role="alert">
          {error}
        </div>
      )}
    </div>
  );
}
