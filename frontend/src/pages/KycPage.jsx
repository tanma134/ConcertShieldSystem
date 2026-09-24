import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import kycApi from "../api/kycApi";
import CameraCapture from "../components/kyc/CameraCapture";
import { CAPTURE_KINDS, qualityChips } from "../utils/cameraUtils";
import "../styles/kyc.css";

const RAIL = ["consent", "front", "back", "selfie", "review"];
const STEP_LABEL = {
  consent: "Đồng ý",
  front: "Mặt trước",
  back: "Mặt sau",
  selfie: "Khuôn mặt",
  review: "Kiểm tra",
};
const NEXT = { front: "back", back: "selfie", selfie: "review" };

/* ---------------------------------------------------------------- *
 * Bước 1: thông báo + đồng ý xử lý dữ liệu
 * ---------------------------------------------------------------- */
function ConsentStep({ state, error, consent, agreed, notice, onAgree, onRetry, onNext }) {
  if (state === "loading") {
    return <p className="kyc-muted">Đang tải nội dung...</p>;
  }

  if (state === "error") {
    return (
      <>
        <h2 className="kyc-title" tabIndex={-1}>Chưa mở được trang này</h2>
        <div className="kyc-alert kyc-alert--error" role="alert">{error}</div>
        <button type="button" className="kyc-button kyc-button--primary" onClick={onRetry}>
          Thử lại
        </button>
      </>
    );
  }

  return (
    <>
      {notice && <div className="kyc-alert kyc-alert--warn" role="status">{notice}</div>}
      <h2 className="kyc-title" tabIndex={-1}>Xác thực danh tính</h2>
      <p className="kyc-lead">
        Bạn cần chụp 3 ảnh trực tiếp bằng camera: mặt trước CCCD, mặt sau CCCD và khuôn mặt của bạn.
      </p>

      <dl className="kyc-facts">
        {consent.items.map((item) => (
          <div key={item.heading}>
            <dt>{item.heading}</dt>
            <dd>{item.content}</dd>
          </div>
        ))}
      </dl>

      <label className="kyc-agree">
        <input type="checkbox" checked={agreed} onChange={(e) => onAgree(e.target.checked)} />
        <span>{consent.checkboxText}</span>
      </label>

      <button type="button" className="kyc-button kyc-button--primary" disabled={!agreed} onClick={onNext}>
        Đồng ý và bắt đầu chụp
      </button>
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Bước 2-4: chụp ảnh rồi xem lại ngay
 * ---------------------------------------------------------------- */
function CaptureStep({ kind, onAccept }) {
  const info = CAPTURE_KINDS[kind];
  const [draft, setDraft] = useState(null);
  const draftRef = useRef(null);
  const acceptedRef = useRef(false);
  draftRef.current = draft;

  // Nếu rời trang khi chưa dùng ảnh nháp thì giải phóng bộ nhớ
  useEffect(
    () => () => {
      if (draftRef.current && !acceptedRef.current) URL.revokeObjectURL(draftRef.current.url);
    },
    []
  );

  const handleCapture = (blob, quality) => setDraft({ blob, quality, url: URL.createObjectURL(blob) });

  const handleRetake = () => {
    URL.revokeObjectURL(draft.url);
    setDraft(null);
  };

  const handleAccept = () => {
    acceptedRef.current = true;
    onAccept(kind, draft);
  };

  if (!draft) {
    return (
      <>
        <h2 className="kyc-title" tabIndex={-1}>{info.title}</h2>
        <p className="kyc-lead">{info.hint}</p>
        <CameraCapture kind={kind} onCapture={handleCapture} />
      </>
    );
  }

  const chips = qualityChips(draft.quality);
  const hasWarn = chips.some((c) => c.tone === "warn");

  return (
    <>
      <h2 className="kyc-title" tabIndex={-1}>Kiểm tra ảnh vừa chụp</h2>
      <p className="kyc-lead">
        {hasWarn
          ? "Ảnh có vẻ chưa tốt. Nên chụp lại để tránh bị từ chối khi xác thực."
          : "Ảnh đạt kiểm tra sơ bộ trên thiết bị. Hệ thống sẽ kiểm tra kỹ hơn khi bạn gửi."}
      </p>
      <img className="kyc-shot" src={draft.url} alt={info.label} />
      <div className="kyc-chips">
        {chips.map((c) => (
          <span key={c.text} className={`kyc-chip kyc-chip--${c.tone}`}>{c.text}</span>
        ))}
      </div>
      <button type="button" className="kyc-button kyc-button--primary" onClick={handleAccept}>
        Dùng ảnh này
      </button>
      <button type="button" className="kyc-button kyc-button--ghost" onClick={handleRetake}>
        Chụp lại
      </button>
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Bước 5: xem lại 3 ảnh và gửi
 * ---------------------------------------------------------------- */
function ReviewStep({ shots, submitting, error, onRetake, onSubmit }) {
  return (
    <>
      <h2 className="kyc-title" tabIndex={-1}>Kiểm tra ảnh trước khi gửi</h2>
      <p className="kyc-lead">Đảm bảo thẻ và khuôn mặt rõ nét, không bị cắt hoặc lóa sáng.</p>

      <div className="kyc-thumbs">
        {["front", "back", "selfie"].map((kind) => (
          <figure className="kyc-thumb" key={kind}>
            <img src={shots[kind].url} alt={CAPTURE_KINDS[kind].label} />
            <figcaption>{CAPTURE_KINDS[kind].label}</figcaption>
            <button type="button" className="kyc-link" disabled={submitting} onClick={() => onRetake(kind)}>
              Chụp lại
            </button>
          </figure>
        ))}
      </div>

      {error && <div className="kyc-alert kyc-alert--error" role="alert">{error}</div>}

      <button type="button" className="kyc-button kyc-button--primary" disabled={submitting} onClick={onSubmit}>
        {submitting ? "Đang xác thực..." : "Gửi để xác thực"}
      </button>
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Kết quả
 * ---------------------------------------------------------------- */
const RESULT_ICON = {
  ok: <path d="M5 12.5l4.5 4.5L19 7.5" />,
  warn: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5V12l3 2" />
    </>
  ),
  bad: <path d="M6.5 6.5l11 11M17.5 6.5l-11 11" />,
};

function ResultStep({ result, onRestart, onHome }) {
  const view =
    {
      Passed: {
        tone: "ok",
        title: "Xác thực thành công",
        text: "Danh tính của bạn đã được xác thực. Bạn có thể tiếp tục mua vé.",
      },
      ManualReview: {
        tone: "warn",
        title: "Hồ sơ đang chờ kiểm duyệt",
        text: result.message || "Chúng tôi sẽ thông báo khi có kết quả.",
      },
      Failed: {
        tone: "bad",
        title: "Chưa xác thực được",
        text: result.message || "Vui lòng chụp lại và thử lại.",
      },
    }[result.status] || { tone: "bad", title: "Chưa xác thực được", text: result.message || "Vui lòng thử lại." };

  return (
    <>
      <div className={`kyc-result-icon kyc-result-icon--${view.tone}`}>
        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          {RESULT_ICON[view.tone]}
        </svg>
      </div>
      <h2 className="kyc-title" tabIndex={-1}>{view.title}</h2>
      <p className="kyc-lead">{view.text}</p>

      {result.status === "Failed" ? (
        <button type="button" className="kyc-button kyc-button--primary" onClick={onRestart}>
          Chụp lại từ đầu
        </button>
      ) : (
        <button type="button" className="kyc-button kyc-button--ghost" onClick={onHome}>
          Về trang chủ
        </button>
      )}
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Trang chính
 * ---------------------------------------------------------------- */
export default function KycPage() {
  const navigate = useNavigate();

  const [step, setStep] = useState("consent");
  const [consent, setConsent] = useState(null);
  const [consentState, setConsentState] = useState("loading"); // loading | ready | error
  const [consentError, setConsentError] = useState("");
  const [agreed, setAgreed] = useState(false);
  const [notice, setNotice] = useState("");

  const [shots, setShots] = useState({ front: null, back: null, selfie: null });
  const [fromReview, setFromReview] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [result, setResult] = useState(null);

  const shotsRef = useRef(shots);
  shotsRef.current = shots;

  const loadConsent = useCallback(async () => {
    setConsentState("loading");
    setConsentError("");
    try {
      const res = await kycApi.getConsent();
      setConsent(res.data);
      setConsentState("ready");
    } catch (err) {
      setConsentError(
        err.response?.data?.message || "Không tải được nội dung đồng ý. Kiểm tra kết nối rồi thử lại."
      );
      setConsentState("error");
    }
  }, []);

  useEffect(() => {
    loadConsent();
  }, [loadConsent]);

  // Giải phóng các ảnh còn giữ trong bộ nhớ khi rời trang
  useEffect(
    () => () => {
      Object.values(shotsRef.current).forEach((s) => s && URL.revokeObjectURL(s.url));
    },
    []
  );

  // Chuyển bước thì đưa focus lên tiêu đề để trình đọc màn hình đọc nội dung mới
  useEffect(() => {
    window.scrollTo({ top: 0 });
    const t = setTimeout(() => document.querySelector(".kyc-title")?.focus(), 0);
    return () => clearTimeout(t);
  }, [step]);

  const clearShots = () => {
    Object.values(shotsRef.current).forEach((s) => s && URL.revokeObjectURL(s.url));
    setShots({ front: null, back: null, selfie: null });
  };

  const acceptShot = (kind, shot) => {
    const old = shotsRef.current[kind];
    if (old) URL.revokeObjectURL(old.url);
    setShots((prev) => ({ ...prev, [kind]: shot }));
    setStep(fromReview ? "review" : NEXT[kind]);
    setFromReview(false);
  };

  const retakeFromReview = (kind) => {
    setFromReview(true);
    setStep(kind);
  };

  const handleSubmit = async () => {
    if (submitting) return;
    setSubmitting(true);
    setSubmitError("");

    const formData = new FormData();
    formData.append("CccdFrontImage", shots.front.blob, "front.jpg");
    formData.append("CccdBackImage", shots.back.blob, "back.jpg");
    formData.append("SelfieImage", shots.selfie.blob, "selfie.jpg");
    formData.append("ConsentAccepted", "true");
    formData.append("ConsentVersion", consent.version);

    try {
      const res = await kycApi.submit(formData);
      setResult(res.data);
      if (res.data.status !== "Failed") clearShots(); // không giữ ảnh CCCD trong bộ nhớ khi không cần
      setStep("result");
    } catch (err) {
      const data = err.response?.data;
      if (err.response?.status === 400 && data?.currentVersion) {
        // Nội dung đồng ý vừa đổi phiên bản: bắt đọc và xác nhận lại
        setNotice("Nội dung đồng ý vừa được cập nhật. Vui lòng đọc lại và xác nhận.");
        setAgreed(false);
        loadConsent();
        setStep("consent");
        return;
      }
      setSubmitError(
        data?.message ||
          (err.response
            ? "Có lỗi xảy ra khi xử lý. Vui lòng thử lại."
            : "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.")
      );
    } finally {
      setSubmitting(false);
    }
  };

  const restart = () => {
    clearShots();
    setResult(null);
    setSubmitError("");
    setStep("front");
  };

  const railIndex = RAIL.indexOf(step);
  const stepText = step === "result" ? "Hoàn tất" : `Bước ${railIndex + 1} trên ${RAIL.length}: ${STEP_LABEL[step]}`;

  return (
    <div className="kyc-page">
      <div className="kyc-card">
        <header className="kyc-head">
          <Link to="/" className="kyc-back">← Trang chủ</Link>
          <p className="kyc-step-text" aria-live="polite">{stepText}</p>
          <div className="kyc-rail" aria-hidden="true">
            {RAIL.map((s, i) => {
              const idx = step === "result" ? RAIL.length : railIndex;
              return <i key={s} className={i < idx ? "is-done" : i === idx ? "is-now" : ""} />;
            })}
          </div>
        </header>

        <div className="kyc-body">
          {step === "consent" && (
            <ConsentStep
              state={consentState}
              error={consentError}
              consent={consent}
              agreed={agreed}
              notice={notice}
              onAgree={setAgreed}
              onRetry={loadConsent}
              onNext={() => {
                setNotice("");
                setStep("front");
              }}
            />
          )}

          {(step === "front" || step === "back" || step === "selfie") && (
            <CaptureStep key={step} kind={step} onAccept={acceptShot} />
          )}

          {step === "review" && (
            <ReviewStep
              shots={shots}
              submitting={submitting}
              error={submitError}
              onRetake={retakeFromReview}
              onSubmit={handleSubmit}
            />
          )}

          {step === "result" && result && (
            <ResultStep result={result} onRestart={restart} onHome={() => navigate("/")} />
          )}
        </div>
      </div>
    </div>
  );
}
