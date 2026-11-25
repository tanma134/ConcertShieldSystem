import { useEffect, useState, useRef, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import jsQR from "jsqr";
import { BrowserMultiFormatReader, BrowserQRCodeReader } from "@zxing/library";
import checkinApi from "../../api/checkinApi";
import staffApi from "../../api/staffApi";
import { useAuth } from "../../context/AuthContext";
import offlineCheckinDb from "../../utils/offlineCheckinDb";
import OfflineConflictModal from "../../components/OfflineConflictModal";
import "./StaffCheckinTerminalPage.css";

export default function StaffCheckinTerminalPage() {
  const { eventId } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [assignedEvents, setAssignedEvents] = useState([]);
  const currentEventId = eventId ? Number(eventId) : (assignedEvents.length > 0 ? assignedEvents[0].eventId : null);
  const [activeTab, setActiveTab] = useState("SCAN"); // "SCAN", "MANUAL", "LOOKUP", "HISTORY", "OFFLINE"

  // Gate Assignment & Shift Info
  const [gateAssignment, setGateAssignment] = useState(null);

  // Scanner Input & WebRTC Camera State
  const [ticketInput, setTicketInput] = useState("");
  const [cameraActive, setCameraActive] = useState(true);
  const [cameraError, setCameraError] = useState("");
  const [scanResult, setScanResult] = useState(null);
  const [activeScanAlert, setActiveScanAlert] = useState(null);
  const alertTimerRef = useRef(null);
  const [scanning, setScanning] = useState(false);
  const [autoScannedFlash, setAutoScannedFlash] = useState(false);
  const [videoDevices, setVideoDevices] = useState([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState("");

  const triggerScanAlert = (res) => {
    if (alertTimerRef.current) clearTimeout(alertTimerRef.current);
    setActiveScanAlert(res);
    alertTimerRef.current = setTimeout(() => {
      setActiveScanAlert(null);
    }, 5000);
  };

  const videoRef = useRef(null);
  const mediaStreamRef = useRef(null);
  const canvasRef = useRef(document.createElement("canvas"));
  const scanAnimFrameRef = useRef(null);
  const barcodeDetectorRef = useRef(null);
  const zxingReaderRef = useRef(null);
  const fallbackIntervalRef = useRef(null);
  const lastScannedQrRef = useRef({ code: "", timestamp: 0 });
  const scanCooldownUntilRef = useRef(0);
  const [isCooldownActive, setIsCooldownActive] = useState(false);

  // Manual Check-in Modal State
  const [showManualModal, setShowManualModal] = useState(false);
  const [manualLookupTickets, setManualLookupTickets] = useState([]);
  const [searchingManualTickets, setSearchingManualTickets] = useState(false);
  const [selectedManualTicket, setSelectedManualTicket] = useState(null);
  const [manualLookupSearched, setManualLookupSearched] = useState(false);
  const [manualForm, setManualForm] = useState({
    ticketCode: "",
    cccdNumber: "",
    reasonCode: "DEAD_BATTERY",
    reasonNote: "",
    cccdVerified: false,
  });

  // Ticket Lookup Modal State (Inputs empty by default)
  const [showLookupModal, setShowLookupModal] = useState(false);
  const [lookupSearch, setLookupSearch] = useState({
    searchType: "PHONE",
    queryValue: "", // Empty string by default
  });
  const [lookupResults, setLookupResults] = useState(null);
  const [searchingLookup, setSearchingLookup] = useState(false);

  // Real-time Scan Feed & Scan History
  const [scanFeed, setScanFeed] = useState([]);
  const [historyFilter, setHistoryFilter] = useState("ALL");
  const [historyStats, setHistoryStats] = useState({
    totalScans: 0,
    approvedScans: 0,
    alreadyUsedScans: 0,
    invalidScans: 0,
    successRate: 100,
  });

  // Offline Check-in Mode State
  const [isOfflineMode, setIsOfflineMode] = useState(false);
  const [isNetworkOnline, setIsNetworkOnline] = useState(navigator.onLine);
  const [offlinePackageMeta, setOfflinePackageMeta] = useState(null);
  const [offlineQueueCount, setOfflineQueueCount] = useState(0);
  const [downloadingPackage, setDownloadingPackage] = useState(false);
  const [syncingOffline, setSyncingOffline] = useState(false);
  const [syncNotice, setSyncNotice] = useState(null);
  const [terminalDialog, setTerminalDialog] = useState(null); // { type, title, message, syncDetails, onConfirm }

  const [showConflictModal, setShowConflictModal] = useState(false);
  const [pendingConflictsCount, setPendingConflictsCount] = useState(0);
  const manualInputRef = useRef(null);
  const syncingOfflineRef = useRef(false);

  const checkConflicts = useCallback(async () => {
    try {
      const res = await checkinApi.getOfflineConflicts({ eventId: Number(currentEventId), status: "PENDING" });
      if (res?.data?.data) {
        setPendingConflictsCount(res.data.data.length);
      }
    } catch (e) {
      // ignore
    }
  }, [currentEventId]);

  useEffect(() => {
    loadAssignedEvents();
  }, [user?.userId, user?.id]);

  const loadAssignedEvents = async () => {
    try {
      const staffId = user?.userId ?? user?.id ?? user?.UserId ?? 0;
      const res = await staffApi.getMyAssignments(staffId);
      const list = res.data?.data || [];
      setAssignedEvents(list);

      // If currently on an event that is not in the assigned list, automatically switch to first assigned event
      if (list.length > 0) {
        const isCurrentAssigned = list.some((e) => e.eventId === Number(currentEventId));
        if (!isCurrentAssigned && !eventId) {
          navigate(`/staff/checkin/${list[0].eventId}`, { replace: true });
        }
      }
    } catch (err) {
      console.warn("Could not load staff assigned events in terminal:", err);
    }
  };

  // Load Gate Assignment, History, and Auto-preload Offline Package on Mount
  useEffect(() => {
    if (!currentEventId) return;
    fetchGateAssignment();
    fetchScanHistory();
    autoPreloadOfflinePackage(currentEventId);
    checkConflicts();

    const handleOnline = async () => {
      setIsNetworkOnline(true);
      setIsOfflineMode(false); // Seamlessly switch back to online mode
      await refreshOfflineQueueCount();
      await handleSyncOfflineScans(true); // Automatically sync offline queue on reconnection
    };
    const handleOffline = () => {
      setIsNetworkOnline(false);
      setIsOfflineMode(true); // Automatically switch to offline mode on connection loss
    };

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    // Initial sync check if network is already online
    if (navigator.onLine) {
      handleSyncOfflineScans(true);
    }

    // Periodic heartbeat auto-sync every 15 seconds if items are queued
    const syncInterval = setInterval(() => {
      if (navigator.onLine) {
        handleSyncOfflineScans(true);
      }
    }, 15000);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
      clearInterval(syncInterval);
    };
  }, [currentEventId]);

  // Always auto-preload and keep offline ticket package ready in IndexedDB
  const autoPreloadOfflinePackage = async (evtId) => {
    setDownloadingPackage(true);
    try {
      // Check existing cached package in IndexedDB
      const meta = await offlineCheckinDb.getPackageMeta(evtId);
      if (meta) {
        setOfflinePackageMeta(meta);
      }

      // Fetch fresh package from server in background if network is available
      if (navigator.onLine) {
        const res = await checkinApi.getOfflinePackage(evtId, gateAssignment?.gateName || "");
        if (res?.data?.data) {
          const pkg = res.data.data;
          await offlineCheckinDb.saveOfflinePackage(evtId, pkg);
          const updatedMeta = await offlineCheckinDb.getPackageMeta(evtId);
          setOfflinePackageMeta(updatedMeta);
        }
      }
      await refreshOfflineQueueCount();
    } catch (err) {
      console.warn("Background auto-preload offline error (relying on cached IndexedDB):", err);
      const meta = await offlineCheckinDb.getPackageMeta(evtId);
      if (meta) setOfflinePackageMeta(meta);
    } finally {
      setDownloadingPackage(false);
    }
  };

  const refreshOfflineStatus = async () => {
    try {
      const meta = await offlineCheckinDb.getPackageMeta(currentEventId);
      setOfflinePackageMeta(meta);
      await refreshOfflineQueueCount();
    } catch (err) {
      console.warn("Could not check offline status:", err);
    }
  };

  const refreshOfflineQueueCount = async () => {
    try {
      const queue = await offlineCheckinDb.getOfflineQueue(currentEventId);
      setOfflineQueueCount(queue.length);
    } catch (err) {
      console.warn("Could not refresh offline queue count:", err);
    }
  };

  // Manual re-download / refresh offline ticket package if desired
  const handleDownloadOfflinePackage = async () => {
    setDownloadingPackage(true);
    try {
      const res = await checkinApi.getOfflinePackage(currentEventId, gateAssignment?.gateName || "");
      if (res?.data?.data) {
        const pkg = res.data.data;
        await offlineCheckinDb.saveOfflinePackage(currentEventId, pkg);
        await refreshOfflineStatus();
        const gateText = pkg.assignedGate ? `for Gate '${pkg.assignedGate}'` : "";
        const typesText = pkg.allowedTicketTypes && pkg.allowedTicketTypes.length > 0
          ? ` (Only admits: ${pkg.allowedTicketTypes.join(", ")})`
          : "";
        setTerminalDialog({
          type: "SUCCESS",
          title: "Offline Package Downloaded",
          message: `Downloaded ${pkg.totalTickets} tickets ${gateText}${typesText} to device IndexedDB. Ready for offline scanning at designated gate!`,
        });
      }
    } catch (err) {
      console.error("Failed to preload offline package:", err);
      setTerminalDialog({
        type: "ERROR",
        title: "Failed to Preload Ticket Package",
        message: err.response?.data?.message || err.message || "Could not download offline ticket package from server.",
      });
    } finally {
      setDownloadingPackage(false);
    }
  };

  // Sync Offline Scans to Backend (Supports both manual click & automatic background sync)
  const handleSyncOfflineScans = async (isSilent = false) => {
    if (syncingOfflineRef.current) return;
    if (!navigator.onLine) {
      if (!isSilent) {
        setTerminalDialog({
          type: "WARNING",
          title: "No Internet Connection",
          message: "Device is currently offline. Scan records will automatically synchronize when network is restored.",
        });
      }
      return;
    }

    syncingOfflineRef.current = true;
    setSyncingOffline(true);
    try {
      const queue = await offlineCheckinDb.getOfflineQueue(currentEventId);
      if (!queue || queue.length === 0) {
        if (!isSilent) {
          setTerminalDialog({
            type: "WARNING",
            title: "No Pending Scans in Queue",
            message: "There are no pending offline check-in records waiting to synchronize on this device.",
          });
        }
        setSyncingOffline(false);
        syncingOfflineRef.current = false;
        return;
      }

      const payload = {
        eventId: Number(currentEventId),
        gateId: gateAssignment?.gateId || "GATE_A",
        scans: queue.map((s) => ({
          ticketCode: s.ticketCode,
          gateId: s.gateId || gateAssignment?.gateId || "GATE_A",
          scannedAt: s.scannedAt,
          scanMethod: s.scanMethod,
          staffUserId: user?.id || 1,
        })),
      };

      const res = await checkinApi.syncOfflineScans(payload);
      if (res?.data?.data) {
        const syncResult = res.data.data;
        await offlineCheckinDb.clearOfflineQueue(currentEventId);
        await refreshOfflineQueueCount();
        fetchScanHistory();
        checkConflicts();

        if (isSilent) {
          setSyncNotice({
            text: `Successfully auto-synchronized ${syncResult.totalProcessed || queue.length} offline scans to server!`,
          });
          setTimeout(() => setSyncNotice(null), 5000);
        } else {
          setTerminalDialog({
            type: "SYNC_RESULT",
            title: "Offline Scans Synchronized Successfully",
            message: syncResult.message || `Successfully synced ${syncResult.totalProcessed} offline scans to the server.`,
            syncDetails: syncResult,
          });
        }
      }
    } catch (err) {
      console.error("Sync error:", err);
      if (!isSilent) {
        setTerminalDialog({
          type: "ERROR",
          title: "Sync Error",
          message: "Could not connect to server to synchronize: " + (err.response?.data?.message || err.message),
        });
      }
    } finally {
      syncingOfflineRef.current = false;
      setSyncingOffline(false);
    }
  };

  // Audio Feedback Synthesizer
  const playAudioFeedback = (type) => {
    try {
      const ctx = new (window.AudioContext || window.webkitAudioContext)();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);

      if (type === "SUCCESS") {
        osc.type = "sine";
        osc.frequency.setValueAtTime(880, ctx.currentTime);
        gain.gain.setValueAtTime(0.3, ctx.currentTime);
        osc.start();
        osc.stop(ctx.currentTime + 0.2);
      } else {
        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(220, ctx.currentTime);
        gain.gain.setValueAtTime(0.4, ctx.currentTime);
        osc.start();
        osc.stop(ctx.currentTime + 0.5);
      }
    } catch (e) {}
  };

  // Execute Offline Scan against local IndexedDB
  const executeOfflineScan = async (rawCode, isAutoFallback = false) => {
    try {
      const offlineResult = await offlineCheckinDb.verifyOfflineTicket(
        Number(currentEventId),
        rawCode.trim(),
        gateAssignment?.gateName || gateAssignment?.gateId || "Main Gate",
        user?.id || 1
      );

      const serverStatusPrefix = !navigator.onLine
        ? "[Network Disconnected - Offline Mode]"
        : "[Server Unreachable - Auto Offline Mode] (CheckinAPI backend not responding on localhost).";

      const offObj = {
        ...offlineResult,
        isOffline: true,
        message: isAutoFallback
          ? `${serverStatusPrefix} ${offlineResult.message}`
          : offlineResult.message,
      };
      setScanResult(offObj);
      triggerScanAlert(offObj);
      playAudioFeedback(offlineResult.status === "SUCCESS" ? "SUCCESS" : "ERROR");

      setScanFeed((prev) => [
        {
          scanId: Math.random().toString(36).substring(2, 9),
          ticketCode: offlineResult.ticketCode || rawCode,
          customerName: offlineResult.customerName || "Attendee",
          zoneName: offlineResult.zoneName || "General Admission",
          orderId: offlineResult.orderId || "N/A",
          gateName: gateAssignment?.gateName || "Gate A",
          scannedAt: offlineResult.scannedAt || new Date().toISOString(),
          scanMethod: isAutoFallback ? "Server Offline Fallback (IndexedDB)" : "Offline QR Scan (IndexedDB)",
          status: offlineResult.status,
        },
        ...prev,
      ]);

      await refreshOfflineQueueCount();
    } catch (offlineErr) {
      console.error("Offline scan error:", offlineErr);
      const errResult = {
        success: false,
        status: "INVALID_QR",
        message: "Local IndexedDB storage error during offline check-in: " + offlineErr.message,
      };
      setScanResult(errResult);
      triggerScanAlert(errResult);
      playAudioFeedback("ERROR");
    } finally {
      setScanning(false);
      setTicketInput("");
      if (manualInputRef.current) manualInputRef.current.focus();
    }
  };

  // Submit Scan Request (Camera or HID Scanner)
  const handleProcessScan = useCallback(
    async (codeToScan) => {
      const payload = codeToScan || ticketInput;
      if (!payload || !payload.trim()) return;

      // Activate 2-second scanner cooldown pause after scanning any QR
      scanCooldownUntilRef.current = Date.now() + 2000;
      setIsCooldownActive(true);
      setTimeout(() => setIsCooldownActive(false), 2000);

      setScanning(true);
      setScanResult(null);

      // If in offline mode OR device has no internet connection, verify locally immediately
      if (isOfflineMode || !navigator.onLine) {
        await executeOfflineScan(payload, false);
        return;
      }

      // Normal Online Mode Processing
      try {
        const res = await checkinApi.scanQrTicket({
          eventId: Number(currentEventId),
          gateId: gateAssignment?.gateId || "GATE_A",
          qrPayload: payload.trim(),
        });

        if (res && res.data && res.data.data) {
          const result = res.data.data;
          const isSuccess = result.success || result.status === "SUCCESS" || result.status === "APPROVED";
          const normStatus = isSuccess
            ? "SUCCESS"
            : result.status === "ALREADY_USED" || result.status === "ALREADY_CHECKED_IN"
            ? "ALREADY_USED"
            : "INVALID_QR";

          result.status = normStatus;
          setScanResult(result);
          triggerScanAlert(result);
          playAudioFeedback(normStatus === "SUCCESS" ? "SUCCESS" : "ERROR");

          setScanFeed((prev) => [
            {
              scanId: Math.random().toString(36).substring(2, 9),
              ticketCode: result.ticketCode || payload,
              customerName: result.customerName || "Attendee",
              zoneName: result.zoneName || "General Admission",
              orderId: result.orderId || "N/A",
              gateName: gateAssignment?.gateName || "Gate A",
              scannedAt: new Date().toISOString(),
              scanMethod: "Dynamic QR Code",
              status: normStatus,
            },
            ...prev,
          ]);

          fetchScanHistory();
        }
      } catch (err) {
        console.error("Scan error:", err);
        const isNetworkErr = !navigator.onLine || !err.response || err.code === "ERR_NETWORK" || err.message?.includes("Network Error");
        if (isNetworkErr) {
          // SEAMLESS AUTO-FALLBACK: Never interrupt attendee flow upon network drop!
          console.warn("Network lost during online scan. Auto-falling back to offline check-in mode!");
          setIsOfflineMode(true);
          await executeOfflineScan(payload, true /* isAutoFallback */);
          return;
        }

        const errResult = {
          success: false,
          status: "INVALID_QR",
          message: err.response?.data?.message || "Invalid QR code payload or server error.",
        };
        setScanResult(errResult);
        triggerScanAlert(errResult);
        playAudioFeedback("ERROR");
      } finally {
        setScanning(false);
        setTicketInput("");
        if (manualInputRef.current) manualInputRef.current.focus();
      }
    },
    [currentEventId, gateAssignment, ticketInput, isOfflineMode, user]
  );

  const stopWebcam = useCallback(() => {
    if (fallbackIntervalRef.current) {
      clearInterval(fallbackIntervalRef.current);
      fallbackIntervalRef.current = null;
    }
    if (scanAnimFrameRef.current) {
      cancelAnimationFrame(scanAnimFrameRef.current);
      scanAnimFrameRef.current = null;
    }
    if (zxingReaderRef.current) {
      try {
        zxingReaderRef.current.reset();
      } catch (e) {}
    }
    if (mediaStreamRef.current) {
      try {
        mediaStreamRef.current.getTracks().forEach((track) => track.stop());
      } catch (e) {}
      mediaStreamRef.current = null;
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
  }, []);

  // Enumerate camera input devices on mount
  useEffect(() => {
    try {
      const reader = new BrowserMultiFormatReader();
      reader
        .listVideoInputDevices()
        .then((devices) => {
          if (devices && devices.length > 0) {
            setVideoDevices(devices);
            const backCam = devices.find((d) =>
              d.label?.toLowerCase().includes("back") ||
              d.label?.toLowerCase().includes("environment") ||
              d.label?.toLowerCase().includes("rear")
            );
            setSelectedDeviceId(backCam ? backCam.deviceId : devices[0].deviceId);
          }
        })
        .catch((e) => {
          console.warn("Could not list video devices:", e);
        });
    } catch (e) {}
  }, []);

  const cleanEventTitle = (title) => {
    if (!title) return "";
    return title
      .replace(/\s*-\s*offline\s*sync\s*demo/gi, "")
      .replace(/\s*offline\s*sync\s*demo/gi, "")
      .replace(/\s*offlinesync\s*demo/gi, "")
      .replace(/^Event\s*#\s*\d+\s*:\s*/i, "")
      .replace(/^Event\s*#\s*/i, "Event ")
      .replace(/^Concert\s*#\s*\d+\s*:\s*/i, "")
      .replace(/^Concert\s*#\s*/i, "Concert ")
      .trim();
  };

  // Real-time Dual-Engine QR Scanner (ZXing + Native BarcodeDetector + jsQR)
  useEffect(() => {
    let active = true;

    async function startScanner() {
      if (activeTab !== "SCAN" || !cameraActive) {
        stopWebcam();
        return;
      }

      setCameraError("");
      stopWebcam();

      try {
        const codeReader = new BrowserQRCodeReader();
        zxingReaderRef.current = codeReader;

        const deviceId = selectedDeviceId || undefined;

        // Primary: ZXing continuous hardware/webcam decoder
        await codeReader.decodeFromVideoDevice(
          deviceId,
          videoRef.current,
          (result, err) => {
            if (!active) return;
            if (result) {
              const codeTrimmed = result.getText()?.trim();
              if (!codeTrimmed) return;

              const now = Date.now();
              if (scanCooldownUntilRef.current && now < scanCooldownUntilRef.current) return;
              if (
                lastScannedQrRef.current.code === codeTrimmed &&
                now - lastScannedQrRef.current.timestamp < 2500
              ) {
                return;
              }

              lastScannedQrRef.current = { code: codeTrimmed, timestamp: now };
              console.log("ZXing Live Auto-Detected QR Code:", codeTrimmed);
              setAutoScannedFlash(true);
              setTimeout(() => setAutoScannedFlash(false), 800);
              handleProcessScan(codeTrimmed);
            }
          }
        );

        // Secondary Backup: Throttled Frame Analyzer (200ms) with BarcodeDetector + jsQR
        const canvas = canvasRef.current;
        const ctx = canvas.getContext("2d", { willReadFrequently: true });

        fallbackIntervalRef.current = setInterval(async () => {
          if (!active) return;
          const now = Date.now();
          if (scanCooldownUntilRef.current && now < scanCooldownUntilRef.current) return;

          const video = videoRef.current;
          if (!video || video.readyState < 2 || !video.videoWidth || !video.videoHeight) return;

          try {
            const targetW = video.videoWidth;
            const targetH = video.videoHeight;

            if (canvas.width !== targetW || canvas.height !== targetH) {
              canvas.width = targetW;
              canvas.height = targetH;
            }

            ctx.drawImage(video, 0, 0, targetW, targetH);

            let detected = null;

            // 1. Native BarcodeDetector
            if (typeof window !== "undefined" && "BarcodeDetector" in window) {
              try {
                if (!barcodeDetectorRef.current) {
                  barcodeDetectorRef.current = new window.BarcodeDetector({ formats: ["qr_code"] });
                }
                const barcodes = await barcodeDetectorRef.current.detect(canvas);
                if (barcodes && barcodes.length > 0 && barcodes[0].rawValue) {
                  detected = barcodes[0].rawValue;
                }
              } catch (e) {}
            }

            // 2. jsQR fallback
            if (!detected) {
              try {
                const imgData = ctx.getImageData(0, 0, targetW, targetH);
                const decodeQR = typeof jsQR === "function" ? jsQR : (jsQR?.default || window.jsQR);
                if (decodeQR) {
                  const res = decodeQR(imgData.data, targetW, targetH, { inversionAttempts: "attemptBoth" });
                  if (res && res.data) detected = res.data;
                }
              } catch (e) {}
            }

            if (detected && detected.trim()) {
              const codeTrimmed = detected.trim();
              if (
                lastScannedQrRef.current.code === codeTrimmed &&
                now - lastScannedQrRef.current.timestamp < 2500
              ) {
                return;
              }
              lastScannedQrRef.current = { code: codeTrimmed, timestamp: now };
              console.log("jsQR / BarcodeDetector Auto-Detected QR Code:", codeTrimmed);
              setAutoScannedFlash(true);
              setTimeout(() => setAutoScannedFlash(false), 800);
              handleProcessScan(codeTrimmed);
            }
          } catch (e) {}
        }, 200);

      } catch (err) {
        console.warn("Camera start error:", err);
        setCameraError(
          "Could not activate webcam stream (" + (err.message || "Busy or no permission") + "). You can upload a QR image or use a handheld barcode scanner."
        );
      }
    }

    startScanner();

    return () => {
      active = false;
      stopWebcam();
    };
  }, [activeTab, cameraActive, gateAssignment, selectedDeviceId, handleProcessScan, stopWebcam]);

  // Support physical USB/Bluetooth handheld barcode scanner (auto-detects fast keystrokes + Enter)
  useEffect(() => {
    let buffer = "";
    let lastKeyTime = Date.now();

    const handleKeyDown = (e) => {
      if (
        document.activeElement &&
        (document.activeElement.tagName === "INPUT" ||
          document.activeElement.tagName === "TEXTAREA" ||
          document.activeElement.tagName === "SELECT")
      ) {
        return;
      }

      const now = Date.now();
      if (now - lastKeyTime > 300) {
        buffer = "";
      }
      lastKeyTime = now;

      if (e.key === "Enter") {
        if (buffer.trim().length > 3) {
          e.preventDefault();
          const scanned = buffer.trim();
          buffer = "";
          handleProcessScan(scanned);
        }
      } else if (e.key.length === 1) {
        buffer += e.key;
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [handleProcessScan]);

  const fetchGateAssignment = async () => {
    try {
      const staffId = user?.userId ?? user?.id ?? user?.UserId ?? 0;
      const res = await checkinApi.getGateAssignment(currentEventId, staffId);
      const data = res.data?.data || res.data;
      if (data) {
        setGateAssignment(data);
      }
    } catch (err) {
      console.error("Failed to fetch gate assignment:", err);
    }
  };

  const fetchScanHistory = async (filter = historyFilter) => {
    try {
      const res = await checkinApi.getScanHistory(currentEventId, {
        gateId: gateAssignment?.gateId || "GATE_A",
        statusFilter: filter,
      });
      if (res && res.data && res.data.data) {
        const data = res.data.data;
        setHistoryStats({
          totalScans: data.totalScans,
          approvedScans: data.approvedScans,
          alreadyUsedScans: data.alreadyUsedScans,
          invalidScans: data.invalidScans,
          successRate: data.successRate,
        });
        if (data.scans) {
          setScanFeed(data.scans);
        }
      }
    } catch (err) {
      console.error("Failed to fetch scan history:", err);
    }
  };

  // Search Tickets by CCCD in Manual Mode
  const searchTicketsByCccd = async (cccdVal) => {
    const val = (cccdVal !== undefined ? cccdVal : manualForm.cccdNumber || "").trim();
    if (!val || val.length < 9) {
      setTerminalDialog({
        type: "WARNING",
        title: "Invalid ID / CCCD Number",
        message: "Please enter a valid National ID / CCCD number (9 to 12 digits) to find tickets.",
      });
      return;
    }

    try {
      setSearchingManualTickets(true);
      setManualLookupSearched(true);
      const res = await checkinApi.lookupTickets({
        eventId: Number(currentEventId),
        searchType: "CCCD",
        queryValue: val,
      });

      const list = res?.data?.data?.tickets || [];
      setManualLookupTickets(list);
      const firstAvailable = list.find((t) => t.status === "AVAILABLE") || list[0] || null;
      setSelectedManualTicket(firstAvailable);
      if (firstAvailable) {
        setManualForm((prev) => ({
          ...prev,
          ticketCode: firstAvailable.ticketCode,
        }));
      }
    } catch (err) {
      console.error("Error looking up tickets by CCCD:", err);
      setManualLookupTickets([]);
      setSelectedManualTicket(null);
    } finally {
      setSearchingManualTickets(false);
    }
  };

  // Submit Manual Check-in
  const handleManualCheckinSubmit = async (e) => {
    e.preventDefault();
    if (!manualForm.cccdNumber.trim() || manualForm.cccdNumber.trim().length < 9) {
      setTerminalDialog({
        type: "WARNING",
        title: "Invalid ID / CCCD Number",
        message: "Please enter a valid National ID / Passport number (9 to 12 digits).",
      });
      return;
    }
    if (!manualForm.cccdVerified) {
      setTerminalDialog({
        type: "WARNING",
        title: "Physical Identity Verification Required",
        message: "Physical inspection of attendee's face against original ID card photo is required before confirming!",
      });
      return;
    }

    try {
      setScanning(true);
      const tCode = selectedManualTicket?.ticketCode || manualForm.ticketCode.trim();
      const res = await checkinApi.manualCheckin({
        eventId: Number(currentEventId),
        gateId: gateAssignment?.gateId || "GATE_A",
        ticketCode: tCode,
        cccdNumber: manualForm.cccdNumber.trim(),
        reasonCode: manualForm.reasonCode,
        reasonNote: manualForm.reasonNote,
        cccdVerified: manualForm.cccdVerified,
      });

      if (res && res.data && res.data.data) {
        const result = res.data.data;
        if (!result.success && result.message) {
          setTerminalDialog({
            type: "ERROR",
            title: "Manual Check-in Failed",
            message: result.message,
          });
          return;
        }
        setScanResult(result);
        playAudioFeedback(result.status === "SUCCESS" ? "SUCCESS" : "ERROR");
        setShowManualModal(false);
        setManualForm({
          ticketCode: "",
          cccdNumber: "",
          reasonCode: "DEAD_BATTERY",
          reasonNote: "",
          cccdVerified: false,
        });
        setSelectedManualTicket(null);
        setManualLookupTickets([]);
        setManualLookupSearched(false);
        fetchScanHistory();
      }
    } catch (err) {
      setTerminalDialog({
        type: "ERROR",
        title: "Manual Check-in Error",
        message: "Error occurred during manual check-in: " + (err.response?.data?.message || err.message),
      });
    } finally {
      setScanning(false);
    }
  };

  // Submit Lookup Ticket Search
  const handleLookupSearch = async (e) => {
    if (e) e.preventDefault();
    if (!lookupSearch.queryValue.trim()) return;

    try {
      setSearchingLookup(true);
      const res = await checkinApi.lookupTickets({
        eventId: Number(currentEventId),
        searchType: lookupSearch.searchType,
        queryValue: lookupSearch.queryValue.trim(),
      });
      if (res && res.data && res.data.data) {
        setLookupResults(res.data.data);
      }
    } catch (err) {
      console.error("Lookup error:", err);
    } finally {
      setSearchingLookup(false);
    }
  };

  // Quick Check-in from Lookup Result
  const handleLookupCheckinNow = (ticket) => {
    setActiveTab("MANUAL");
    setSelectedManualTicket(ticket);
    setManualLookupTickets([ticket]);
    setManualLookupSearched(true);
    setManualForm({
      ticketCode: ticket.ticketCode,
      cccdNumber: ticket.maskedCccd && ticket.maskedCccd !== "N/A" ? ticket.maskedCccd : "",
      reasonCode: "DEAD_BATTERY",
      reasonNote: `Verified via lookup ${lookupSearch.searchType}`,
      cccdVerified: false,
    });
  };


  const getLookupPlaceholder = () => {
    switch (lookupSearch.searchType) {
      case "PHONE":
        return "Enter Phone Number (e.g. 0987654321)...";
      case "CCCD":
        return "Enter ID / CCCD Number (e.g. 079201008812)...";
      case "EMAIL":
        return "Enter Email Address (e.g. customer@ticketbox.com)...";
      default:
        return "Enter search keywords...";
    }
  };

  return (
    <div className="terminal-container">
      {/* Header Banner */}
      <header className="terminal-header">
        <div className="header-left">
          <button className="btn-back" onClick={() => navigate("/staff/gate-assignment")}>
            Exit Scanner Terminal
          </button>
          <div className="gate-title-block">
            <span className="badge-terminal">GATE ADMISSION CONTROL TERMINAL</span>
            <div className="gate-title-row">
              <h1 className="terminal-event-title">
                {cleanEventTitle(gateAssignment?.eventTitle)
                  || cleanEventTitle(assignedEvents.find((e) => e.eventId === currentEventId)?.eventTitle)
                  || cleanEventTitle(gateAssignment?.eventName)
                  || (currentEventId ? `Event #${currentEventId}` : "No Event Selected")}
              </h1>
              {gateAssignment?.isAssigned && (
                <span className="badge-event-active">Event Active</span>
              )}
            </div>
            <div className="event-switch-row">
              <span className="event-switch-label">Switch Event:</span>
              <select
                value={currentEventId || ""}
                onChange={(e) => navigate(`/staff/checkin/${e.target.value}`)}
                className="event-selector-dark"
                disabled={assignedEvents.length === 0}
              >
                {assignedEvents.length > 0 ? (
                  assignedEvents.map((ev) => (
                    <option key={ev.eventId} value={ev.eventId}>
                      {cleanEventTitle(ev.eventTitle)} ({ev.gateName || "Main Gate"})
                    </option>
                  ))
                ) : (
                  <option value="" disabled>No events assigned yet</option>
                )}
              </select>
            </div>
            <p className="shift-sub">
              {assignedEvents.length > 0 || gateAssignment?.isAssigned || currentEventId ? (
                <>
                  Shift: <strong>{gateAssignment?.shiftStartTime && gateAssignment?.shiftEndTime ? `${gateAssignment.shiftStartTime} - ${gateAssignment.shiftEndTime}` : (assignedEvents.find(e => e.eventId === currentEventId)?.shiftStartTime ? `${assignedEvents.find(e => e.eventId === currentEventId).shiftStartTime} - ${assignedEvents.find(e => e.eventId === currentEventId).shiftEndTime}` : "19:30 - 22:30")}</strong> | Gate: <strong>{gateAssignment?.gateName && gateAssignment.gateName !== "Unassigned" ? gateAssignment.gateName : (assignedEvents.find(e => e.eventId === currentEventId)?.gateName || "Main Gate")}</strong> | Staff: <strong>{user?.fullName || user?.email || "Check-in Staff"}</strong>
                  {((gateAssignment?.allowedTicketTiers && gateAssignment.allowedTicketTiers.length > 0) || (assignedEvents.find(e => e.eventId === currentEventId)?.allowedTicketTypes && assignedEvents.find(e => e.eventId === currentEventId).allowedTicketTypes.length > 0)) && (
                    <span style={{ marginLeft: "8px", color: "#38bdf8" }}>
                      | Permitted: <strong>{(gateAssignment?.allowedTicketTiers?.length > 0 ? gateAssignment.allowedTicketTiers : [assignedEvents.find(e => e.eventId === currentEventId)?.allowedTicketTypes]).join(", ")}</strong>
                    </span>
                  )}
                </>
              ) : (
                <span style={{ color: "#ef4444" }}>No event assigned yet</span>
              )}
            </p>
          </div>
        </div>

        <div className="header-actions">
          <div className="camera-status-pill">
            <span className="pulse-dot"></span>
            <span>Camera Active</span>
          </div>

          {/* Offline Mode Toggle */}
          <button
            type="button"
            className={`btn-mode-toggle ${isOfflineMode ? "mode-offline" : "mode-online"}`}
            onClick={() => {
              const newMode = !isOfflineMode;
              setIsOfflineMode(newMode);
              if (newMode && !offlinePackageMeta) {
                handleDownloadOfflinePackage();
              }
            }}
          >
            {isOfflineMode ? "Offline Mode" : "Online Mode"}
          </button>

          {/* Compact Auto-Sync Indicator in Header */}
          {offlineQueueCount > 0 && (
            <button
              type="button"
              className="btn-sync-header-badge"
              onClick={() => handleSyncOfflineScans(false)}
              disabled={syncingOffline}
              title="Pending offline scans waiting for sync."
            >
              {syncingOffline ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : `Pending Sync (${offlineQueueCount})`}
            </button>
          )}

          <button
            type="button"
            className="btn-action-conflict"
            onClick={() => setShowConflictModal(true)}
            title="Review and resolve offline check-in conflicts"
          >
            Conflicts {pendingConflictsCount > 0 ? `(${pendingConflictsCount})` : ""}
          </button>
        </div>
      </header>

      {/* 5 Main Subtabs Bar */}
      <div className="terminal-nav-tabs">
        <button
          type="button"
          className={`terminal-nav-tab ${activeTab === "SCAN" ? "active" : ""}`}
          onClick={() => setActiveTab("SCAN")}
        >
          Scan Dynamic QR
        </button>
        <button
          type="button"
          className={`terminal-nav-tab ${activeTab === "MANUAL" ? "active" : ""}`}
          onClick={() => setActiveTab("MANUAL")}
        >
          Manual Input
        </button>
        <button
          type="button"
          className={`terminal-nav-tab ${activeTab === "LOOKUP" ? "active" : ""}`}
          onClick={() => setActiveTab("LOOKUP")}
        >
          Lookup Ticket
        </button>
        <button
          type="button"
          className={`terminal-nav-tab ${activeTab === "HISTORY" ? "active" : ""}`}
          onClick={() => {
            setActiveTab("HISTORY");
            fetchScanHistory();
          }}
        >
          Scan History
        </button>
        <button
          type="button"
          className={`terminal-nav-tab ${activeTab === "OFFLINE" ? "active" : ""}`}
          onClick={() => setActiveTab("OFFLINE")}
        >
          Offline & Sync {offlineQueueCount > 0 ? `(${offlineQueueCount})` : ""}
        </button>
      </div>

      {/* Sync Success / Notice Toast */}
      {syncNotice && (
        <div className="offline-sync-toast">
          <span>{syncNotice.text}</span>
          <button type="button" onClick={() => setSyncNotice(null)} className="toast-dismiss">
            Close
          </button>
        </div>
      )}

      {/* Not Assigned or Inactive Shift Warning Banner */}
      {!currentEventId ? (
        <div className="shift-inactive-banner">
          <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
            <div>
              <h4 style={{ margin: "0 0 0.25rem 0", fontSize: "1.15rem" }}>NO EVENT ASSIGNED</h4>
              <p style={{ margin: 0, fontSize: "0.95rem" }}>
                Your staff account is currently not assigned to any event or admission gate. Scanning terminal is locked.
              </p>
            </div>
          </div>
          <button className="btn-secondary" onClick={() => navigate("/staff/gate-assignment")}>
            Return to Gate Assignment
          </button>
        </div>
      ) : null}

      {/* PROMINENT REAL-TIME SCAN NOTIFICATION BANNER */}
      {activeScanAlert && (
        <div
          className={`scan-notification-popup ${
            activeScanAlert.status === "SUCCESS"
              ? "popup-success"
              : activeScanAlert.status === "ALREADY_USED"
              ? "popup-warning"
              : "popup-error"
          }`}
          style={{
            marginBottom: "20px",
            padding: "20px 24px",
            borderRadius: "12px",
            background:
              activeScanAlert.status === "SUCCESS"
                ? "#064e3b"
                : activeScanAlert.status === "ALREADY_USED"
                ? "#78350f"
                : "#7f1d1d",
            border: `2px solid ${
              activeScanAlert.status === "SUCCESS"
                ? "#10b981"
                : activeScanAlert.status === "ALREADY_USED"
                ? "#f59e0b"
                : "#ef4444"
            }`,
            color: "#ffffff",
            boxShadow: "0 10px 25px rgba(0,0,0,0.4)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            gap: "16px",
            flexWrap: "wrap",
          }}
        >
          <div style={{ flex: 1, minWidth: "260px" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "10px", marginBottom: "6px" }}>
              <span
                style={{
                  padding: "4px 12px",
                  borderRadius: "6px",
                  fontSize: "0.85rem",
                  fontWeight: 800,
                  letterSpacing: "0.05em",
                  background:
                    activeScanAlert.status === "SUCCESS"
                      ? "#10b981"
                      : activeScanAlert.status === "ALREADY_USED"
                      ? "#f59e0b"
                      : "#ef4444",
                  color: activeScanAlert.status === "ALREADY_USED" ? "#111827" : "#ffffff",
                }}
              >
                {activeScanAlert.status === "SUCCESS"
                  ? "SUCCESS"
                  : activeScanAlert.status === "ALREADY_USED"
                  ? "ALREADY USED"
                  : "INVALID QR"}
              </span>
              {activeScanAlert.isOffline && (
                <span style={{ fontSize: "0.8rem", padding: "3px 8px", background: "rgba(255,255,255,0.2)", borderRadius: "4px" }}>
                  Offline Mode
                </span>
              )}
            </div>

            <div style={{ fontSize: "1.15rem", fontWeight: 700, marginBottom: "6px" }}>
              {activeScanAlert.status === "SUCCESS"
                ? "Ticket verified successfully. Admission permitted!"
                : activeScanAlert.status === "ALREADY_USED"
                ? "This ticket has already been checked in. (Already Used)"
                : (activeScanAlert.message || "Invalid QR code. Admission rejected!")}
            </div>

            <div style={{ display: "flex", gap: "16px", flexWrap: "wrap", fontSize: "0.9rem", color: "#e2e8f0" }}>
              {activeScanAlert.customerName && activeScanAlert.customerName !== "N/A" && (
                <span>Attendee: <strong style={{ color: "#fff" }}>{activeScanAlert.customerName}</strong></span>
              )}
              {activeScanAlert.zoneName && activeScanAlert.zoneName !== "N/A" && (
                <span>Ticket Type: <strong style={{ color: "#fff" }}>{activeScanAlert.zoneName}</strong></span>
              )}
              {activeScanAlert.orderId && activeScanAlert.orderId !== "N/A" && (
                <span>Order ID: <strong style={{ color: "#fff" }}>#{activeScanAlert.orderId}</strong></span>
              )}
              <span>Gate: <strong style={{ color: "#fff" }}>{gateAssignment?.gateName || "Gate A"}</strong></span>
            </div>

            {activeScanAlert.status === "ALREADY_USED" && activeScanAlert.previousScanTime && (
              <div style={{ marginTop: "6px", fontSize: "0.85rem", color: "#fef08a" }}>
                Previously admitted at {new Date(activeScanAlert.previousScanTime).toLocaleTimeString()} at {activeScanAlert.previousGateName || "Gate A"}.
              </div>
            )}
          </div>

          <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
            {activeScanAlert.status === "ALREADY_USED" && (
              <button
                type="button"
                onClick={() => setShowConflictModal(true)}
                style={{
                  background: "#f59e0b",
                  color: "#111827",
                  border: "none",
                  borderRadius: "8px",
                  padding: "10px 18px",
                  fontWeight: 700,
                  cursor: "pointer",
                  fontSize: "0.9rem",
                }}
              >
                View Duplicate Details
              </button>
            )}
            <button
              type="button"
              onClick={() => setActiveScanAlert(null)}
              style={{
                background: "rgba(255,255,255,0.2)",
                color: "#ffffff",
                border: "1px solid rgba(255,255,255,0.4)",
                borderRadius: "8px",
                padding: "10px 18px",
                fontWeight: 700,
                cursor: "pointer",
                fontSize: "0.9rem",
              }}
            >
              Dismiss
            </button>
          </div>
        </div>
      )}

      {/* TAB 1: Scan Dynamic QR (Figure 114) */}
      <div
        className="terminal-scan-tab"
        style={{ display: activeTab === "SCAN" ? "block" : "none" }}
      >
        {/* Camera Viewfinder */}
          <div className="scanner-viewfinder-card">
            <div className="viewfinder-header">
              <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                <span style={{ fontWeight: 700, fontSize: "1.1rem" }}>Camera QR Scanner</span>
              </div>
              <div className="viewfinder-tools">
                {videoDevices.length > 1 && (
                  <select
                    className="camera-device-select"
                    value={selectedDeviceId}
                    onChange={(e) => setSelectedDeviceId(e.target.value)}
                    title="Select Camera Input Device"
                  >
                    {videoDevices.map((d, idx) => (
                      <option key={d.deviceId || idx} value={d.deviceId}>
                        {d.label || `Camera ${idx + 1}`}
                      </option>
                    ))}
                  </select>
                )}
                <button
                  className="btn-toggle-cam"
                  onClick={() => setCameraActive(!cameraActive)}
                >
                  {cameraActive ? "Pause Camera" : "Activate Camera"}
                </button>
              </div>
            </div>

            <div className={`camera-box ${cameraActive ? "active" : "paused"} ${autoScannedFlash ? "flash-detected" : ""}`}>
              {cameraActive ? (
                <>
                  <video ref={videoRef} autoPlay playsInline muted className="webcam-video-stream" />
                  <div className="scanner-laser"></div>
                  <div className={`scanner-frame ${autoScannedFlash ? "detected" : ""}`}></div>
                  {isCooldownActive ? (
                    <div className="cooldown-overlay-banner">
                      <span className="app-loading-spinner" role="status" aria-label="Loading" />
                    </div>
                  ) : (
                    <p className="scanner-hint">Point attendee QR code at camera to scan automatically</p>
                  )}
                </>
              ) : (
                <div className="camera-off-msg">
                  <span>Camera Paused</span>
                  <p>Click Activate Camera, upload a QR image, or use a handheld barcode scanner</p>
                </div>
              )}
            </div>

            {cameraError && (
              <div className="camera-warning-banner">
                {cameraError}
              </div>
            )}
          </div>

          {/* Visual Scan Feedback Banner */}
          {scanResult && (
            <div
              className={`scan-feedback-card ${
                scanResult.status === "SUCCESS"
                  ? "feedback-success"
                  : scanResult.status === "ALREADY_USED"
                  ? "feedback-warning"
                  : "feedback-error"
              }`}
            >
              <div className="feedback-body">
                <h2>{scanResult.status === "SUCCESS" ? "ENTRY PERMITTED" : scanResult.status}</h2>
                <p className="feedback-msg">{scanResult.message}</p>

                {scanResult.customerName && (
                  <div className="feedback-details">
                    <span>Attendee: <strong>{scanResult.customerName}</strong></span>
                    <span>Zone: <strong>{scanResult.zoneName}</strong></span>
                    <span>Seat: <strong>{scanResult.seatNumber || "Standing"}</strong></span>
                  </div>
                )}

                {scanResult.status === "ALREADY_USED" && scanResult.previousScanTime && (
                  <div className="already-used-audit">
                    <strong>Previous Audit Entry:</strong> Checked-in at{" "}
                    {new Date(scanResult.previousScanTime).toLocaleTimeString()} at{" "}
                    {scanResult.previousGateName} by Staff {scanResult.previousStaffName}.
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Real-time Admission Scan Feed Table Underneath (Figure 114) */}
          <div className="scan-feed-section">
            <div className="feed-header-row">
              <h3>Real-time Admission Scan Feed</h3>
              <div style={{ display: "flex", gap: "0.5rem" }}>
                <button
                  type="button"
                  className="btn-history-view-all"
                  onClick={() => setActiveTab("HISTORY")}
                >
                  View Full History ({historyStats.totalScans ?? 0})
                </button>
              </div>
            </div>

            <div className="table-responsive">
              <table className="terminal-table">
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Ticket Code</th>
                    <th>Attendee</th>
                    <th>Ticket Type</th>
                    <th>Gate</th>
                    <th>Method</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {scanFeed.length === 0 ? (
                    <tr>
                      <td colSpan="7" style={{ textAlign: "center", padding: "2rem", color: "#6b6480" }}>
                        No admissions recorded yet for this shift.
                      </td>
                    </tr>
                  ) : (
                    scanFeed.slice(0, 10).map((item, idx) => (
                      <tr key={item.scanId || idx}>
                        <td style={{ color: "#7a7392", fontFamily: "monospace" }}>
                          {new Date(item.scannedAt).toLocaleTimeString()}
                        </td>
                        <td>
                          <strong style={{ color: "#1e1b2e", fontFamily: "monospace" }}>{item.ticketCode}</strong>
                        </td>
                        <td>
                          <strong style={{ color: "#1e1b2e" }}>{item.customerName || "N/A"}</strong>
                        </td>
                        <td>
                          <span style={{ color: "#2fa4e0", fontWeight: 600 }}>{item.zoneName || "General Admission"}</span>
                        </td>
                        <td>
                          <span style={{ color: "#7a7392" }}>{item.gateName || gateAssignment?.gateName || "Gate A"}</span>
                        </td>
                        <td>
                          <span className="method-tag-dark">{item.scanMethod || "Dynamic QR"}</span>
                        </td>
                        <td>
                          <span className={`status-badge-pill ${item.status?.toLowerCase()}`}>
                            {item.status === "SUCCESS" ? "Approved" : item.status}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>

      {/* TAB 2: Manual / Hardware Scanner Input (Figure 115) */}
      {activeTab === "MANUAL" && (
        <div className="terminal-manual-tab">
          <div className="card-manual-input">
            <div className="manual-card-header">
              <h2>Manual / Hardware Scanner Input</h2>
              <p className="card-subtext">
                For attendees with dead phone batteries, damaged screens, or direct handheld hardware scanner input.
              </p>
            </div>

            {/* Prominent Physical Verification Notice */}
            <div className="staff-face-notice">
              <div className="notice-content">
                <strong>MANDATORY PHYSICAL CHECK:</strong>
                <p>Check-in staff MUST physically match attendee's <strong>REAL FACE</strong> against portrait photo on their <strong>ORIGINAL ID / CCCD CARD</strong> before checking in.</p>
              </div>
            </div>

            {/* Manual Check-in Form: Enter CCCD to find and select tickets */}
            <form onSubmit={handleManualCheckinSubmit} className="manual-form-grid">
              <div className="form-group cccd-box-full">
                <label>Attendee National ID / CCCD Number *</label>
                <div style={{ display: "flex", gap: "0.5rem" }}>
                  <input
                    type="text"
                    required
                    placeholder="Enter National ID / CCCD (e.g. 001202000000, 079201008812)..."
                    value={manualForm.cccdNumber}
                    onChange={(e) => {
                      const val = e.target.value;
                      setManualForm({ ...manualForm, cccdNumber: val });
                      if (!val.trim()) {
                        setManualLookupTickets([]);
                        setSelectedManualTicket(null);
                        setManualLookupSearched(false);
                      }
                    }}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        e.preventDefault();
                        searchTicketsByCccd(manualForm.cccdNumber);
                      }
                    }}
                  />
                  <button
                    type="button"
                    className="btn-lookup-pink"
                    style={{ whiteSpace: "nowrap", padding: "0 1.25rem", minHeight: "44px" }}
                    onClick={() => searchTicketsByCccd(manualForm.cccdNumber)}
                    disabled={searchingManualTickets || !manualForm.cccdNumber.trim()}
                  >
                    {searchingManualTickets ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Tìm vé theo CCCD"}
                  </button>
                </div>
              </div>

              {/* Real-time DB lookup results for this CCCD */}
              {searchingManualTickets && (
                <div className="cccd-box-full" style={{ color: "#2fa4e0", padding: "0.5rem 0", fontStyle: "italic" }}>
                  Đang tra cứu vé từ cơ sở dữ liệu...
                </div>
              )}

              {manualLookupSearched && !searchingManualTickets && manualLookupTickets.length === 0 && (
                <div className="cccd-box-full empty-lookup-box" style={{ margin: "0.5rem 0" }}>
                  No tickets found matching ID / CCCD <strong>{manualForm.cccdNumber}</strong> for this concert in the database.
                </div>
              )}

              {manualLookupTickets.length > 0 && (
                <div className="cccd-box-full manual-tickets-preview-box" style={{ margin: "0.5rem 0" }}>
                  <label style={{ fontSize: "0.875rem", color: "#7a7392", display: "block", marginBottom: "0.5rem" }}>
                    Vé tìm thấy trong Database ({manualLookupTickets.length} vé) - Chọn vé cần check-in:
                  </label>
                  <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
                    {manualLookupTickets.map((t) => {
                      const isSelected = selectedManualTicket?.ticketId === t.ticketId || selectedManualTicket?.ticketCode === t.ticketCode;
                      const isAvailable = t.status === "AVAILABLE";
                      return (
                        <div
                          key={t.ticketId || t.ticketCode}
                          onClick={() => {
                            if (isAvailable) {
                              setSelectedManualTicket(t);
                              setManualForm((prev) => ({ ...prev, ticketCode: t.ticketCode }));
                            }
                          }}
                          style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            padding: "0.75rem 1rem",
                            borderRadius: "8px",
                            background: isSelected ? "rgba(255, 45, 117, 0.15)" : "rgba(30, 41, 59, 0.7)",
                            border: isSelected ? "2px solid #d91a63" : "1px solid #dcd3e8",
                            cursor: isAvailable ? "pointer" : "not-allowed",
                            opacity: isAvailable ? 1 : 0.65,
                            transition: "all 0.2s ease",
                          }}
                        >
                          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
                            <input
                              type="radio"
                              name="selectedManualTicket"
                              checked={isSelected}
                              disabled={!isAvailable}
                              onChange={() => {
                                setSelectedManualTicket(t);
                                setManualForm((prev) => ({ ...prev, ticketCode: t.ticketCode }));
                              }}
                              style={{ accentColor: "#d91a63", width: "18px", height: "18px", cursor: isAvailable ? "pointer" : "default" }}
                            />
                            <div>
                              <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
                                <span className="t-code-badge" style={{ fontSize: "0.85rem" }}>{t.ticketCode}</span>
                                <strong style={{ color: "#1e1b2e" }}>{t.customerName || "Khán giả"}</strong>
                              </div>
                              <div style={{ fontSize: "0.8rem", color: "#7a7392", marginTop: "0.2rem" }}>
                                Khu vực: <span style={{ color: "#d99a16" }}>{t.zoneName}</span> • Ghế: {t.seatNumber || "General"}
                              </div>
                            </div>
                          </div>
                          <div>
                            <span className={`status-pill ${t.status}`}>
                              {t.status === "AVAILABLE" ? "VALID / READY" : t.status === "CHECKED_IN" ? "CHECKED-IN" : t.status}
                            </span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}

              <div className="form-group">
                <label>Reason Code *</label>
                <select
                  value={manualForm.reasonCode}
                  onChange={(e) =>
                    setManualForm({ ...manualForm, reasonCode: e.target.value })
                  }
                >
                  <option value="DEAD_BATTERY">Attendee phone battery depleted</option>
                  <option value="BROKEN_SCREEN">Phone screen damaged or unreadable</option>
                  <option value="NETWORK_ERROR">Mobile network error on attendee phone</option>
                  <option value="OTHER">Paper ticket / Special exception</option>
                </select>
              </div>

              <div className="form-group">
                <label>Reason / Audit Notes</label>
                <input
                  type="text"
                  placeholder="Optional audit notes regarding manual exception..."
                  value={manualForm.reasonNote}
                  onChange={(e) =>
                    setManualForm({ ...manualForm, reasonNote: e.target.value })
                  }
                />
              </div>

              <div className="form-group cccd-box-full">
                <label className="checkbox-label-dark">
                  <input
                    type="checkbox"
                    required
                    checked={manualForm.cccdVerified}
                    onChange={(e) =>
                      setManualForm({ ...manualForm, cccdVerified: e.target.checked })
                    }
                  />
                  <span>
                    <strong>Physical Verification Confirmation:</strong> I confirm that I have inspected the original ID card and physically verified the attendee's face against the photo on the card.
                  </span>
                </label>
              </div>

              <div className="form-actions-row">
                <button
                  type="submit"
                  className="btn-validate-ticket-pink"
                  disabled={!manualForm.cccdVerified || !manualForm.cccdNumber.trim() || (!selectedManualTicket && !manualForm.ticketCode.trim()) || scanning}
                >
                  {scanning ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Xác nhận Check-in Vé"}
                </button>
              </div>
            </form>

            {/* Feedback for manual check-in result */}
            {scanResult && (
              <div
                className={`scan-feedback-card ${
                  scanResult.status === "SUCCESS"
                    ? "feedback-success"
                    : scanResult.status === "ALREADY_USED"
                    ? "feedback-warning"
                    : "feedback-error"
                }`}
                style={{ marginTop: "1.5rem" }}
              >
                <div className="feedback-body">
                  <h2>{scanResult.status === "SUCCESS" ? "ENTRY PERMITTED" : scanResult.status}</h2>
                  <p className="feedback-msg">{scanResult.message}</p>
                  {scanResult.customerName && (
                    <div className="feedback-details">
                      <span>Attendee: <strong>{scanResult.customerName}</strong></span>
                      <span>Zone: <strong>{scanResult.zoneName}</strong></span>
                      <span>Seat: <strong>{scanResult.seatNumber || "Standing"}</strong></span>
                      <span>Ticket: <strong>{scanResult.ticketCode}</strong></span>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 3: Manual Ticket Lookup (Figure 116) */}
      {activeTab === "LOOKUP" && (
        <div className="terminal-lookup-tab">
          <div className="card-lookup-main">
            <div className="lookup-header">
              <h2>Manual Ticket Lookup</h2>
              <p className="card-subtext">
                Search ticket inventory by attendee identification parameters. Non-searched personal data fields are masked to preserve data privacy.
              </p>
            </div>

            <form onSubmit={handleLookupSearch} className="lookup-form-dark">
              {/* Filter Pills */}
              <div className="search-type-selector">
                {[
                  { id: "PHONE", label: "Phone Number" },
                  { id: "CCCD", label: "ID / CCCD Number" },
                  { id: "EMAIL", label: "Email Address" },
                ].map((type) => (
                  <button
                    type="button"
                    key={type.id}
                    className={`type-chip-dark ${
                      lookupSearch.searchType === type.id ? "active" : ""
                    }`}
                    onClick={() => {
                      setLookupSearch({
                        searchType: type.id,
                        queryValue: "",
                      });
                      setLookupResults(null);
                    }}
                  >
                    {type.label}
                  </button>
                ))}
              </div>

              {/* Search Bar Row */}
              <div className="lookup-input-bar">
                <input
                  type="text"
                  required
                  placeholder={getLookupPlaceholder()}
                  value={lookupSearch.queryValue}
                  onChange={(e) =>
                    setLookupSearch({ ...lookupSearch, queryValue: e.target.value })
                  }
                  className="lookup-search-input"
                />
                <button
                  type="submit"
                  className="btn-lookup-pink"
                  disabled={searchingLookup || !lookupSearch.queryValue.trim()}
                >
                  {searchingLookup ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Search Ticket"}
                </button>
              </div>
            </form>

            {/* Lookup Results Section (Figure 116) */}
            {lookupResults && (
              <div className="lookup-results-container">
                {lookupResults.tickets?.length === 0 ? (
                  <div className="empty-lookup-box">
                    No tickets found matching the specified search criteria for this event.
                  </div>
                ) : (
                  <div className="attendee-result-card">
                    <div className="attendee-card-header">
                      <div>
                        <h3>{lookupResults.tickets[0]?.customerName || "Nguyen Thanh"}</h3>
                        <div className="attendee-meta-pills">
                          <span>Phone: <strong style={{ color: "#d99a16" }}>{lookupResults.tickets[0]?.maskedPhone}</strong></span>
                          <span>CCCD: <strong style={{ color: "#d99a16" }}>{lookupResults.tickets[0]?.maskedCccd}</strong></span>
                          <span>Order: <strong style={{ color: "#d91a63" }}>{lookupResults.tickets[0]?.orderId || "#AC-1847"}</strong></span>
                        </div>
                      </div>
                    </div>

                    <div className="associated-tickets-block">
                      <h4>Associated Tickets ({lookupResults.tickets.length})</h4>
                      <div className="tickets-cards-list">
                        {lookupResults.tickets.map((t) => (
                          <div key={t.ticketId} className="associated-ticket-item">
                            <div className="t-left">
                              <span className="t-code-badge" style={{ background: "rgba(56, 189, 248, 0.2)", color: "#38bdf8" }}>{t.zoneName}</span>
                              <span className="t-zone-info">Seat: {t.seatNumber || "General"}</span>
                              <span className="t-price-tag">{(t.price || 1500000).toLocaleString()} VND</span>
                            </div>
                            <div className="t-right">
                              <span className={`status-pill ${t.status}`}>
                                {t.status === "AVAILABLE" ? "VALID / READY" : t.status}
                              </span>
                              {t.status === "AVAILABLE" ? (
                                <button
                                  type="button"
                                  className="btn-checkin-direct-pink"
                                  onClick={() => handleLookupCheckinNow(t)}
                                >
                                  Check-in Manually Now
                                </button>
                              ) : (
                                <span className="checked-in-timestamp">
                                  Checked-in at {new Date(t.checkedInAt || Date.now()).toLocaleTimeString()}
                                </span>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 4: Scan History (Figure 117) */}
      {activeTab === "HISTORY" && (
        <div className="terminal-history-tab">
          <div className="history-main-card">
            <div className="history-header-block">
              <h2>Scan History</h2>
              <p className="card-subtext">
                Audit trail of admissions for the current event shift.
              </p>
            </div>

            {/* 4 KPI Summary Cards (Figure 117) */}
            <div className="kpi-cards-grid">
              <div className="kpi-card total">
                <span className="kpi-label">TOTAL SCANS</span>
                <strong className="kpi-value">{historyStats.totalScans ?? 0}</strong>
                <small className="kpi-sub">All admission attempts</small>
              </div>
              <div className="kpi-card approved">
                <span className="kpi-label">APPROVED</span>
                <strong className="kpi-value green">{historyStats.approvedScans ?? 0}</strong>
                <small className="kpi-sub">Admitted to stadium</small>
              </div>
              <div className="kpi-card used">
                <span className="kpi-label">ALREADY USED</span>
                <strong className="kpi-value yellow">{historyStats.alreadyUsedScans ?? 0}</strong>
                <small className="kpi-sub">Duplicate scan denied</small>
              </div>
              <div className="kpi-card invalid">
                <span className="kpi-label">ERRORS / INVALID</span>
                <strong className="kpi-value red">{historyStats.invalidScans ?? 0}</strong>
                <small className="kpi-sub">QR tampered / Wrong gate</small>
              </div>
            </div>

            {/* Filter Tabs */}
            <div className="history-filter-row">
              <div className="filter-pills-group">
                {[
                  { id: "ALL", label: `All (${historyStats.totalScans ?? 0})` },
                  { id: "SUCCESS", label: `Approved (${historyStats.approvedScans ?? 0})` },
                  { id: "ALREADY_USED", label: `Already Used (${historyStats.alreadyUsedScans ?? 0})` },
                  { id: "WRONG_GATE", label: `Errors (${historyStats.invalidScans ?? 0})` },
                ].map((tab) => (
                  <button
                    key={tab.id}
                    className={`history-filter-pill ${historyFilter === tab.id ? "active" : ""}`}
                    onClick={() => {
                      setHistoryFilter(tab.id);
                      fetchScanHistory(tab.id);
                    }}
                  >
                    {tab.label}
                  </button>
                ))}
              </div>
              <button
                type="button"
                className="btn-refresh-history"
                onClick={() => fetchScanHistory()}
              >
                Refresh Logs
              </button>
            </div>

            {/* Audit Table (Figure 117) */}
            <div className="table-responsive">
              <table className="terminal-table">
                <thead>
                  <tr>
                    <th>Time</th>
                    <th>Ticket Code</th>
                    <th>Attendee Name</th>
                    <th>Ticket Type</th>
                    <th>Order ID</th>
                    <th>Gate</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {scanFeed.length === 0 ? (
                    <tr>
                      <td colSpan="7" style={{ textAlign: "center", padding: "2rem", color: "#6b6480" }}>
                        No scan records found matching this filter.
                      </td>
                    </tr>
                  ) : (
                    scanFeed.map((item, idx) => (
                      <tr key={item.scanId || idx}>
                        <td style={{ color: "#7a7392", fontFamily: "monospace" }}>
                          {new Date(item.scannedAt).toLocaleTimeString()}
                        </td>
                        <td>
                          <strong style={{ color: "#1e1b2e", fontFamily: "monospace" }}>{item.ticketCode}</strong>
                        </td>
                        <td>
                          <strong style={{ color: "#1e1b2e" }}>{item.customerName || "N/A"}</strong>
                        </td>
                        <td>
                          <span style={{ color: "#2fa4e0", fontWeight: 600 }}>{item.zoneName || "General Admission"}</span>
                        </td>
                        <td>
                          {item.orderId ? `#${item.orderId}` : "N/A"}
                        </td>
                        <td>
                          <span style={{ color: "#7a7392" }}>{item.gateName || gateAssignment?.gateName || "Gate A"}</span>
                        </td>
                        <td>
                          <span className={`status-badge-pill ${item.status?.toLowerCase()}`}>
                            {item.status === "SUCCESS"
                              ? "Success"
                              : item.status === "ALREADY_USED"
                              ? "Already Used"
                              : "Invalid QR"}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 5: Offline Admission Mode & Edge Sync */}
      {activeTab === "OFFLINE" && (
        <div className="terminal-offline-tab">
          <div className="card-offline-control">
            <div className="offline-card-header">
              <h2>Offline Check-in Mode & Edge Sync</h2>
              <p className="card-subtext">
                Edge admission redundancy allows gates to verify tickets without cloud internet connection using IndexedDB cached packages.
              </p>
            </div>

            <div className="offline-status-grid">
              <div className="offline-info-card">
                <span className="offline-card-title">Network Connectivity</span>
                <strong style={{ fontSize: "1.3rem", color: isNetworkOnline ? "#0e9e78" : "#d63a3a" }}>
                  {isNetworkOnline ? "Online (Server Reachable)" : "Offline (Network Disconnected)"}
                </strong>
                <p style={{ margin: "0.5rem 0 0", color: "#7a7392", fontSize: "0.85rem" }}>
                  Current Mode: <strong style={{ color: isOfflineMode ? "#d98f14" : "#0e9e78" }}>{isOfflineMode ? "Offline Verification Active" : "Cloud Online"}</strong>
                </p>
              </div>

              <div className="offline-info-card">
                <span className="offline-card-title">Cached Offline Package</span>
                <strong style={{ fontSize: "1.3rem", color: offlinePackageMeta ? "#d91a63" : "#7a7392" }}>
                  {offlinePackageMeta ? `${offlinePackageMeta.totalTickets || 3} Preloaded Tickets` : "No Package Loaded"}
                </strong>
                <p style={{ margin: "0.5rem 0 0", color: "#7a7392", fontSize: "0.85rem" }}>
                  Event: <strong>{offlinePackageMeta?.eventTitle || "Assigned Concert"}</strong>
                </p>
              </div>

              <div className="offline-info-card">
                <span className="offline-card-title">Pending Sync Queue</span>
                <strong style={{ fontSize: "1.3rem", color: offlineQueueCount > 0 ? "#d99a16" : "#0e9e78" }}>
                  {offlineQueueCount} Scans Waiting
                </strong>
                <p style={{ margin: "0.5rem 0 0", color: "#7a7392", fontSize: "0.85rem" }}>
                  Stored safely in local IndexedDB until connection restores.
                </p>
              </div>
            </div>

            <div className="offline-actions-toolbar">
              <button
                type="button"
                className="btn-offline-action-pink"
                onClick={handleDownloadOfflinePackage}
                disabled={downloadingPackage}
              >
                {downloadingPackage ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Download / Refresh Offline Package"}
              </button>

              <button
                type="button"
                className="btn-offline-action-blue"
                onClick={() => handleSyncOfflineScans(false)}
                disabled={syncingOffline || offlineQueueCount === 0}
              >
                {syncingOffline ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : `Synchronize Scans to Server (${offlineQueueCount})`}
              </button>

              <button
                type="button"
                className="btn-offline-action-warning"
                onClick={() => setShowConflictModal(true)}
              >
                Resolve Conflicts {pendingConflictsCount > 0 ? `(${pendingConflictsCount})` : ""}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Universal Terminal Modal Dialog (Replaces raw browser alert) */}
      <OfflineConflictModal
        isOpen={showConflictModal}
        onClose={() => {
          setShowConflictModal(false);
          checkConflicts();
        }}
        eventId={currentEventId}
        onConflictResolved={() => {
          checkConflicts();
          fetchScanHistory();
        }}
      />

      {terminalDialog && (
        <div className="terminal-dialog-overlay">
          <div className="terminal-dialog-box">
            <div className={`terminal-dialog-header ${terminalDialog.type?.toLowerCase()}`}>
              <div className="dialog-header-left">
                <h3>{terminalDialog.title}</h3>
              </div>
              <button
                type="button"
                className="close-btn"
                onClick={() => setTerminalDialog(null)}
              >
                &times;
              </button>
            </div>

            <div className="terminal-dialog-body">
              <p className="dialog-main-message">{terminalDialog.message}</p>

              {terminalDialog.syncDetails && (
                <div className="sync-breakdown-card">
                  <div className="sync-stat-row">
                    <span>Total processed scans:</span>
                    <strong>{terminalDialog.syncDetails.totalProcessed} scans</strong>
                  </div>
                  <div className="sync-stat-row text-success">
                    <span>Newly admitted tickets:</span>
                    <strong>+{terminalDialog.syncDetails.newlyCheckedIn} tickets</strong>
                  </div>
                  <div className="sync-stat-row text-warning">
                    <span>Previously scanned (duplicate):</span>
                    <strong>{terminalDialog.syncDetails.alreadyUsedCount} tickets</strong>
                  </div>
                  {terminalDialog.syncDetails.conflictCount > 0 && (
                    <div className="sync-stat-row" style={{ color: "#d64545", fontWeight: 700 }}>
                      <span>Duplicate scan conflicts detected:</span>
                      <strong>{terminalDialog.syncDetails.conflictCount} tickets</strong>
                    </div>
                  )}
                  <div className="sync-stat-row">
                    <span>Server completed at:</span>
                    <small>{new Date(terminalDialog.syncDetails.syncedAt || Date.now()).toLocaleTimeString("en-US")}</small>
                  </div>
                </div>
              )}
            </div>

            <div className="terminal-dialog-footer">
              {terminalDialog.syncDetails?.conflictCount > 0 && (
                <button
                  type="button"
                  className="btn btn-dialog-action"
                  style={{ background: "#c06a0a", borderColor: "#b45309", color: "#fff", marginRight: "10px" }}
                  onClick={() => {
                    setTerminalDialog(null);
                    setShowConflictModal(true);
                  }}
                >
                  Resolve Conflicts Now
                </button>
              )}
              <button
                type="button"
                className={`btn btn-dialog-action ${terminalDialog.type === "SYNC_RESULT" ? "btn-success" : "btn-primary"}`}
                onClick={() => {
                  if (terminalDialog.onConfirm) terminalDialog.onConfirm();
                  setTerminalDialog(null);
                }}
              >
                Acknowledge & Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
