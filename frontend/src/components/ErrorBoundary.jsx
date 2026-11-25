import React from "react";

export default class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error("ErrorBoundary caught an error:", error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          backgroundColor: "#f8fafc",
          padding: "24px",
          fontFamily: "system-ui, -apple-system, sans-serif"
        }}>
          <div style={{
            maxWidth: "500px",
            width: "100%",
            background: "#ffffff",
            padding: "32px",
            borderRadius: "16px",
            boxShadow: "0 10px 25px -5px rgba(0,0,0,0.1)",
            textAlign: "center"
          }}>
            <h2 style={{ fontSize: "20px", color: "#e11d48", marginBottom: "12px", fontWeight: 700 }}>
              Something went wrong
            </h2>
            <p style={{ fontSize: "14px", color: "#64748b", marginBottom: "20px", lineHeight: 1.5 }}>
              {this.state.error?.message || "An unexpected error occurred while rendering this page."}
            </p>
            <div style={{ display: "flex", gap: "12px", justifyContent: "center" }}>
              <button
                type="button"
                onClick={() => window.location.reload()}
                style={{
                  padding: "10px 20px",
                  borderRadius: "8px",
                  border: "none",
                  background: "#e11d48",
                  color: "#ffffff",
                  fontWeight: 600,
                  fontSize: "14px",
                  cursor: "pointer"
                }}
              >
                Reload Page
              </button>
              <button
                type="button"
                onClick={() => window.location.href = "/my-tickets"}
                style={{
                  padding: "10px 20px",
                  borderRadius: "8px",
                  border: "1px solid #cbd5e1",
                  background: "#ffffff",
                  color: "#334155",
                  fontWeight: 600,
                  fontSize: "14px",
                  cursor: "pointer"
                }}
              >
                Go to My Tickets
              </button>
            </div>
          </div>
        </div>
      );
    }
    return this.props.children;
  }
}
