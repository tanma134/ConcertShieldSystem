import { useState } from "react";
import StaffShell from "./StaffShell";
import ticketApi from "../../api/ticketApi";
import "../../styles/reportPages.css";

export default function CheckinScanTerminalPage() {
  const [gateName, setGateName] = useState("Main Gate");
  const [scanType, setScanType] = useState("Entry");
  const [ticketInput, setTicketInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [lastResult, setLastResult] = useState(null);
  const [recentScans, setRecentScans] = useState([]);

  const handleScan = async (e) => {
    if (e) e.preventDefault();
    const code = ticketInput.trim();
    if (!code) return;

    setLoading(true);
    setLastResult(null);

    const payload = {
      gateName: gateName || "Main Gate",
      scanType: scanType,
    };

    // Check if input looks like GUID or numeric ID
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    if (guidPattern.test(code)) {
      payload.ticketCode = code;
    } else if (/^\d+$/.test(code)) {
      payload.ticketId = Number(code);
    } else {
      payload.qrToken = code;
    }

    try {
      const res = await ticketApi.scanTicket(payload);
      const data = res.data?.data;
      const scanItem = {
        time: new Date().toLocaleTimeString(),
        code: code.slice(0, 8) + "...",
        gate: gateName,
        type: scanType,
        success: true,
        resultCode: data?.resultCode || "Valid",
        message: res.data?.message || "Valid scan",
        ownerName: data?.ownerName,
        seatId: data?.seatId,
        ticketTypeName: data?.ticketTypeName,
      };
      setLastResult(scanItem);
      setRecentScans((prev) => [scanItem, ...prev.slice(0, 19)]);
      setTicketInput("");
    } catch (err) {
      const errData = err.response?.data;
      const scanItem = {
        time: new Date().toLocaleTimeString(),
        code: code.slice(0, 8) + "...",
        gate: gateName,
        type: scanType,
        success: false,
        resultCode: errData?.data?.resultCode || "Invalid",
        message: errData?.message || err.message || "Scan failed",
        ownerName: errData?.data?.ownerName,
        seatId: errData?.data?.seatId,
        ticketTypeName: errData?.data?.ticketTypeName,
      };
      setLastResult(scanItem);
      setRecentScans((prev) => [scanItem, ...prev.slice(0, 19)]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <StaffShell title="Check-in & Gate Scan Terminal">
      <div className="tb-container" style={{ maxWidth: "900px", margin: "0 auto", padding: "20px 0" }}>
        <div style={{ marginBottom: "24px" }}>
          <h1 style={{ fontSize: "1.75rem", fontWeight: "700", marginBottom: "8px" }}>
            Gate Check-in Terminal
          </h1>
          <p style={{ color: "#6b7280" }}>
            Scan QR codes or enter ticket identifiers to validate entry and exit according to event policy.
          </p>
        </div>

        {/* Control Box */}
        <div
          style={{
            background: "#ffffff",
            borderRadius: "12px",
            border: "1px solid #e5e7eb",
            padding: "24px",
            boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
            marginBottom: "24px",
          }}
        >
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px", marginBottom: "20px" }}>
            <div>
              <label style={{ display: "block", fontSize: "0.875rem", fontWeight: "600", marginBottom: "6px" }}>
                Gate Assignment
              </label>
              <select
                value={gateName}
                onChange={(e) => setGateName(e.target.value)}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  borderRadius: "8px",
                  border: "1px solid #d1d5db",
                  fontSize: "1rem",
                }}
              >
                <option value="Main Gate">Main Gate</option>
                <option value="Gate A">Gate A</option>
                <option value="Gate B">Gate B</option>
                <option value="VIP Gate">VIP Gate</option>
              </select>
            </div>

            <div>
              <label style={{ display: "block", fontSize: "0.875rem", fontWeight: "600", marginBottom: "6px" }}>
                Scan Mode
              </label>
              <div style={{ display: "flex", gap: "8px" }}>
                <button
                  type="button"
                  onClick={() => setScanType("Entry")}
                  style={{
                    flex: 1,
                    padding: "10px",
                    borderRadius: "8px",
                    border: scanType === "Entry" ? "2px solid #10b981" : "1px solid #d1d5db",
                    background: scanType === "Entry" ? "#ecfdf5" : "#ffffff",
                    color: scanType === "Entry" ? "#065f46" : "#374151",
                    fontWeight: "600",
                    cursor: "pointer",
                  }}
                >
                  🟢 Entry
                </button>
                <button
                  type="button"
                  onClick={() => setScanType("Exit")}
                  style={{
                    flex: 1,
                    padding: "10px",
                    borderRadius: "8px",
                    border: scanType === "Exit" ? "2px solid #f59e0b" : "1px solid #d1d5db",
                    background: scanType === "Exit" ? "#fffbeb" : "#ffffff",
                    color: scanType === "Exit" ? "#92400e" : "#374151",
                    fontWeight: "600",
                    cursor: "pointer",
                  }}
                >
                  🟡 Exit
                </button>
              </div>
            </div>
          </div>

          <form onSubmit={handleScan} style={{ display: "flex", gap: "12px" }}>
            <input
              type="text"
              placeholder="Scan QR code or enter ticket code (UUID / Token / ID)..."
              value={ticketInput}
              onChange={(e) => setTicketInput(e.target.value)}
              autoFocus
              style={{
                flex: 1,
                padding: "12px 16px",
                fontSize: "1.1rem",
                borderRadius: "8px",
                border: "2px solid #6366f1",
                outline: "none",
              }}
            />
            <button
              type="submit"
              disabled={loading || !ticketInput.trim()}
              style={{
                padding: "12px 24px",
                background: scanType === "Entry" ? "#10b981" : "#f59e0b",
                color: "#ffffff",
                border: "none",
                borderRadius: "8px",
                fontSize: "1rem",
                fontWeight: "700",
                cursor: loading || !ticketInput.trim() ? "not-allowed" : "pointer",
              }}
            >
              {loading ? "Scanning..." : scanType === "Entry" ? "Confirm Entry" : "Confirm Exit"}
            </button>
          </form>
        </div>

        {/* Live Feedback Display */}
        {lastResult && (
          <div
            style={{
              padding: "24px",
              borderRadius: "12px",
              marginBottom: "24px",
              background: lastResult.success ? "#f0fdf4" : "#fef2f2",
              border: `2px solid ${lastResult.success ? "#22c55e" : "#ef4444"}`,
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "16px", marginBottom: "12px" }}>
              <span style={{ fontSize: "2.5rem" }}>{lastResult.success ? "✅" : "❌"}</span>
              <div>
                <h2
                  style={{
                    fontSize: "1.35rem",
                    fontWeight: "800",
                    color: lastResult.success ? "#15803d" : "#b91c1c",
                    margin: 0,
                  }}
                >
                  {lastResult.success ? "SCAN SUCCESSFUL" : "SCAN REJECTED"} ({lastResult.resultCode})
                </h2>
                <p style={{ margin: "4px 0 0", fontSize: "1.05rem", fontWeight: "600", color: "#374151" }}>
                  {lastResult.message}
                </p>
              </div>
            </div>

            {lastResult.ownerName && (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(3, 1fr)",
                  gap: "12px",
                  marginTop: "16px",
                  paddingTop: "16px",
                  borderTop: "1px dashed #d1d5db",
                  fontSize: "0.95rem",
                }}
              >
                <div>
                  <span style={{ color: "#6b7280" }}>Attendee:</span> <strong>{lastResult.ownerName}</strong>
                </div>
                <div>
                  <span style={{ color: "#6b7280" }}>Ticket Type:</span> <strong>{lastResult.ticketTypeName}</strong>
                </div>
                <div>
                  <span style={{ color: "#6b7280" }}>Seat:</span> <strong>{lastResult.seatId ? `Seat #${lastResult.seatId}` : "General Admission"}</strong>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Recent Scans Table */}
        <div
          style={{
            background: "#ffffff",
            borderRadius: "12px",
            border: "1px solid #e5e7eb",
            padding: "20px",
          }}
        >
          <h3 style={{ fontSize: "1.1rem", fontWeight: "700", marginBottom: "12px" }}>
            Recent Scan History
          </h3>
          {recentScans.length === 0 ? (
            <p style={{ color: "#9ca3af", fontStyle: "italic", margin: 0 }}>No scans recorded in this session yet.</p>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.9rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e5e7eb", textAlign: "left", color: "#6b7280" }}>
                    <th style={{ padding: "8px 12px" }}>Time</th>
                    <th style={{ padding: "8px 12px" }}>Ticket Code</th>
                    <th style={{ padding: "8px 12px" }}>Gate</th>
                    <th style={{ padding: "8px 12px" }}>Type</th>
                    <th style={{ padding: "8px 12px" }}>Result</th>
                    <th style={{ padding: "8px 12px" }}>Details</th>
                  </tr>
                </thead>
                <tbody>
                  {recentScans.map((s, idx) => (
                    <tr key={idx} style={{ borderBottom: "1px solid #f3f4f6" }}>
                      <td style={{ padding: "8px 12px" }}>{s.time}</td>
                      <td style={{ padding: "8px 12px", fontFamily: "monospace" }}>{s.code}</td>
                      <td style={{ padding: "8px 12px" }}>{s.gate}</td>
                      <td style={{ padding: "8px 12px" }}>
                        <span
                          style={{
                            padding: "2px 8px",
                            borderRadius: "4px",
                            fontSize: "0.8rem",
                            fontWeight: "600",
                            background: s.type === "Entry" ? "#d1fae5" : "#fef3c7",
                            color: s.type === "Entry" ? "#065f46" : "#92400e",
                          }}
                        >
                          {s.type}
                        </span>
                      </td>
                      <td style={{ padding: "8px 12px" }}>
                        <span
                          style={{
                            padding: "2px 8px",
                            borderRadius: "4px",
                            fontSize: "0.8rem",
                            fontWeight: "700",
                            background: s.success ? "#dcfce7" : "#fee2e2",
                            color: s.success ? "#166534" : "#991b1b",
                          }}
                        >
                          {s.resultCode}
                        </span>
                      </td>
                      <td style={{ padding: "8px 12px", color: "#4b5563" }}>{s.message}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </StaffShell>
  );
}
