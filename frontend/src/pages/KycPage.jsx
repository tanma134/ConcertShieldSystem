import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import kycApi from "../api/kycApi";
import CameraCapture from "../components/kyc/CameraCapture";
import { CAPTURE_KINDS, qualityChips } from "../utils/cameraUtils";
import "../styles/kyc.css";
import Header from "../components/Header";
import Footer from "../components/Footer";

const RAIL = ["consent", "front", "back", "selfie", "review"];
const STEP_LABEL = {
  consent: "Consent",
  front: "Front",
  back: "Back",
  selfie: "Face",
  review: "Review",
};
const NEXT = { front: "back", back: "selfie", selfie: "review" };

/* ---------------------------------------------------------------- *
 * Step 1: notice + consent to data processing
 * ---------------------------------------------------------------- */
function ConsentStep({ state, error, consent, agreed, notice, onAgree, onRetry, onNext }) {
  if (state === "loading") {
    return <p className="kyc-muted">Loading content...</p>;
  }

  if (state === "error") {
    return (
      <>
        <h2 className="kyc-title" tabIndex={-1}>Couldn't load this page</h2>
        <div className="kyc-alert kyc-alert--error" role="alert">{error}</div>
        <button type="button" className="kyc-button kyc-button--primary" onClick={onRetry}>
          Retry
        </button>
      </>
    );
  }

  return (
    <>
      {notice && <div className="kyc-alert kyc-alert--warn" role="status">{notice}</div>}
      <h2 className="kyc-title" tabIndex={-1}>Identity Verification</h2>
      <p className="kyc-lead">
        You need to take 3 live photos using your camera: the front of your ID card, the back of your ID card, and a selfie of your face.
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
        Agree and start capturing
      </button>
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Steps 2-4: capture a photo, then review it immediately
 * ---------------------------------------------------------------- */
function CaptureStep({ kind, onAccept }) {
  const info = CAPTURE_KINDS[kind];
  const [draft, setDraft] = useState(null);
  const draftRef = useRef(null);
  const acceptedRef = useRef(false);
  draftRef.current = draft;

  // Release memory if the user leaves the page before using the draft photo
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
      <h2 className="kyc-title" tabIndex={-1}>Review your photo</h2>
      <p className="kyc-lead">
        {hasWarn
          ? "This photo doesn't look great. We recommend retaking it to avoid rejection during verification."
          : "The photo passed the on-device check. The system will check it more thoroughly when you submit."}
      </p>
      <img className="kyc-shot" src={draft.url} alt={info.label} />
      <div className="kyc-chips">
        {chips.map((c) => (
          <span key={c.text} className={`kyc-chip kyc-chip--${c.tone}`}>{c.text}</span>
        ))}
      </div>
      <button type="button" className="kyc-button kyc-button--primary" onClick={handleAccept}>
        Use this photo
      </button>
      <button type="button" className="kyc-button kyc-button--ghost" onClick={handleRetake}>
        Retake
      </button>
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Step 5: review the 3 photos and submit
 * ---------------------------------------------------------------- */
function ReviewStep({ shots, submitting, error, onRetake, onSubmit }) {
  return (
    <>
      <h2 className="kyc-title" tabIndex={-1}>Review your photos before submitting</h2>
      <p className="kyc-lead">Make sure the card and your face are clear, not cropped, and not overexposed.</p>

      <div className="kyc-thumbs">
        {["front", "back", "selfie"].map((kind) => (
          <figure className="kyc-thumb" key={kind}>
            <img src={shots[kind].url} alt={CAPTURE_KINDS[kind].label} />
            <figcaption>{CAPTURE_KINDS[kind].label}</figcaption>
            <button type="button" className="kyc-link" disabled={submitting} onClick={() => onRetake(kind)}>
              Retake
            </button>
          </figure>
        ))}
      </div>

      {error && <div className="kyc-alert kyc-alert--error" role="alert">{error}</div>}

      <button type="button" className="kyc-button kyc-button--primary" disabled={submitting} onClick={onSubmit}>
        {submitting ? "Verifying..." : "Submit for verification"}
      </button>
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Result
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
        title: "Verification successful",
        text: "Your identity has been verified. You can continue purchasing tickets.",
      },
      ManualReview: {
        tone: "warn",
        title: "Your submission is under review",
        text: result.message || "We'll notify you once there's a result.",
      },
      Failed: {
        tone: "bad",
        title: "Verification unsuccessful",
        text: result.message || "Please retake your photos and try again.",
      },
    }[result.status] || { tone: "bad", title: "Verification unsuccessful", text: result.message || "Please try again." };

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
          Start over
        </button>
      ) : (
        <button type="button" className="kyc-button kyc-button--ghost" onClick={onHome}>
          Back to home
        </button>
      )}
    </>
  );
}

/* ---------------------------------------------------------------- *
 * Main page
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
        err.response?.data?.message || "Couldn't load the consent content. Check your connection and try again."
      );
      setConsentState("error");
    }
  }, []);

  useEffect(() => {
    loadConsent();
  }, [loadConsent]);

  // Release any photos still held in memory when leaving the page
  useEffect(
    () => () => {
      Object.values(shotsRef.current).forEach((s) => s && URL.revokeObjectURL(s.url));
    },
    []
  );

  // On step change, move focus to the heading so screen readers announce the new content
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
      if (res.data.status !== "Failed") clearShots(); // don't keep ID photos in memory once they're no longer needed
      setStep("result");
    } catch (err) {
      const data = err.response?.data;
      if (err.response?.status === 400 && data?.currentVersion) {
        // The consent content just changed version: require re-reading and re-confirming
        setNotice("The consent content has just been updated. Please read it again and confirm.");
        setAgreed(false);
        loadConsent();
        setStep("consent");
        return;
      }
      setSubmitError(
        data?.message ||
          (err.response
            ? "Something went wrong while processing. Please try again."
            : "Couldn't connect to the server. Check your network and try again.")
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
  const stepText = step === "result" ? "Done" : `Step ${railIndex + 1} of ${RAIL.length}: ${STEP_LABEL[step]}`;

  return (
    <>
      <Header />

      <div className="kyc-page">
        <div className="kyc-card">
          <header className="kyc-head">
            <Link to="/" className="kyc-back">← Home</Link>
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

      <Footer />
    </>
  );
}
