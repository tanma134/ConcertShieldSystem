// Bridge between the axios interceptor (428 RISK_CHALLENGE) and the ChallengeModal UI.
// The interceptor calls requestChallenge(type) and waits; the modal resolves it.

let listener = null;
let pending = null;
let stepUp = null; // { token, exp }

export function onChallenge(fn) {
  listener = fn;
  return () => {
    if (listener === fn) listener = null;
  };
}

// Step-up token is reused until it expires (backend TTL is 10 minutes).
export function getStepUpToken() {
  if (stepUp && stepUp.exp > Date.now()) return stepUp.token;
  stepUp = null;
  return null;
}

export function setStepUpToken(token, expiresAt) {
  const exp = expiresAt ? new Date(expiresAt).getTime() - 5000 : Date.now() + 9 * 60 * 1000;
  stepUp = { token, exp };
}

// type: "CAPTCHA" | "OTP". Resolves { captchaToken } or { stepUpToken }.
export function requestChallenge(type) {
  if (!listener) return Promise.reject(new Error("No challenge handler mounted"));
  if (pending && pending.type === type) return pending.promise;

  const promise = new Promise((resolve, reject) => {
    listener({
      type,
      resolve: (v) => {
        pending = null;
        resolve(v);
      },
      reject: (e) => {
        pending = null;
        reject(e);
      },
    });
  });
  pending = { type, promise };
  return promise;
}
