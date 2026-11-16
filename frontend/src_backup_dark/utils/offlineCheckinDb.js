// ==========================================
// IndexedDB Storage Layer for Offline Check-in (UC_14.5 & UC_14.6)
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
        exportedAt: packageData.exportedAt,
        totalTickets: packageData.totalTickets,
        downloadedAt: new Date().toISOString(),
        signature: packageData.signature || "",
      });

      // Save each ticket
      if (Array.isArray(packageData.tickets)) {
        packageData.tickets.forEach((t) => {
          const tCode = (t.ticketCode || "").toUpperCase();
          const qToken = (t.qrToken || "").toUpperCase();
          const rec = {
            ticketCode: tCode,
            qrToken: qToken,
            eventId: eventIdNum,
            attendeeName: t.attendeeName,
            zoneName: t.zoneName,
            seatNumber: t.seatNumber,
            assignedGate: t.assignedGate,
            isCheckedIn: t.isCheckedIn || t.status === "Used",
            status: t.status || (t.isCheckedIn ? "Used" : "Available"),
            ticketTokenHash: t.ticketTokenHash,
            updatedAt: new Date().toISOString(),
          };
          ticketStore.put(rec);
          // If a dynamic QR token exists, also store an alias record keyed by qrToken
          if (qToken && qToken !== tCode) {
            ticketStore.put({ ...rec, ticketCode: qToken, primaryTicketCode: tCode });
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
  async verifyOfflineTicket(eventId, rawPayload, gateId = "GATE_A", staffUserId = 1) {
    const db = await openDb();
    const eventIdNum = Number(eventId);
    const normalizedCode = (rawPayload || "").trim().toUpperCase();

    return new Promise((resolve, reject) => {
      const tx = db.transaction(["cachedTickets", "offlineQueue"], "readwrite");
      const ticketStore = tx.objectStore("cachedTickets");
      const queueStore = tx.objectStore("offlineQueue");

      const getReq = ticketStore.get(normalizedCode);

      getReq.onsuccess = () => {
        let ticket = getReq.result;

        // If not found by direct key, search all cached tickets for dynamic qrToken or token hash
        if (!ticket) {
          const allReq = ticketStore.getAll();
          allReq.onsuccess = () => {
            const list = allReq.result || [];
            ticket = list.find(
              (t) =>
                t.eventId === eventIdNum &&
                ((t.qrToken && t.qrToken === normalizedCode) ||
                 (t.ticketCode && t.ticketCode === normalizedCode) ||
                 (t.primaryTicketCode && t.primaryTicketCode === normalizedCode) ||
                 (t.ticketTokenHash && t.ticketTokenHash.toUpperCase() === normalizedCode))
            );
            proceedValidation(ticket);
          };
          allReq.onerror = () => proceedValidation(null);
          return;
        }

        proceedValidation(ticket);
      };

      const proceedValidation = (ticket) => {
        // 1. Ticket not found in local package
        if (!ticket || ticket.eventId !== eventIdNum) {
          resolve({
            success: false,
            status: "INVALID_QR",
            message: "INVALID_QR: Ticket code or Dynamic QR token not found in offline ticket package for this event.",
            ticketCode: normalizedCode,
            customerName: "N/A",
            zoneName: "N/A",
            isOffline: true,
          });
          return;
        }

        // 2. Ticket already used / checked-in
        if (ticket.isCheckedIn || ticket.status === "Used") {
          resolve({
            success: false,
            status: "ALREADY_USED",
            message: "ALREADY_USED: Ticket has already been scanned/used previously.",
            ticketCode: ticket.primaryTicketCode || ticket.ticketCode,
            customerName: ticket.attendeeName,
            zoneName: ticket.zoneName,
            seatNumber: ticket.seatNumber,
            isOffline: true,
          });
          return;
        }

        // 3. Ticket revoked
        if (ticket.status === "Revoked" || ticket.status === "REVOKED") {
          resolve({
            success: false,
            status: "TICKET_REVOKED",
            message: "TICKET_REVOKED: This ticket has been revoked or invalidated.",
            ticketCode: ticket.primaryTicketCode || ticket.ticketCode,
            customerName: ticket.attendeeName,
            zoneName: ticket.zoneName,
            isOffline: true,
          });
          return;
        }

        // 4. Valid Ticket: Update local record to Used & enqueue into offlineQueue
        const actualTicketCode = ticket.primaryTicketCode || ticket.ticketCode;
        ticket.isCheckedIn = true;
        ticket.status = "Used";
        ticket.updatedAt = new Date().toISOString();
        ticketStore.put(ticket);

        const scanId = `OFFLINE_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`;
        const scanRecord = {
          scanId,
          ticketCode: actualTicketCode,
          eventId: eventIdNum,
          gateId,
          scannedAt: new Date().toISOString(),
          scanMethod: "Offline QR Scanner",
          staffUserId,
          customerName: ticket.attendeeName,
          zoneName: ticket.zoneName,
          seatNumber: ticket.seatNumber,
        };

        queueStore.put(scanRecord);

        tx.oncomplete = () => {
          resolve({
            success: true,
            status: "SUCCESS",
            message: "Offline check-in SUCCESSFUL (Saved to local cache).",
            ticketCode: actualTicketCode,
            customerName: ticket.attendeeName,
            zoneName: ticket.zoneName,
            seatNumber: ticket.seatNumber,
            isOffline: true,
            scannedAt: scanRecord.scannedAt,
          });
        };
      };

      getReq.onerror = () => reject(getReq.error);
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
