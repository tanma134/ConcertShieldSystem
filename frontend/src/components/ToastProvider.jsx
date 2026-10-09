import { createContext, useCallback, useContext, useMemo, useRef, useState } from "react";
import "./ToastProvider.css";

// Thông báo ngắn (thành công / lỗi / thông tin) hiển thị ở góc phải màn hình.
// Khác NotificationToast (đẩy thông báo từ server qua SignalR): cái này dùng
// cho phản hồi tức thì của thao tác người dùng vừa làm (lưu, lỗi nhập liệu...).
const ToastContext = createContext(null);

const DURATION_MS = { success: 3500, info: 4000, error: 6000 };

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
      // Cùng một nội dung đang hiển thị thì không chồng thêm.
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

// Dùng ngoài Provider (ví dụ trong test) thì trả về hàm rỗng thay vì crash.
const NOOP = { success: () => {}, error: () => {}, info: () => {} };

export function useToast() {
  return useContext(ToastContext) || NOOP;
}

// Thay thế trực tiếp cho `useState("")` của state lỗi: vẫn trả về [error, setError]
// như cũ (banner đỏ trong trang không đổi) nhưng mỗi lỗi mới còn hiện thêm một toast.
export function useToastedError() {
  const toast = useToast();
  const [error, setErrorState] = useState("");
  const setError = useCallback(
    (message) => {
      setErrorState(message);
      if (message) toast.error(message);
    },
    [toast]
  );
  return [error, setError];
}
