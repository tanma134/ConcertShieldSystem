import { useEffect, useRef, useState } from "react";
import authApi from "../api/authApi";
import { onChallenge, setStepUpToken } from "../utils/challenge";

const SITE_KEY = import.meta.env.VITE_TURNSTILE_SITE_KEY;
const SCRIPT_SRC = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";

let scriptPromise = null;
function loadTurnstile() {
  if (window.turnstile) return Promise.resolve(window.turnstile);
  if (!scriptPromise) {
    scriptPromise = new Promise((resolve, reject) => {
      const s = document.createElement("script");
      s.src = SCRIPT_SRC;
      s.async = true;
      s.onload = () => resolve(window.turnstile);
      s.onerror = () => {
        scriptPromise = null;
        reject(new Error("Could not load Turnstile"));
      };
      document.head.appendChild(s);
    });
  }
  return scriptPromise;
}

const overlay = {
  position: "fixed", inset: 0, background: "rgba(0,0,0,.55)", zIndex: 10000,
  display: "flex", alignItems: "center", justifyContent: "center",
};
const card = {
  background: "#fff", color: "#111", borderRadius: 12, padding: 24,
  width: "min(92vw, 400px)", boxShadow: "0 10px 40px rgba(0,0,0,.3)",
};
const btn = {
  padding: "8px 16px", borderRadius: 8, border: "1px solid #d1d5db",
  background: "#fff", cursor: "pointer",
};
const btnPrimary = { ...btn, background: "#4F46E5", color: "#fff", border: "none" };

function CaptchaStep({ onToken, onError }) {
  const ref = useRef(null);
  useEffect(() => {
    let widgetId;
    let cancelled = false;
    if (!SITE_KEY) {
      onError("Missing VITE_TURNSTILE_SITE_KEY.");
      return undefined;
    }
    loadTurnstile()
      .then((ts) => {
        if (cancelled || !ref.current) return;
        widgetId = ts.render(ref.current, {
          sitekey: SITE_KEY,
          callback: (token) => onToken(token),
          "error-callback": () => onError("Verification failed. Please try again."),
        });
      })
      .catch((e) => onError(e.message));
    return () => {
      cancelled = true;
      if (widgetId !== undefined && window.turnstile) window.turnstile.remove(widgetId);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return <div ref={ref} style={{ display: "flex", justifyContent: "center", minHeight: 65 }} />;
}

function OtpStep({ onToken, onError }) {
  const [code, setCode] = useState("");
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState("");

  const send = async () => {
    setBusy(true);
    setMsg("");
    try {
      await authApi.sendStepUpOtp();
      setSent(true);
      setMsg("A 6-digit code was sent to your email.");
    } catch (e) {
      setMsg(e.response?.data?.message || "Could not send the code.");
    } finally {
      setBusy(false);
    }
  };

  const verify = async () => {
    setBusy(true);
    setMsg("");
    try {
      const res = await authApi.verifyStepUpOtp({ otp: code.trim() });
      onToken(res.data.stepUpToken, res.data.expiresAt);
    } catch (e) {
      setMsg(e.response?.data?.message || "Invalid or expired code.");
      if (!e.response) onError("Network error.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      {!sent ? (
        <button style={btnPrimary} disabled={busy} onClick={send}>Send code to my email</button>
      ) : (
        <div style={{ display: "flex", gap: 8 }}>
          <input
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
            inputMode="numeric"
            placeholder="123456"
            style={{ flex: 1, padding: 8, borderRadius: 8, border: "1px solid #d1d5db", letterSpacing: 4 }}
          />
          <button style={btnPrimary} disabled={busy || code.length !== 6} onClick={verify}>Verify</button>
        </div>
      )}
      {sent && (
        <button style={{ ...btn, marginTop: 8, border: "none", color: "#4F46E5" }} disabled={busy} onClick={send}>
          Resend code
        </button>
      )}
      {msg && <p style={{ fontSize: 13, color: "#6B7280", marginTop: 8 }}>{msg}</p>}
    </div>
  );
}

export default function ChallengeModal() {
  const [req, setReq] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => onChallenge((r) => { setError(""); setReq(r); }), []);

  if (!req) return null;
  const isOtp = req.type === "OTP";

  const cancel = () => {
    req.reject(new Error("Challenge cancelled"));
    setReq(null);
  };

  return (
    <div style={overlay} role="dialog" aria-modal="true">
      <div style={card}>
        <h3 style={{ marginTop: 0 }}>Security check</h3>
        <p style={{ fontSize: 14, color: "#4B5563" }}>
          {isOtp
            ? "We detected unusual activity. Confirm it is you with a one-time code."
            : "Please confirm you are not a robot to continue."}
        </p>
        {isOtp ? (
          <OtpStep
            onError={setError}
            onToken={(token, expiresAt) => {
              setStepUpToken(token, expiresAt);
              req.resolve({ stepUpToken: token });
              setReq(null);
            }}
          />
        ) : (
          <CaptchaStep
            onError={setError}
            onToken={(token) => {
              req.resolve({ captchaToken: token });
              setReq(null);
            }}
          />
        )}
        {error && <p style={{ color: "#B91C1C", fontSize: 13 }}>{error}</p>}
        <div style={{ textAlign: "right", marginTop: 16 }}>
          <button style={btn} onClick={cancel}>Cancel</button>
        </div>
      </div>
    </div>
  );
}
