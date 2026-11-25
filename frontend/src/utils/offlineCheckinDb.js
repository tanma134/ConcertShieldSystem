// ==========================================
// IndexedDB Storage Layer for Offline Check-in (UC_14.5 & UC_14.6)
// Enhanced with Gate Restriction & Robust QR Matching
// ==========================================

const DB_NAME = "ConcertShieldOfflineCheckin";
const DB_VERSION = 1;

function openDb() {
  return new Promise((resolve, reject) => {
    if (!window.indexedDB) {
      reject(new Error("IndexedDB is not supported on this device/browser."));
      return;
    }

    const request = window.indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = (event) => {
      const db = event.target.result;

      // Store 1: Cached tickets manifest for local verification
      if (!db.objectStoreNames.contains("cachedTickets")) {
        const ticketStore = db.createObjectStore("cachedTickets", { keyPath: "ticketCode" });
        ticketStore.createIndex("eventId", "eventId", { unique: false });
        ticketStore.createIndex("status", "status", { unique: false });
        ticketStore.createIndex("qrToken", "qrToken", { unique: false });
        ticketStore.createIndex("ticketId", "ticketId", { unique: false });
      }

      // Store 2: Offline scan queue waiting to sync
      if (!db.objectStoreNames.contains("offlineQueue")) {
        const queueStore = db.createObjectStore("offlineQueue", { keyPath: "scanId" });
        queueStore.createIndex("eventId", "eventId", { unique: false });
        queueStore.createIndex("scannedAt", "scannedAt", { unique: false });
      }

      // Store 3: Package Metadata
      if (!db.objectStoreNames.contains("packageMeta")) {
        db.createObjectStore("packageMeta", { keyPath: "eventId" });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function extractPayloadCode(rawPayload) {
  if (!rawPayload) return "";
  let trimmed = String(rawPayload).trim();

  // 1. If payload is JSON format, e.g. {"ticketCode":"...", "qrToken":"..."}
  if (trimmed.startsWith("{") && trimmed.endsWith("}")) {
    try {
      const parsed = JSON.parse(trimmed);
      return parsed.qrToken || parsed.ticketCode || parsed.code || parsed.ticketId || trimmed;
    } catch {
      // ignore
    }
  }

  // 2. If payload has ticketCode:token or ticketCode_token prefix
  if (trimmed.includes(":")) {
    const prefix = trimmed.split(":")[0].trim();
    if (prefix) return prefix;
  }

  // 3. If payload is URL format, e.g. https://domain.com/verify?token=XYZ or ?code=XYZ
  if (trimmed.includes("://") || trimmed.includes("?") || trimmed.includes("/")) {
    try {
      const url = new URL(trimmed.includes("://") ? trimmed : `https://dummy.local/${trimmed}`);
      const codeParam = url.searchParams.get("code") || url.searchParams.get("token") || url.searchParams.get("qrToken");
      if (codeParam) return codeParam.trim();
      const segments = url.pathname.split("/").filter(Boolean);
      if (segments.length > 0) {
        return segments[segments.length - 1].trim();
      }
    } catch {
      // ignore
    }
  }

  return trimmed;
}

function isWithinShift(startStr, endStr) {
  if (!startStr || !endStr) return true;
  try {
    const now = new Date();
    const currentMinutes = now.getHours() * 60 + now.getMinutes();

    const parseToMinutes = (str) => {
      const parts = str.trim().split(":");
      if (parts.length >= 2) {
        return parseInt(parts[0], 10) * 60 + parseInt(parts[1], 10);
      }
      return null;
    };

    const startMin = parseToMinutes(startStr);
    const endMin = parseToMinutes(endStr);

    if (startMin !== null && endMin !== null) {
      if (endMin >= startMin) {
        // Daytime shift
        return currentMinutes >= startMin && currentMinutes <= endMin;
      } else {
        // Overnight shift
        return currentMinutes >= startMin || currentMinutes <= endMin;
      }
    }
    return true;
  } catch {
    return true;
  }
}

export const offlineCheckinDb = {
  // UC_14.5: Save downloaded ticket package into IndexedDB
  async saveOfflinePackage(eventId, packageData) {
    const db = await openDb();
    const eventIdNum = Number(eventId);

    return new Promise((resolve, reject) => {
      const tx = db.transaction(["cachedTickets", "packageMeta"], "readwrite");
      const ticketStore = tx.objectStore("cachedTickets");
      const metaStore = tx.objectStore("packageMeta");

      // Save metadata
      metaStore.put({
        eventId: eventIdNum,
        eventTitle: packageData.eventTitle,
        assignedGate: packageData.assignedGate || "All Gates",
        allowedTicketTypes: Array.isArray(packageData.allowedTicketTypes)
          ? packageData.allowedTicketTypes
          : [],
        shiftStartTime: packageData.shiftStartTime || null,
        shiftEndTime: packageData.shiftEndTime || null,
        exportedAt: packageData.exportedAt,
        totalTickets: packageData.totalTickets,
        downloadedAt: new Date().toISOString(),
        signature: packageData.signature || "",
      });

      // Save each ticket
      if (Array.isArray(packageData.tickets)) {
        packageData.tickets.forEach((t, idx) => {
          const rawCode = t.ticketCode ? String(t.ticketCode).trim() : `TKT-E${eventIdNum}-${t.ticketId || idx + 1}`;
          const qToken = t.qrToken ? String(t.qrToken).trim() : "";
          const tid = t.ticketId ? Number(t.ticketId) : null;

          const rec = {
            ticketCode: rawCode,
            ticketId: tid,
            qrToken: qToken,
            eventId: eventIdNum,
            attendeeName: t.attendeeName || "Attendee",
            zoneName: t.zoneName || "General",
            seatNumber: t.seatNumber || `Seat-${idx + 1}`,
            assignedGate: t.assignedGate || packageData.assignedGate || "Main Gate",
            isCheckedIn: t.isCheckedIn || t.status === "Used",
            status: t.status || (t.isCheckedIn ? "Used" : "Available"),
            ticketTokenHash: t.ticketTokenHash || "",
            updatedAt: new Date().toISOString(),
          };

          ticketStore.put(rec);

          // If a dynamic QR token exists, also store alias record keyed by qrToken for fast direct lookup
          if (qToken && qToken !== rawCode) {
            ticketStore.put({
              ...rec,
              ticketCode: qToken,
              primaryTicketCode: rawCode,
            });
          }
        });
      }

      tx.oncomplete = () => resolve({ success: true, count: packageData.tickets?.length || 0 });
      tx.onerror = () => reject(tx.error);
    });
  },

  // Get cached package metadata
  async getPackageMeta(eventId) {
    const db = await openDb();
    const eventIdNum = Number(eventId);

    return new Promise((resolve, reject) => {
      const tx = db.transaction("packageMeta", "readonly");
      const store = tx.objectStore("packageMeta");
      const request = store.get(eventIdNum);

      request.onsuccess = () => resolve(request.result || null);
      request.onerror = () => reject(request.error);
    });
  },

  // UC_14.5: Verify and perform check-in locally in IndexedDB
  // Features: Robust Payload Matching + Shift Time Validation + Gate Ticket Type Validation
  async verifyOfflineTicket(eventId, rawPayload, currentGateName = "Main Gate", staffUserId = 1) {
    const db = await openDb();
    const eventIdNum = Number(eventId);
    const rawTrimmed = String(rawPayload || "").trim();
    const cleanedCode = extractPayloadCode(rawPayload);
    const upperCode = cleanedCode.toUpperCase();

    // Fetch package metadata first to check shift and gate restrictions
    const meta = await this.getPackageMeta(eventIdNum);
    const allowedTypes = meta?.allowedTicketTypes || [];

    // 0.1 Dynamic QR Expiration Check (Anti-Screenshot Protection in Offline Mode)
    if (rawTrimmed.includes(":")) {
      const parts = rawTrimmed.split(":");
      // Format: {ticketCode}:{timestamp}:{randomToken}
      if (parts.length >= 3) {
        const qrTimestamp = parseInt(parts[1], 10);
        if (!isNaN(qrTimestamp) && qrTimestamp > 0) {
          const nowSec = Math.floor(Date.now() / 1000);
          // 60s rotation + 30s buffer for clock skew = 90s
          if (Math.abs(nowSec - qrTimestamp) > 90) {
            return {
              success: false,
              status: "EXPIRED_QR",
              message: "Dynamic QR code has expired. Please ask attendee to refresh their ticket.",
              ticketCode: cleanedCode || parts[0],
              customerName: "Attendee",
              zoneName: "N/A",
              isOffline: true,
            };
          }
        }
      }
    }

    return new Promise((resolve, reject) => {
      const tx = db.transaction(["cachedTickets", "offlineQueue"], "readwrite");
      const ticketStore = tx.objectStore("cachedTickets");
      const queueStore = tx.objectStore("offlineQueue");

      // Try exact direct lookup first
      const getReq = ticketStore.get(cleanedCode);

      getReq.onsuccess = () => {
        let ticket = getReq.result;

        // Try direct lookup by rawTrimmed if different
        if (!ticket && rawTrimmed !== cleanedCode) {
          const rawReq = ticketStore.get(rawTrimmed);
          rawReq.onsuccess = () => {
            if (rawReq.result) {
              proceedValidation(rawReq.result);
            } else {
              tryUpper();
            }
          };
          rawReq.onerror = () => tryUpper();
          return;
        }

        tryUpper();

        function tryUpper() {
          if (!ticket && upperCode !== cleanedCode) {
            const upperReq = ticketStore.get(upperCode);
            upperReq.onsuccess = () => {
              if (upperReq.result) {
                proceedValidation(upperReq.result);
              } else {
                searchAllList();
              }
            };
            upperReq.onerror = () => searchAllList();
            return;
          }

          if (!ticket) {
            searchAllList();
            return;
          }

          proceedValidation(ticket);
        }
      };

      getReq.onerror = () => searchAllList();

      const searchAllList = () => {
        const allReq = ticketStore.getAll();
        allReq.onsuccess = () => {
          const list = allReq.result || [];
          const numInput = Number(cleanedCode);
          const hasNum = !isNaN(numInput) && numInput > 0;

          const matched = list.find((t) => {
            if (eventIdNum > 0 && t.eventId && t.eventId !== eventIdNum && list.some(x => x.eventId === eventIdNum)) {
              return false;
            }

            // 1. Exact string match
            if (t.ticketCode === cleanedCode || t.primaryTicketCode === cleanedCode) return true;
            if (t.ticketCode === rawTrimmed || t.primaryTicketCode === rawTrimmed) return true;
            if (t.qrToken && (t.qrToken === cleanedCode || t.qrToken === rawTrimmed)) return true;

            // 2. Starts with ticket code (e.g. ticketCode:dynamicToken)
            if (t.ticketCode && (rawTrimmed.startsWith(t.ticketCode + ":") || rawTrimmed.startsWith(t.ticketCode + "_"))) return true;
            if (t.primaryTicketCode && (rawTrimmed.startsWith(t.primaryTicketCode + ":") || rawTrimmed.startsWith(t.primaryTicketCode + "_"))) return true;

            // 3. Case-insensitive match
            if (t.ticketCode && t.ticketCode.toUpperCase() === upperCode) return true;
            if (t.primaryTicketCode && t.primaryTicketCode.toUpperCase() === upperCode) return true;
            if (t.qrToken && t.qrToken.toUpperCase() === upperCode) return true;

            // 4. Numeric Ticket ID match
            if (hasNum && t.ticketId === numInput) return true;
            if (t.ticketId && String(t.ticketId) === cleanedCode) return true;

            // 5. Token hash match
            if (t.ticketTokenHash && t.ticketTokenHash.toUpperCase() === upperCode) return true;

            return false;
          });

          proceedValidation(matched);
        };
        allReq.onerror = () => proceedValidation(null);
      };

      const proceedValidation = (ticket) => {
        // 1. Ticket not found in local package
        if (!ticket || ticket.eventId !== eventIdNum) {
          resolve({
            success: false,
            status: "INVALID_QR",
            message: "Ticket not found or invalid QR code in offline package.",
            ticketCode: cleanedCode,
            customerName: "N/A",
            zoneName: "N/A",
            isOffline: true,
          });
          return;
        }

        const actualTicketCode = ticket.primaryTicketCode || ticket.ticketCode;
        const ticketZone = ticket.zoneName || "General Admission";

        // 2. Gate & Ticket Type Restriction Check
        if (allowedTypes && allowedTypes.length > 0) {
          const isAllowedAtGate = allowedTypes.some(
            (at) =>
              at.toLowerCase() === ticketZone.toLowerCase() ||
              ticketZone.toLowerCase().includes(at.toLowerCase()) ||
              at.toLowerCase().includes(ticketZone.toLowerCase())
          );

          if (!isAllowedAtGate) {
            resolve({
              success: false,
              status: "WRONG_GATE",
              message: `Wrong Gate: Ticket [${ticketZone}] is not permitted at gate [${currentGateName || meta?.assignedGate || "current"}]. Permitted types: [${allowedTypes.join(", ")}].`,
              ticketCode: actualTicketCode,
              customerName: ticket.attendeeName,
              zoneName: ticketZone,
              seatNumber: ticket.seatNumber,
              isOffline: true,
            });
            return;
          }
        }

        // 3. Ticket already used / checked-in
        if (ticket.isCheckedIn || ticket.status === "Used" || ticket.status === "USED") {
          resolve({
            success: false,
            status: "ALREADY_USED",
            message: "This ticket has already been checked in.",
            ticketCode: actualTicketCode,
            customerName: ticket.attendeeName,
            zoneName: ticketZone,
            seatNumber: ticket.seatNumber,
            isOffline: true,
          });
          return;
        }

        // 4. Ticket revoked
        if (ticket.status === "Revoked" || ticket.status === "REVOKED") {
          resolve({
            success: false,
            status: "TICKET_REVOKED",
            message: "This ticket has been cancelled or revoked by Event Administration.",
            ticketCode: actualTicketCode,
            customerName: ticket.attendeeName,
            zoneName: ticketZone,
            isOffline: true,
          });
          return;
        }

        // 5. Valid Ticket: Update local record to Used & enqueue into offlineQueue
        ticket.isCheckedIn = true;
        ticket.status = "Used";
        ticket.updatedAt = new Date().toISOString();
        ticketStore.put(ticket);

        const scanId = `OFFLINE_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;
        const scanRecord = {
          scanId,
          ticketCode: actualTicketCode,
          eventId: eventIdNum,
          gateId: currentGateName || "Main Gate",
          scannedAt: new Date().toISOString(),
          scanMethod: "Offline QR Scanner",
          staffUserId,
          customerName: ticket.attendeeName,
          zoneName: ticketZone,
          seatNumber: ticket.seatNumber,
        };

        queueStore.put(scanRecord);

        tx.oncomplete = () => {
          resolve({
            success: true,
            status: "SUCCESS",
            message: "Ticket verified successfully. Entry permitted (Offline record saved).",
            ticketCode: actualTicketCode,
            customerName: ticket.attendeeName,
            zoneName: ticketZone,
            seatNumber: ticket.seatNumber,
            isOffline: true,
            scannedAt: scanRecord.scannedAt,
          });
        };
      };
    });
  },

  // Get list of pending offline scans
  async getOfflineQueue(eventId) {
    const db = await openDb();
    const eventIdNum = Number(eventId);

    return new Promise((resolve, reject) => {
      const tx = db.transaction("offlineQueue", "readonly");
      const store = tx.objectStore("offlineQueue");
      const request = store.getAll();

      request.onsuccess = () => {
        const all = request.result || [];
        const filtered = eventIdNum ? all.filter((s) => s.eventId === eventIdNum) : all;
        resolve(filtered);
      };
      request.onerror = () => reject(request.error);
    });
  },

  // Clear offline queue after successful sync
  async clearOfflineQueue(eventId) {
    const db = await openDb();
    const eventIdNum = Number(eventId);

    return new Promise((resolve, reject) => {
      const tx = db.transaction("offlineQueue", "readwrite");
      const store = tx.objectStore("offlineQueue");
      const req = store.getAll();

      req.onsuccess = () => {
        const records = req.result || [];
        records.forEach((r) => {
          if (!eventIdNum || r.eventId === eventIdNum) {
            store.delete(r.scanId);
          }
        });
        tx.oncomplete = () => resolve(true);
      };
      req.onerror = () => reject(req.error);
    });
  },

  // Clear cache for an event
  async clearEventCache(eventId) {
    const db = await openDb();
    const eventIdNum = Number(eventId);

    return new Promise((resolve, reject) => {
      const tx = db.transaction(["cachedTickets", "packageMeta", "offlineQueue"], "readwrite");
      tx.objectStore("packageMeta").delete(eventIdNum);
      const ticketStore = tx.objectStore("cachedTickets");
      const index = ticketStore.index("eventId");
      const req = index.getAllKeys(eventIdNum);

      req.onsuccess = () => {
        (req.result || []).forEach((k) => ticketStore.delete(k));
        tx.oncomplete = () => resolve(true);
      };
      req.onerror = () => reject(req.error);
    });
  },
};

export default offlineCheckinDb;
