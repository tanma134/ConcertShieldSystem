import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import "./ToastProvider.css";

// Ephemeral user-action toasts (distinct from server-pushed SignalR NotificationToast).
const ToastContext = createContext(null);

const DURATION_MS = { success: 3500, info: 4000, error: 6000, warning: 5000 };

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  const push = useCallback(
    (type, message) => {
      if (!message) return;
      const id = nextId.current++;
      // Avoid stacking duplicate active toasts.
      setToasts((current) =>
        current.some((t) => t.type === type && t.message === message)
          ? current
          : [...current, { id, type, message }]
      );
      window.setTimeout(() => dismiss(id), DURATION_MS[type] || DURATION_MS.info);
    },
    [dismiss]
  );

  const api = useMemo(
    () => ({
      success: (message) => push("success", message),
      error: (message) => push("error", message),
      info: (message) => push("info", message),
      warning: (message) => push("warning", message),
      showSuccess: (message) => push("success", message),
      showError: (message) => push("error", message),
      showInfo: (message) => push("info", message),
      showWarning: (message) => push("warning", message),
    }),
    [push]
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="app-toast-stack" role="status" aria-live="polite">
        {toasts.map((t) => (
          <div key={t.id} className={`app-toast app-toast-${t.type}`}>
            <span className="app-toast-msg">{t.message}</span>
            <button type="button" className="app-toast-close" onClick={() => dismiss(t.id)} aria-label="Dismiss">
              ×
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

// Fall back to no-op handlers when used outside Provider (e.g. in tests).
const NOOP = { success: () => {}, error: () => {}, info: () => {} };

export function useToast() {
  return useContext(ToastContext) || NOOP;
}

// Drop-in replacement for error state: preserves [error, setError], shows a toast, and auto-clears the banner.
export function useToastedError(autoDismissMs = 8000) {
  const toast = useToast();
  const [error, setErrorState] = useState("");
  const timerRef = useRef(null);

  const setError = useCallback(
    (message) => {
      setErrorState(message);
      if (timerRef.current) clearTimeout(timerRef.current);
      if (message) {
        toast.error(message);
        if (autoDismissMs > 0) {
          timerRef.current = setTimeout(() => {
            setErrorState("");
          }, autoDismissMs);
        }
      }
    },
    [toast, autoDismissMs]
  );

  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);

  return [error, setError];
}
