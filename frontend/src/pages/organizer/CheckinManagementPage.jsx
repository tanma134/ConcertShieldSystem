import { useEffect, useState, useCallback } from "react";
import { Link, useParams } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import reportApi, { saveBlob } from "../../api/reportApi";
import checkinApi from "../../api/checkinApi";
import staffApi from "../../api/staffApi";
import eventApi from "../../api/eventApi";
import ticketTypeApi from "../../api/ticketTypeApi";
import { apiErrorMessage } from "../../utils/apiError";
import { formatDateTime } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./OrganizerDashboardPage.css";
import "./OrganizerStaffPage.css";
import "../../styles/reportPages.css";

// UC_14.2 (export check-in report), UC_14.3 (view staff list) and UC_14.4 (assign / unassign staff),
// plus Event Gates Management with Ticket Type mapping & Shift Scheduling.
export default function CheckinManagementPage() {
  const { id } = useParams();
  const [eventsList, setEventsList] = useState([]);
  const [activeEventId, setActiveEventId] = useState(id ? Number(id) : null);
  const effectiveId = activeEventId || (id ? Number(id) : null);

  const [checkin, setCheckin] = useState(null);
  const [staff, setStaff] = useState([]);
  const [candidates, setCandidates] = useState([]);
  const [ticketTypes, setTicketTypes] = useState([]);
  const [gatesList, setGatesList] = useState([]);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [toast, setToast] = useState(null);
  const [busy, setBusy] = useState(false);
  const [searched, setSearched] = useState(false);
  const [confirmModal, setConfirmModal] = useState(null);

  // Assignment Form State per Candidate
  const [gateByStaff, setGateByStaff] = useState({});
  const [shiftStartByStaff, setShiftStartByStaff] = useState({});
  const [shiftEndByStaff, setShiftEndByStaff] = useState({});
  const [shiftDateByStaff, setShiftDateByStaff] = useState({});
  const [shiftStartDateTimeByStaff, setShiftStartDateTimeByStaff] = useState({});
  const [shiftEndDateTimeByStaff, setShiftEndDateTimeByStaff] = useState({});
  const [checkinByStaff, setCheckinByStaff] = useState({});
  const [reviewByStaff, setReviewByStaff] = useState({});
  const [incidentsByStaff, setIncidentsByStaff] = useState({});
  const [supportByStaff, setSupportByStaff] = useState({});

  // Active section tab: "OPERATIONS", "GATES", "STAFF", "REPORT"
  const [activeTab, setActiveTab] = useState("OPERATIONS");

  // Modal State for Creating Gate
  const [isGateModalOpen, setIsGateModalOpen] = useState(false);
  const [gateFormData, setGateFormData] = useState({
    gateName: "",
    description: "",
    selectedTicketTypes: [],
  });
  const [gateFormError, setGateFormError] = useState("");
  const [savingGate, setSavingGate] = useState(false);

  // Modal State for Editing Staff Shift
  const [editingStaffShift, setEditingStaffShift] = useState(null);
  const [shiftFormData, setShiftFormData] = useState({
    gateName: "",
    shiftStartTime: "08:00",
    shiftEndTime: "17:00",
    shiftDate: "",
  });
  const [savingShift, setSavingShift] = useState(false);

  const showToast = (message, type = "ok") => {
    setToast({ message, type });
    window.setTimeout(() => setToast(null), 4000);
  };

  // Load Organizer's Events to allow switching
  useEffect(() => {
    eventApi
      .getMine()
      .then((res) => {
        const list = res.data?.data || [];
        setEventsList(list);
        if (!id && list.length > 0 && !activeEventId) {
          const firstId = list[0].eventId || list[0].id;
          setActiveEventId(firstId);
        }
      })
      .catch(() => {});
  }, [id, activeEventId]);

  const loadAll = useCallback(async () => {
    if (!effectiveId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const [checkinRes, staffRes, candidateRes, gatesRes, ticketTypesRes] = await Promise.all([
        reportApi.getCheckin(effectiveId).catch(() => ({ data: { data: null } })),
        staffApi.list(effectiveId).catch(() => ({ data: { data: [] } })),
        staffApi.candidates(effectiveId, "").catch(() => ({ data: { data: [] } })),
        eventApi.getGates(effectiveId).catch(() => ({ data: { data: [] } })),
        ticketTypeApi.getByEvent(effectiveId).catch(() => ({ data: { data: [] } })),
      ]);

      setCheckin(checkinRes.data?.data || null);
      setStaff(staffRes.data?.data || []);
      setCandidates(candidateRes.data?.data || []);
      setGatesList(gatesRes.data?.data || []);

      const tt = ticketTypesRes.data?.data || [];
      setTicketTypes(Array.isArray(tt) ? tt : []);
      setSearched(true);
    } catch (err) {
      setError(apiErrorMessage(err, "Could not load check-in or gate data."));
    } finally {
      setLoading(false);
    }
  }, [effectiveId]);

  useEffect(() => {
    if (effectiveId) {
      loadAll();
    }
  }, [effectiveId, loadAll]);

  // Export check-in report as CSV (structured Real-time Check-in Monitor data)
  const exportCsv = async () => {
    if (!effectiveId) return;
    setBusy(true);
    try {
      const res = await checkinApi.getMonitorStatus(effectiveId, {}).catch(() => null);
      const d = res?.data?.data || {};
      const currentEv = eventsList.find((e) => (e.eventId || e.id) === Number(effectiveId));
      const s = checkin?.summary || {};

      const esc = (val) => {
        const str = val == null ? "" : String(val);
        if (str.includes(",") || str.includes('"') || str.includes("\n")) {
          return `"${str.replace(/"/g, '""')}"`;
        }
        return str;
      };

      const exportedAt = new Date().toLocaleString("en-US");
      const totalIssued = d.totalIssued ?? s.totalTickets ?? 0;
      const checkedIn = d.checkedIn ?? s.checkedIn ?? 0;
      const pending = d.pending ?? s.notCheckedIn ?? Math.max(0, totalIssued - checkedIn);
      const occupancyRate = d.occupancyRate ?? s.checkInRatePercent ?? 0;
      const gatesRows = Array.isArray(d.gates) ? d.gates : [];
      const zonesRows = Array.isArray(d.zones) ? d.zones : [];
      const scansRows = Array.isArray(d.recentScans) ? d.recentScans : [];

      const lines = [
        "=== 1. REAL-TIME CHECK-IN OVERVIEW ===",
        "Metric,Value",
        `Event ID,${esc(effectiveId)}`,
        `Event Title,${esc(d.eventTitle || currentEv?.title || `Concert #${effectiveId}`)}`,
        `Exported At,${esc(exportedAt)}`,
        `Total Tickets Issued,${totalIssued}`,
        `Checked-in Tickets (Used),${checkedIn}`,
        `Total / Used Ratio,${esc(`${totalIssued} / ${checkedIn}`)}`,
        `Pending Admission,${pending}`,
        `Occupancy Rate (%),${esc(`${occupancyRate}%`)}`,
        "",
        "=== 2. GATE ADMISSION BREAKDOWN ===",
        "Gate ID,Gate Name,Total Assigned,Checked-in (Used),Pending,Occupancy Rate (%),Gate Status",
        ...(gatesRows.length > 0
          ? gatesRows.map(
              (g) =>
                `${esc(g.gateId)},${esc(g.gateName)},${g.totalAssigned ?? 0},${g.checkedIn ?? 0},${g.pending ?? Math.max(0, (g.totalAssigned ?? 0) - (g.checkedIn ?? 0))},${esc(`${g.occupancyRate ?? 0}%`)},${esc(g.status || "Normal")}`
            )
          : ["No gate data available,,,,"]),
        "",
        "=== 3. TICKET TIER & SEATING ZONE BREAKDOWN ===",
        "Ticket Tier / Zone Name,Total Issued,Checked-in (Used),Pending,Occupancy Rate (%)",
        ...(zonesRows.length > 0
          ? zonesRows.map(
              (z) =>
                `${esc(z.zoneName)},${z.totalIssued ?? 0},${z.checkedIn ?? 0},${z.pending ?? Math.max(0, (z.totalIssued ?? 0) - (z.checkedIn ?? 0))},${esc(`${z.occupancyRate ?? 0}%`)}`
            )
          : ["No zone data available,,,,"]),
        "",
        "=== 4. RECENT SCAN TELEMETRY STREAM (SCAN HISTORY) ===",
        "Scan ID,Attendee Name,Ticket Tier / Zone,Entrance Gate,Scan Time,Scan Method,Status",
        ...(scansRows.length > 0
          ? scansRows.map(
              (sRow) =>
                `${esc(sRow.scanId)},${esc(sRow.customerName)},${esc(sRow.zoneName)},${esc(sRow.gateName)},${esc(sRow.scannedAt ? new Date(sRow.scannedAt).toLocaleString("en-US") : "")},${esc(sRow.scanMethod)},${esc(sRow.status)}`
            )
          : ["No scan events logged yet,,,,,,"]),
      ];

      const csvContent = "\uFEFF" + lines.join("\r\n");
      const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
      saveBlob({ data: blob, headers: {} }, `realtime-checkin-event-${effectiveId}.csv`);
      showToast("Check-in report exported successfully!");
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not export report."), "error");
    } finally {
      setBusy(false);
    }
  };

  // Search candidate staff accounts
  const searchCandidates = async () => {
    if (!effectiveId) return;
    setBusy(true);
    try {
      const res = await staffApi.candidates(effectiveId, query.trim());
      setCandidates(res.data?.data || []);
      setSearched(true);
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not search staff accounts."), "error");
    } finally {
      setBusy(false);
    }
  };

const toDateTimeLocal = (isoStr) => {
  if (!isoStr) return "";
  const d = new Date(isoStr);
  if (isNaN(d.getTime())) return "";
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const formatEventDateTime = (d) => {
  if (!d) return "";
  const dateObj = new Date(d);
  if (isNaN(dateObj.getTime())) return String(d);
  return dateObj.toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
};

const getShiftScheduleWarning = (ev, shiftStartDtStr, shiftEndDtStr) => {
  if (!ev?.startsAt || !shiftStartDtStr || !shiftEndDtStr) return null;
  const evStartLocal = toDateTimeLocal(ev.startsAt);
  const evEndLocal = ev.endsAt
    ? toDateTimeLocal(ev.endsAt)
    : toDateTimeLocal(new Date(new Date(ev.startsAt).getTime() + 4 * 3600000));
  if (!evStartLocal || !evEndLocal) return null;

  const evStartMs = new Date(evStartLocal).getTime();
  const evEndMs = new Date(evEndLocal).getTime();
  const shiftStartMs = new Date(shiftStartDtStr).getTime();
  const shiftEndMs = new Date(shiftEndDtStr).getTime();

  if (isNaN(evStartMs) || isNaN(evEndMs) || isNaN(shiftStartMs) || isNaN(shiftEndMs)) {
    return null;
  }

  const isDifferent =
    Math.abs(shiftStartMs - evStartMs) >= 60000 ||
    Math.abs(shiftEndMs - evEndMs) >= 60000;

  if (!isDifferent) return null;

  const a = formatEventDateTime(evStartLocal);
  const b = formatEventDateTime(evEndLocal);
  const c = formatEventDateTime(shiftStartDtStr);
  const d = formatEventDateTime(shiftEndDtStr);

  return `Notice: This event takes place from ${a} to ${b}, and you are currently assigning a shift from ${c} to ${d}. Are you sure you want to proceed with this schedule?`;
};

  // Assign staff to event with gate and shift hours (based on 3 Staff Roles)
  const assign = async (candidate) => {
    if (!effectiveId) return;
    setBusy(true);
    try {
      // Role 1: Check-in Staff -> Gate Check-in (QR/Manual/Offline) + On-site Incidents
      const isCheckinStaff = checkinByStaff[candidate.userId] ?? true;
      // Role 2: Security/Support Staff -> Support Cases + Fraud Cases & Risk Decisions
      const isSecuritySupportStaff = !!supportByStaff[candidate.userId];
      // Role 3: Ticketing/Operations Staff -> Process Full/Partial Refund + View Seat/Ticket Status History
      const isTicketingOpsStaff = !!reviewByStaff[candidate.userId];

      const isCheckin = isCheckinStaff;
      const canIncidents = isCheckinStaff;
      const canSupport = isSecuritySupportStaff;
      const canReview = isTicketingOpsStaff;

      if (!isCheckinStaff && !isSecuritySupportStaff && !isTicketingOpsStaff) {
        showToast("Please select at least one Staff Role (Check-in Staff, Security/Support Staff, or Ticketing/Operations Staff).", "error");
        setBusy(false);
        return;
      }

      const currentEv = eventsList.find((e) => (e.eventId || e.id) === Number(effectiveId));
      const defaultStart = currentEv?.startsAt ? toDateTimeLocal(currentEv.startsAt) : "";
      const defaultEnd = currentEv?.endsAt
        ? toDateTimeLocal(currentEv.endsAt)
        : (currentEv?.startsAt ? toDateTimeLocal(new Date(new Date(currentEv.startsAt).getTime() + 4 * 3600000)) : "");

      let chosenGate = "General Duty";
      let startTime = null;
      let endTime = null;
      let shiftDate = null;

      if (isCheckin) {
        chosenGate = gateByStaff[candidate.userId] || (gatesList.length > 0 ? gatesList[0].gateName : "Main Gate");
        const startVal = shiftStartDateTimeByStaff[candidate.userId] || defaultStart;
        const endVal = shiftEndDateTimeByStaff[candidate.userId] || defaultEnd;

        if (!startVal || !endVal) {
          showToast("Please choose both shift start and end date & time.", "error");
          setBusy(false);
          return;
        }

        const sDt = new Date(startVal);
        const eDt = new Date(endVal);

        if (eDt <= sDt) {
          showToast("Shift end time must be after shift start time.", "error");
          setBusy(false);
          return;
        }

        shiftDate = startVal.substring(0, 10);
        const shiftEndDate = endVal.substring(0, 10);
        startTime = startVal.substring(11, 16);
        endTime = endVal.substring(11, 16);

        await staffApi.assign(
          effectiveId,
          candidate.userId,
          chosenGate,
          canReview,
          startTime,
          endTime,
          shiftDate,
          canIncidents,
          canSupport,
          isCheckin,
          shiftEndDate
        );
      } else {
        await staffApi.assign(
          effectiveId,
          candidate.userId,
          chosenGate,
          canReview,
          null,
          null,
          null,
          canIncidents,
          canSupport,
          isCheckin,
          null
        );
      }

      const roleLabels = [];
      if (isCheckinStaff) roleLabels.push(`Check-in Staff (${chosenGate})`);
      if (isSecuritySupportStaff) roleLabels.push("Security/Support Staff");
      if (isTicketingOpsStaff) roleLabels.push("Ticketing/Operations Staff");

      showToast(`Assigned ${candidate.fullName} as [${roleLabels.join(", ")}].`);
      await Promise.all([loadAll(), searchCandidates()]);
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not assign this staff member."), "error");
    } finally {
      setBusy(false);
    }
  };

  // Open modal to edit existing staff shift
  const openEditShiftModal = (member) => {
    const currentEv = eventsList.find((e) => (e.eventId || e.id) === Number(effectiveId));
    const defaultStart = currentEv?.startsAt ? toDateTimeLocal(currentEv.startsAt) : "";
    const defaultEnd = currentEv?.endsAt
      ? toDateTimeLocal(currentEv.endsAt)
      : (currentEv?.startsAt ? toDateTimeLocal(new Date(new Date(currentEv.startsAt).getTime() + 4 * 3600000)) : "");

    const sDate = member.shiftDate ? String(member.shiftDate).substring(0, 10) : (defaultStart ? defaultStart.substring(0, 10) : "");
    const eDate = member.shiftEndDate ? String(member.shiftEndDate).substring(0, 10) : sDate;
    const sTime = member.shiftStartTime ? String(member.shiftStartTime).substring(0, 5) : (defaultStart ? defaultStart.substring(11, 16) : "08:00");
    const eTime = member.shiftEndTime ? String(member.shiftEndTime).substring(0, 5) : (defaultEnd ? defaultEnd.substring(11, 16) : "17:00");

    const startDateTime = sDate && sTime ? `${sDate}T${sTime}` : defaultStart;
    const endDateTime = eDate && eTime ? `${eDate}T${eTime}` : (sDate && eTime ? `${sDate}T${eTime}` : defaultEnd);

    setEditingStaffShift(member);
    setShiftFormData({
      gateName: member.gateName || (gatesList[0]?.gateName || "Main Gate"),
      shiftStartDateTime: startDateTime,
      shiftEndDateTime: endDateTime,
    });
  };

  const handleSaveShift = async (e) => {
    e.preventDefault();
    if (!editingStaffShift || !effectiveId) return;

    const startVal = shiftFormData.shiftStartDateTime;
    const endVal = shiftFormData.shiftEndDateTime;

    if (!startVal || !endVal) {
      showToast("Please choose both shift start and end date & time.", "error");
      return;
    }

    const sDt = new Date(startVal);
    const eDt = new Date(endVal);

    if (eDt <= sDt) {
      showToast("Shift end time must be after shift start time.", "error");
      return;
    }

    const shiftDate = startVal.substring(0, 10);
    const shiftEndDate = endVal.substring(0, 10);
    const shiftStartTime = startVal.substring(11, 16);
    const shiftEndTime = endVal.substring(11, 16);

    setSavingShift(true);
    try {
      await staffApi.updateShift(effectiveId, editingStaffShift.staffUserId, {
        gateName: shiftFormData.gateName,
        shiftStartTime,
        shiftEndTime,
        shiftDate,
        shiftEndDate,
      });

      showToast(`Shift updated for ${editingStaffShift.fullName} (From ${shiftDate} ${shiftStartTime} to ${shiftEndDate} ${shiftEndTime}).`);
      setEditingStaffShift(null);
      await loadAll();
    } catch (err) {
      showToast(apiErrorMessage(err, "Failed to update staff shift."), "error");
    } finally {
      setSavingShift(false);
    }
  };

  // Toggle one of the 3 Staff Roles (Check-in Staff, Security/Support Staff, Ticketing/Operations Staff)
  const toggleStaffRole = async (member, roleType, currentValue) => {
    if (!effectiveId) return;
    setBusy(true);
    try {
      const nextVal = !currentValue;
      let payload = {};
      let label = "";

      if (roleType === "CHECKIN_STAFF") {
        payload = { canCheckin: nextVal, canManageIncidents: nextVal };
        label = "Check-in Staff (Gate Check-in & On-site Incidents)";
      } else if (roleType === "SECURITY_SUPPORT_STAFF") {
        payload = { canManageSupportCases: nextVal };
        label = "Security/Support Staff (Support Cases & Fraud/Risk)";
      } else if (roleType === "TICKETING_OPS_STAFF") {
        payload = { canReviewReturns: nextVal };
        label = "Ticketing/Operations Staff (Process Refund & Ticket Status History)";
      }

      await staffApi.updateDuties(effectiveId, member.staffUserId, payload);
      showToast(
        nextVal
          ? `Assigned ${member.fullName} to role: ${label}.`
          : `Removed ${member.fullName} from role: ${label}.`
      );
      await loadAll();
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not update staff role."), "error");
    } finally {
      setBusy(false);
    }
  };

  // Remove staff member from event
  const unassign = (member) => {
    if (!effectiveId) return;
    setConfirmModal({
      title: "Remove Staff Member",
      message: `Are you sure you want to remove ${member.fullName} from this concert?`,
      confirmText: "Remove Staff",
      confirmBtnStyle: { background: "#dc2626", color: "#ffffff" },
      onConfirm: async () => {
        setConfirmModal(null);
        setBusy(true);
        try {
          await staffApi.unassign(effectiveId, member.staffUserId);
          showToast(`${member.fullName} removed from event.`);
          await loadAll();
          if (searched) await searchCandidates();
        } catch (err) {
          showToast(apiErrorMessage(err, "Could not remove staff member."), "error");
        } finally {
          setBusy(false);
        }
      }
    });
  };

  // Gate Management Handlers
  const openGateModal = () => {
    setGateFormData({
      gateName: "",
      description: "",
      selectedTicketTypes: [],
    });
    setGateFormError("");
    setIsGateModalOpen(true);
  };

  const toggleTicketTypeSelection = (typeName) => {
    setGateFormData((prev) => {
      const exists = prev.selectedTicketTypes.includes(typeName);
      const updated = exists
        ? prev.selectedTicketTypes.filter((t) => t !== typeName)
        : [...prev.selectedTicketTypes, typeName];
      return { ...prev, selectedTicketTypes: updated };
    });
  };

  const handleSelectAllTicketTypes = () => {
    if (gateFormData.selectedTicketTypes.length === ticketTypes.length) {
      setGateFormData((prev) => ({ ...prev, selectedTicketTypes: [] }));
    } else {
      setGateFormData((prev) => ({
        ...prev,
        selectedTicketTypes: ticketTypes.map((t) => t.typeName),
      }));
    }
  };

  const handleSaveGate = async (e) => {
    e.preventDefault();
    setGateFormError("");

    const trimmedName = gateFormData.gateName.trim();
    if (!trimmedName) {
      setGateFormError("Please enter gate name (e.g. Gate A - VIP Lounge).");
      return;
    }

    const isDuplicate = gatesList.some((g) => g.gateName.trim().toLowerCase() === trimmedName.toLowerCase());
    if (isDuplicate) {
      setGateFormError(`A gate with the name "${trimmedName}" already exists for this event.`);
      return;
    }

    if (!effectiveId) return;

    setSavingGate(true);
    try {
      const allowedString =
        gateFormData.selectedTicketTypes.length > 0
          ? gateFormData.selectedTicketTypes.join(",")
          : "ALL";

      await eventApi.createGate(effectiveId, {
        gateName: trimmedName,
        description: gateFormData.description.trim() || null,
        allowedTicketTypes: allowedString,
        allowedTicketTypeList: gateFormData.selectedTicketTypes,
      });

      showToast(`Gate "${trimmedName}" created successfully!`);
      setIsGateModalOpen(false);
      const gatesRes = await eventApi.getGates(effectiveId);
      setGatesList(gatesRes.data?.data || []);
    } catch (err) {
      setGateFormError(err.response?.data?.message || err.message || "Failed to create gate.");
    } finally {
      setSavingGate(false);
    }
  };

  const handleDeleteGate = (gate) => {
    if (!effectiveId) return;
    setConfirmModal({
      title: "Delete Entry Gate",
      message: `Are you sure you want to delete gate "${gate.gateName}"? This action cannot be undone.`,
      confirmText: "Delete Gate",
      confirmBtnStyle: { background: "#dc2626", color: "#ffffff" },
      onConfirm: async () => {
        setConfirmModal(null);
        try {
          await eventApi.deleteGate(effectiveId, gate.gateId);
          showToast(`Gate "${gate.gateName}" removed.`);
          const gatesRes = await eventApi.getGates(effectiveId);
          setGatesList(gatesRes.data?.data || []);
        } catch (err) {
          showToast(err.response?.data?.message || "Could not delete gate.", "error");
        }
      }
    });
  };

  // Helper to find gate definition by name
  const findGateConfig = (name) => {
    return gatesList.find((g) => g.gateName.toLowerCase() === (name || "").toLowerCase());
  };

  // Calculate staff currently on-duty (checks both shift hours and shift date / date range)
  const isShiftActiveNow = (startStr, endStr, dateStr, endDateStr) => {
    try {
      const now = new Date();
      const sDate = dateStr ? String(dateStr).substring(0, 10) : "";
      const eDate = endDateStr ? String(endDateStr).substring(0, 10) : sDate;

      const sTime = startStr ? String(startStr).substring(0, 5) : "00:00";
      const eTime = endStr ? String(endStr).substring(0, 5) : "23:59";

      if (sDate) {
        const startDt = new Date(`${sDate}T${sTime}:00`);
        const endDt = new Date(`${eDate}T${eTime}:00`);
        if (!isNaN(startDt.getTime()) && !isNaN(endDt.getTime())) {
          return now >= startDt && now <= endDt;
        }
      }

      if (!startStr || !endStr) return true;
      const currentMinutes = now.getHours() * 60 + now.getMinutes();
      const parseToMin = (s) => {
        const p = s.split(":");
        return parseInt(p[0], 10) * 60 + parseInt(p[1], 10);
      };
      const sMin = parseToMin(startStr);
      const eMin = parseToMin(endStr);
      if (eMin >= sMin) return currentMinutes >= sMin && currentMinutes <= eMin;
      return currentMinutes >= sMin || currentMinutes <= eMin;
    } catch {
      return true;
    }
  };

  const onDutyCount = staff.filter((s) => isShiftActiveNow(s.shiftStartTime, s.shiftEndTime, s.shiftDate, s.shiftEndDate)).length;
  const summary = checkin?.summary;

  return (
    <OrganizerShell title="Check-in & Staff Operations" theme="light">
      <style>{`
        .cmp-scope h1, .cmp-scope h2, .cmp-scope h3, .cmp-scope h4 {
          color: #0f172a !important;
        }
        .cmp-scope .ow-table th {
          background: #f1f5f9 !important;
          color: #334155 !important;
          font-weight: 700 !important;
          font-size: 0.8rem !important;
          text-transform: uppercase !important;
          letter-spacing: 0.04em !important;
          border-bottom: 2px solid #cbd5e1 !important;
          border-top: none !important;
          padding: 12px 14px !important;
        }
        .cmp-scope .ow-table td {
          background: #ffffff !important;
          color: #0f172a !important;
          border-bottom: 1px solid #e2e8f0 !important;
          padding: 12px 14px !important;
        }
        .cmp-scope .ow-table tr:hover td {
          background: #f8fafc !important;
        }
        .cmp-scope .ow-td-title {
          color: #0f172a !important;
          font-weight: 600 !important;
        }
        .cmp-scope .ow-td-sub {
          color: #64748b !important;
        }
        .cmp-scope .tb-empty {
          background: #f8fafc !important;
          border: 1px dashed #cbd5e1 !important;
          border-radius: 8px !important;
          color: #475569 !important;
          text-align: center !important;
        }
        .cmp-candidate-box {
          background: #f8fafc;
          border: 1px solid #e2e8f0;
          border-radius: 10px;
          padding: 14px 16px;
          margin-bottom: 16px;
        }
      `}</style>
      <div className="tb-container rp-wrap cmp-scope" style={{ maxWidth: "1200px", margin: "0 auto", padding: "24px 16px" }}>
        {/* Top Header */}
        <div className="od-head" style={{ marginBottom: "20px", display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "16px" }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "12px", marginBottom: "6px", flexWrap: "wrap" }}>
              <h1 style={{ margin: 0, fontSize: "1.75rem", fontWeight: 700, color: "#111827" }}>
                Staff & Gate Management
              </h1>
              {eventsList.length > 0 && (
                <select
                  value={effectiveId || ""}
                  onChange={(e) => setActiveEventId(Number(e.target.value))}
                  style={{
                    padding: "6px 12px",
                    borderRadius: "8px",
                    border: "1px solid #cbd5e1",
                    fontSize: "0.9rem",
                    fontWeight: 600,
                    background: "#ffffff",
                    color: "#1e293b",
                    cursor: "pointer",
                    boxShadow: "0 1px 2px rgba(0,0,0,0.05)"
                  }}
                  title="Switch Concert"
                >
                  {eventsList.map((ev) => (
                    <option key={ev.eventId || ev.id} value={ev.eventId || ev.id}>
                      {ev.title || `Concert #${ev.eventId || ev.id}`}
                    </option>
                  ))}
                </select>
              )}
            </div>
            <p style={{ margin: 0, color: "#4b5563", fontSize: "0.95rem" }}>
              Configure gate assignments, staff shifts, and check-in access policies.
            </p>
          </div>
          <div style={{ display: "flex", gap: "10px", flexWrap: "wrap", alignItems: "center" }}>
            <button
              type="button"
              disabled={busy || !effectiveId}
              onClick={exportCsv}
              style={{
                padding: "8px 18px",
                borderRadius: "8px",
                fontWeight: 600,
                fontSize: "0.9rem",
                background: "#ffffff",
                border: "1px solid #0f172a",
                color: "#0f172a",
                cursor: busy || !effectiveId ? "not-allowed" : "pointer",
                boxShadow: "0 1px 2px rgba(0,0,0,0.05)"
              }}
            >
              Export Check-in Report
            </button>
            <Link
              to={effectiveId ? `/organizer/events/${effectiveId}/realtime-checkin` : "/organizer/realtime-checkin"}
              style={{
                padding: "8px 18px",
                borderRadius: "8px",
                fontWeight: 600,
                fontSize: "0.9rem",
                background: "linear-gradient(135deg, #d61f6e, #be185d)",
                color: "#ffffff",
                textDecoration: "none",
                boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)",
                display: "inline-flex",
                alignItems: "center",
                gap: "6px"
              }}
            >
              Real-time Monitor
            </Link>
            <Link
              to="/organizer/staff"
              style={{
                padding: "8px 18px",
                borderRadius: "8px",
                fontWeight: 600,
                fontSize: "0.9rem",
                background: "#ffffff",
                border: "1px solid #be185d",
                color: "#be185d",
                textDecoration: "none",
                boxShadow: "0 1px 2px rgba(0,0,0,0.05)"
              }}
            >
              Staff Directory
            </Link>
          </div>
        </div>

        {/* Filter Pills (Styled like Organizer Requests UI) */}
        <div style={{ display: "flex", gap: "8px", marginBottom: "20px", flexWrap: "wrap" }}>
          <button
            type="button"
            onClick={() => setActiveTab("OPERATIONS")}
            style={{
              padding: "6px 18px",
              borderRadius: "20px",
              fontSize: "0.9rem",
              fontWeight: 600,
              cursor: "pointer",
              border: activeTab === "OPERATIONS" ? "1px solid #be185d" : "1px solid #e5e7eb",
              background: activeTab === "OPERATIONS" ? "#be185d" : "#ffffff",
              color: activeTab === "OPERATIONS" ? "#ffffff" : "#374151",
              boxShadow: "0 1px 2px rgba(0,0,0,0.05)",
              transition: "all 0.15s ease",
            }}
          >
            Staff Shift Assignments ({staff.length})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("GATES")}
            style={{
              padding: "6px 18px",
              borderRadius: "20px",
              fontSize: "0.9rem",
              fontWeight: 600,
              cursor: "pointer",
              border: activeTab === "GATES" ? "1px solid #be185d" : "1px solid #e5e7eb",
              background: activeTab === "GATES" ? "#be185d" : "#ffffff",
              color: activeTab === "GATES" ? "#ffffff" : "#374151",
              boxShadow: "0 1px 2px rgba(0,0,0,0.05)",
              transition: "all 0.15s ease",
            }}
          >
            Entry Gates & Rules ({gatesList.length})
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("REPORT")}
            style={{
              padding: "6px 18px",
              borderRadius: "20px",
              fontSize: "0.9rem",
              fontWeight: 600,
              cursor: "pointer",
              border: activeTab === "REPORT" ? "1px solid #be185d" : "1px solid #e5e7eb",
              background: activeTab === "REPORT" ? "#be185d" : "#ffffff",
              color: activeTab === "REPORT" ? "#ffffff" : "#374151",
              boxShadow: "0 1px 2px rgba(0,0,0,0.05)",
              transition: "all 0.15s ease",
            }}
          >
            Attendance Report
          </button>
        </div>

        {loading && <div className="tb-loading">Loading check-in operations data...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && (
          <>
            {/* ================= TAB 1: STAFF SHIFT ASSIGNMENTS ================= */}
            {activeTab === "OPERATIONS" && (
              <div className="rp-staff-grid">
                {/* Column 1: Assigned Staff Members */}
                <div className="rp-section" style={{ background: "#ffffff", padding: "20px", borderRadius: "12px", border: "1px solid #e5e7eb" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
                    <div>
                      <h2 style={{ margin: "0 0 4px 0", fontSize: "1.2rem", fontWeight: 700, color: "#0f172a" }}>
                        On-Duty Staff & Gate Schedules ({staff.length})
                      </h2>
                      <p style={{ margin: 0, color: "#475569", fontSize: "0.85rem" }}>
                        Staff can only scan QR codes during their designated shift hours and at assigned gates.
                      </p>
                    </div>
                  </div>

                  {staff.length === 0 ? (
                    <div className="tb-empty" style={{ padding: "32px 16px", background: "#f8fafc", border: "1px dashed #cbd5e1", borderRadius: "8px", color: "#475569" }}>
                      No staff members assigned yet. Search and assign staff from the right panel.
                    </div>
                  ) : (
                    <div className="od-table-wrap">
                      <table className="ow-table">
                        <thead>
                          <tr>
                            <th>Staff Member</th>
                            <th>Assigned Gate</th>
                            <th>Shift Operational Hours</th>
                            <th>Role Permissions & Duties</th>
                            <th style={{ textAlign: "right" }}>Actions</th>
                          </tr>
                        </thead>
                        <tbody>
                          {staff.map((member) => {
                            const gateCfg = findGateConfig(member.gateName);
                            const onDuty = isShiftActiveNow(member.shiftStartTime, member.shiftEndTime, member.shiftDate, member.shiftEndDate);
                            const sDate = member.shiftDate ? String(member.shiftDate).substring(0, 10) : "";
                            const eDate = member.shiftEndDate ? String(member.shiftEndDate).substring(0, 10) : sDate;
                            const sTime = member.shiftStartTime ? String(member.shiftStartTime).substring(0, 5) : "";
                            const eTime = member.shiftEndTime ? String(member.shiftEndTime).substring(0, 5) : "";
                            const fmtDate = (dStr) => {
                              if (!dStr) return "";
                              const parts = dStr.split("-");
                              if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`;
                              return new Date(dStr).toLocaleDateString("vi-VN");
                            };
                            const fromDisplay = sDate && sTime ? `${fmtDate(sDate)} ${sTime}` : sTime;
                            const toDisplay = eDate && eTime ? `${fmtDate(eDate)} ${eTime}` : eTime;

                            return (
                              <tr key={member.staffUserId}>
                                <td>
                                  <div className="ow-td-title">{member.fullName}</div>
                                  <div className="ow-td-sub">{member.email}</div>
                                </td>
                                <td>
                                  <div style={{ fontWeight: 600, color: "#111827" }}>
                                    {(member.canCheckin ?? true) ? (member.gateName || "Main Gate") : <span style={{ color: "#94a3b8", fontWeight: 400 }}>No Gate (Non-checkin)</span>}
                                  </div>
                                  {(member.canCheckin ?? true) && (gateCfg?.allowedTicketTypeList && gateCfg.allowedTicketTypeList.length > 0 ? (
                                    <div style={{ fontSize: "0.78rem", color: "#0369a1", marginTop: "2px", fontWeight: 500 }}>
                                      Allowed: {gateCfg.allowedTicketTypeList.join(", ")}
                                    </div>
                                  ) : (
                                    <div style={{ fontSize: "0.78rem", color: "#9ca3af", marginTop: "2px" }}>
                                      All Ticket Types
                                    </div>
                                  ))}
                                </td>
                                <td>
                                  <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
                                    <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                                      <span
                                        style={{
                                          fontSize: "0.75rem",
                                          padding: "2px 6px",
                                          borderRadius: "4px",
                                          fontWeight: 600,
                                          background: onDuty ? "#ecfdf5" : "#fef2f2",
                                          color: onDuty ? "#065f46" : "#991b1b",
                                          border: `1px solid ${onDuty ? "#a7f3d0" : "#fecaca"}`,
                                        }}
                                      >
                                        {onDuty ? "ON SHIFT" : "OFF SHIFT"}
                                      </span>
                                    </div>
                                    {sTime && eTime ? (
                                      <>
                                        <div style={{ fontSize: "0.82rem", color: "#0f172a" }}>
                                          <span style={{ color: "#64748b", fontWeight: 600 }}>From:</span>{" "}
                                          <strong>{fromDisplay}</strong>
                                        </div>
                                        <div style={{ fontSize: "0.82rem", color: "#0f172a" }}>
                                          <span style={{ color: "#64748b", fontWeight: 600 }}>To:</span>{" "}
                                          <strong>{toDisplay}</strong>
                                        </div>
                                      </>
                                    ) : (
                                      <div style={{ fontSize: "0.82rem", color: "#64748b" }}>All Day / Unset</div>
                                    )}
                                  </div>
                                </td>
                                <td>
                                  <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                                    {/* Role 1: Check-in Staff */}
                                    <label style={{ display: "flex", gap: 8, alignItems: "flex-start", cursor: "pointer", fontSize: "0.8rem" }}>
                                      <input
                                        type="checkbox"
                                        checked={!!(member.canCheckin || member.canManageIncidents)}
                                        disabled={busy}
                                        onChange={() =>
                                          toggleStaffRole(
                                            member,
                                            "CHECKIN_STAFF",
                                            !!(member.canCheckin || member.canManageIncidents)
                                          )
                                        }
                                        style={{ width: "14px", height: "14px", marginTop: "2px", padding: 0, accentColor: "#d61f6e" }}
                                      />
                                      <div style={{ display: "flex", flexDirection: "column" }}>
                                        <span>
                                          <strong>Check-in Staff:</strong>{" "}
                                          {member.canCheckin || member.canManageIncidents ? (
                                            <strong style={{ color: "#059669" }}>Allowed</strong>
                                          ) : (
                                            <span style={{ color: "#9ca3af" }}>No</span>
                                          )}
                                        </span>
                                        <span style={{ fontSize: "0.72rem", color: "#64748b" }}>
                                          Gate Assignment, QR/Manual/Offline Check-in, On-site Incidents
                                        </span>
                                      </div>
                                    </label>

                                    {/* Role 2: Security/Support Staff */}
                                    <label style={{ display: "flex", gap: 8, alignItems: "flex-start", cursor: "pointer", fontSize: "0.8rem" }}>
                                      <input
                                        type="checkbox"
                                        checked={!!member.canManageSupportCases}
                                        disabled={busy}
                                        onChange={() =>
                                          toggleStaffRole(
                                            member,
                                            "SECURITY_SUPPORT_STAFF",
                                            !!member.canManageSupportCases
                                          )
                                        }
                                        style={{ width: "14px", height: "14px", marginTop: "2px", padding: 0, accentColor: "#d61f6e" }}
                                      />
                                      <div style={{ display: "flex", flexDirection: "column" }}>
                                        <span>
                                          <strong>Security/Support Staff:</strong>{" "}
                                          {member.canManageSupportCases ? (
                                            <strong style={{ color: "#059669" }}>Allowed</strong>
                                          ) : (
                                            <span style={{ color: "#9ca3af" }}>No</span>
                                          )}
                                        </span>
                                        <span style={{ fontSize: "0.72rem", color: "#64748b" }}>
                                          Support Cases (Create/View/Resolve), Fraud Cases & Risk Decisions
                                        </span>
                                      </div>
                                    </label>

                                    {/* Role 3: Ticketing/Operations Staff */}
                                    <label style={{ display: "flex", gap: 8, alignItems: "flex-start", cursor: "pointer", fontSize: "0.8rem" }}>
                                      <input
                                        type="checkbox"
                                        checked={!!member.canReviewReturns}
                                        disabled={busy}
                                        onChange={() =>
                                          toggleStaffRole(
                                            member,
                                            "TICKETING_OPS_STAFF",
                                            !!member.canReviewReturns
                                          )
                                        }
                                        style={{ width: "14px", height: "14px", marginTop: "2px", padding: 0, accentColor: "#d61f6e" }}
                                      />
                                      <div style={{ display: "flex", flexDirection: "column" }}>
                                        <span>
                                          <strong>Ticketing/Operations Staff:</strong>{" "}
                                          {member.canReviewReturns ? (
                                            <strong style={{ color: "#059669" }}>Allowed</strong>
                                          ) : (
                                            <span style={{ color: "#9ca3af" }}>No</span>
                                          )}
                                        </span>
                                        <span style={{ fontSize: "0.72rem", color: "#64748b" }}>
                                          Process Full/Partial Refund, View Seat/Ticket Status History
                                        </span>
                                      </div>
                                    </label>
                                  </div>
                                </td>
                                <td style={{ textAlign: "right" }}>
                                  <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end" }}>
                                    <button
                                      type="button"
                                      className="ow-link"
                                      disabled={busy}
                                      onClick={() => openEditShiftModal(member)}
                                      style={{ color: "#2563eb", fontWeight: 600 }}
                                    >
                                      Edit Shift
                                    </button>
                                    <button
                                      type="button"
                                      className="ow-link ow-link-danger"
                                      disabled={busy}
                                      onClick={() => unassign(member)}
                                    >
                                      Remove
                                    </button>
                                  </div>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>

                {/* Column 2: Assign Candidate Staff */}
                <div className="rp-section" style={{ background: "#ffffff", padding: "20px", borderRadius: "12px", border: "1px solid #e5e7eb" }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
                    <h2 style={{ margin: 0, fontSize: "1.2rem", fontWeight: 700, color: "#0f172a" }}>Assign Staff to Gate</h2>
                    <Link to="/organizer/staff" style={{ fontSize: "0.85rem", color: "#2563eb", textDecoration: "none", fontWeight: 600 }}>
                      + Manage Accounts
                    </Link>
                  </div>

                  <div className="cmp-candidate-box">
                    <label style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, color: "#334155", marginBottom: "6px" }}>
                      Search candidate by name or email
                    </label>
                    <div style={{ display: "flex", gap: "10px" }}>
                      <input
                        type="text"
                        placeholder="Search staff candidate..."
                        value={query}
                        onChange={(event) => setQuery(event.target.value)}
                        onKeyDown={(event) => event.key === "Enter" && searchCandidates()}
                        style={{
                          flex: 1,
                          padding: "8px 12px",
                          borderRadius: "8px",
                          border: "1px solid #cbd5e1",
                          fontSize: "0.9rem",
                          background: "#ffffff",
                          color: "#0f172a",
                          outline: "none"
                        }}
                      />
                      <button
                        type="button"
                        disabled={busy}
                        onClick={searchCandidates}
                        style={{
                          padding: "8px 20px",
                          borderRadius: "8px",
                          fontWeight: 600,
                          fontSize: "0.9rem",
                          background: "linear-gradient(135deg, #d61f6e, #be185d)",
                          color: "#ffffff",
                          border: "none",
                          cursor: "pointer",
                          boxShadow: "0 1px 3px rgba(190, 24, 93, 0.25)"
                        }}
                      >
                        Search
                      </button>
                    </div>
                  </div>

                  {searched && candidates.length === 0 && (
                    <div className="tb-empty" style={{ padding: "32px 16px", background: "#f8fafc", border: "1px dashed #cbd5e1", borderRadius: "8px", color: "#475569" }}>
                      No staff accounts found. Create new staff accounts in the{" "}
                      <Link to="/organizer/staff"><strong>Staff Directory</strong></Link>.
                    </div>
                  )}

                  {candidates.length > 0 && (
                    <div className="od-table-wrap">
                      <table className="ow-table">
                        <thead>
                          <tr>
                            <th>Candidate</th>
                            <th>Assign Parameters</th>
                          </tr>
                        </thead>
                        <tbody>
                          {candidates.map((candidate) => {
                            const availableGates = gatesList.length > 0
                              ? gatesList.map((g) => g.gateName)
                              : ["Main Gate", "Gate A", "Gate B", "Gate VIP"];
                            const currentChosenGate = gateByStaff[candidate.userId] || availableGates[0];
                            const currentEv = eventsList.find((e) => (e.eventId || e.id) === Number(effectiveId));
                            const defaultStart = currentEv?.startsAt ? toDateTimeLocal(currentEv.startsAt) : "";
                            const defaultEnd = currentEv?.endsAt
                              ? toDateTimeLocal(currentEv.endsAt)
                              : (currentEv?.startsAt ? toDateTimeLocal(new Date(new Date(currentEv.startsAt).getTime() + 4 * 3600000)) : "");

                            const currentStartDt = shiftStartDateTimeByStaff[candidate.userId] ?? defaultStart;
                            const currentEndDt = shiftEndDateTimeByStaff[candidate.userId] ?? defaultEnd;
                            const isCheckin = checkinByStaff[candidate.userId] ?? true;
                            const canReview = !!reviewByStaff[candidate.userId];
                            const canIncidents = !!incidentsByStaff[candidate.userId];
                            const canSupport = !!supportByStaff[candidate.userId];
                            const gateCfg = findGateConfig(currentChosenGate);

                            return (
                              <tr key={candidate.userId}>
                                <td>
                                  <div className="ow-td-title">{candidate.fullName}</div>
                                  <div className="ow-td-sub">{candidate.email}</div>
                                </td>
                                <td>
                                  {candidate.isAssigned ? (
                                    <span className="rp-badge rp-badge-active">Already Assigned</span>
                                  ) : (
                                    <div style={{ display: "flex", flexDirection: "column", gap: "10px", alignItems: "flex-start", width: "100%", maxWidth: "420px" }}>
                                      {/* 3 Staff Roles Selection */}
                                      <div style={{ display: "flex", flexDirection: "column", gap: "8px", width: "100%", padding: "10px 12px", background: "#f8fafc", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                                        <span style={{ fontSize: "0.8rem", fontWeight: 700, color: "#0f172a" }}>Select Staff Role(s):</span>
                                        <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                                          {/* 1. Check-in Staff */}
                                          <label style={{ display: "flex", gap: "8px", alignItems: "flex-start", fontSize: "0.8rem", cursor: "pointer", color: isCheckin ? "#0f172a" : "#475569", padding: "6px 8px", borderRadius: "6px", background: isCheckin ? "#fdf2f8" : "#ffffff", border: `1px solid ${isCheckin ? "#f472b6" : "#e2e8f0"}` }}>
                                            <input
                                              type="checkbox"
                                              checked={isCheckin}
                                              onChange={(e) => setCheckinByStaff({ ...checkinByStaff, [candidate.userId]: e.target.checked })}
                                              style={{ width: "15px", height: "15px", marginTop: "2px", padding: 0, accentColor: "#d61f6e" }}
                                            />
                                            <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                                              <span style={{ fontWeight: 700, color: "#0f172a" }}>1. Check-in Staff</span>
                                              <span style={{ fontSize: "0.73rem", color: "#475569", lineHeight: 1.35 }}>
                                                View Gate Assignment, Scan Dynamic QR / Manual Check-in / Lookup Ticket / Scan History, Offline Check-in & Sync, Report & Escalate On-site Incidents
                                              </span>
                                            </div>
                                          </label>

                                          {/* 2. Security/Support Staff */}
                                          <label style={{ display: "flex", gap: "8px", alignItems: "flex-start", fontSize: "0.8rem", cursor: "pointer", color: canSupport ? "#0f172a" : "#475569", padding: "6px 8px", borderRadius: "6px", background: canSupport ? "#fdf2f8" : "#ffffff", border: `1px solid ${canSupport ? "#f472b6" : "#e2e8f0"}` }}>
                                            <input
                                              type="checkbox"
                                              checked={canSupport}
                                              onChange={(e) => setSupportByStaff({ ...supportByStaff, [candidate.userId]: e.target.checked })}
                                              style={{ width: "15px", height: "15px", marginTop: "2px", padding: 0, accentColor: "#d61f6e" }}
                                            />
                                            <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                                              <span style={{ fontWeight: 700, color: "#0f172a" }}>2. Security/Support Staff</span>
                                              <span style={{ fontSize: "0.73rem", color: "#475569", lineHeight: 1.35 }}>
                                                View / Create / Resolve Support Cases, View & Resolve Fraud Cases, View Risk Decision Details
                                              </span>
                                            </div>
                                          </label>

                                          {/* 3. Ticketing/Operations Staff */}
                                          <label style={{ display: "flex", gap: "8px", alignItems: "flex-start", fontSize: "0.8rem", cursor: "pointer", color: canReview ? "#0f172a" : "#475569", padding: "6px 8px", borderRadius: "6px", background: canReview ? "#fdf2f8" : "#ffffff", border: `1px solid ${canReview ? "#f472b6" : "#e2e8f0"}` }}>
                                            <input
                                              type="checkbox"
                                              checked={canReview}
                                              onChange={(e) => setReviewByStaff({ ...reviewByStaff, [candidate.userId]: e.target.checked })}
                                              style={{ width: "15px", height: "15px", marginTop: "2px", padding: 0, accentColor: "#d61f6e" }}
                                            />
                                            <div style={{ display: "flex", flexDirection: "column", gap: "2px" }}>
                                              <span style={{ fontWeight: 700, color: "#0f172a" }}>3. Ticketing/Operations Staff</span>
                                              <span style={{ fontSize: "0.73rem", color: "#475569", lineHeight: 1.35 }}>
                                                Process Full/Partial Refund, View Seat/Ticket Status History
                                              </span>
                                            </div>
                                          </label>
                                        </div>
                                      </div>

                                      {/* Gate & Shift time parameters: ONLY visible if Check-in is selected */}
                                      {isCheckin ? (
                                        <div style={{ display: "flex", flexDirection: "column", gap: "8px", width: "100%", padding: "10px", background: "#fdf2f8", borderRadius: "8px", border: "1px solid #fbcfe8" }}>
                                          {/* Gate selection */}
                                          <div style={{ display: "flex", alignItems: "center", gap: "6px", width: "100%" }}>
                                            <span style={{ fontSize: "0.8rem", color: "#831843", width: "45px", fontWeight: 600 }}>Gate:</span>
                                            <select
                                              style={{ padding: "5px 8px", fontSize: "0.85rem", flex: 1, background: "#ffffff", color: "#0f172a", border: "1px solid #f472b6", borderRadius: "6px" }}
                                              value={currentChosenGate}
                                              onChange={(e) =>
                                                setGateByStaff({ ...gateByStaff, [candidate.userId]: e.target.value })
                                              }
                                            >
                                              {availableGates.map((gName) => (
                                                <option key={gName} value={gName}>
                                                  {gName}
                                                </option>
                                              ))}
                                            </select>
                                          </div>

                                          {/* Shift hours from/to datetime selection */}
                                          <div style={{ display: "flex", flexDirection: "column", gap: "6px", width: "100%" }}>
                                            <div style={{ display: "flex", alignItems: "center", gap: "6px", width: "100%" }}>
                                              <span style={{ fontSize: "0.8rem", color: "#831843", width: "45px", fontWeight: 600 }}>From:</span>
                                              <input
                                                type="datetime-local"
                                                value={currentStartDt}
                                                onChange={(e) =>
                                                  setShiftStartDateTimeByStaff({ ...shiftStartDateTimeByStaff, [candidate.userId]: e.target.value })
                                                }
                                                style={{ padding: "5px 8px", fontSize: "0.82rem", borderRadius: "6px", border: "1px solid #f472b6", flex: 1, background: "#ffffff", color: "#0f172a" }}
                                              />
                                            </div>
                                            <div style={{ display: "flex", alignItems: "center", gap: "6px", width: "100%" }}>
                                              <span style={{ fontSize: "0.8rem", color: "#831843", width: "45px", fontWeight: 600 }}>To:</span>
                                              <input
                                                type="datetime-local"
                                                value={currentEndDt}
                                                onChange={(e) =>
                                                  setShiftEndDateTimeByStaff({ ...shiftEndDateTimeByStaff, [candidate.userId]: e.target.value })
                                                }
                                                style={{ padding: "5px 8px", fontSize: "0.82rem", borderRadius: "6px", border: "1px solid #f472b6", flex: 1, background: "#ffffff", color: "#0f172a" }}
                                              />
                                            </div>
                                          </div>

                                          {gateCfg?.allowedTicketTypeList && gateCfg.allowedTicketTypeList.length > 0 && (
                                            <div style={{ fontSize: "0.75rem", color: "#9d174d", fontWeight: 500 }}>
                                              Tickets: {gateCfg.allowedTicketTypeList.join(", ")}
                                            </div>
                                          )}

                                          {currentEv?.startsAt && (
                                            <small style={{ color: "#64748b", fontSize: "0.72rem" }}>
                                              Concert schedule: {formatEventDateTime(currentEv.startsAt)}
                                              {currentEv.endsAt ? ` – ${formatEventDateTime(currentEv.endsAt)}` : ""}
                                            </small>
                                          )}

                                          {(() => {
                                            const shiftWarning = getShiftScheduleWarning(currentEv, currentStartDt, currentEndDt);
                                            if (!shiftWarning) return null;
                                            return (
                                              <div
                                                style={{
                                                  marginTop: "4px",
                                                  padding: "8px 10px",
                                                  background: "#fffbeb",
                                                  border: "1px solid #f59e0b",
                                                  borderRadius: "6px",
                                                  color: "#92400e",
                                                  fontSize: "0.76rem",
                                                  lineHeight: "1.4",
                                                  fontWeight: 600,
                                                }}
                                              >
                                                {shiftWarning}
                                              </div>
                                            );
                                          })()}
                                        </div>
                                      ) : (
                                        <div style={{ fontSize: "0.78rem", color: "#64748b", fontStyle: "italic", background: "#f8fafc", padding: "8px 10px", borderRadius: "6px", border: "1px dashed #cbd5e1", width: "100%" }}>
                                          Non-checkin staff. No gate or scanning schedule required.
                                        </div>
                                      )}

                                      <button
                                        type="button"
                                        style={{
                                          fontSize: "0.85rem",
                                          padding: "7px 16px",
                                          borderRadius: "6px",
                                          fontWeight: 600,
                                          background: "linear-gradient(135deg, #d61f6e, #be185d)",
                                          color: "#ffffff",
                                          border: "none",
                                          cursor: "pointer",
                                          alignSelf: "flex-end",
                                          boxShadow: "0 1px 3px rgba(190, 24, 93, 0.3)"
                                        }}
                                        disabled={busy}
                                        onClick={() => assign(candidate)}
                                      >
                                        {isCheckin ? "Assign to Gate" : "Assign Staff"}
                                      </button>
                                    </div>
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* ================= TAB 2: ENTRY GATES & TICKET RULES ================= */}
            {activeTab === "GATES" && (
              <div className="rp-section" style={{ background: "#ffffff", padding: "24px", borderRadius: "12px", border: "1px solid #e5e7eb" }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
                  <div>
                    <h2 style={{ margin: "0 0 4px 0", fontSize: "1.3rem", fontWeight: 700, color: "#0f172a" }}>
                      Event Gates & Ticket Type Routing Policies ({gatesList.length})
                    </h2>
                    <p style={{ margin: 0, color: "#475569", fontSize: "0.9rem" }}>
                      Designate specific physical gates and enforce admission ticket types (e.g. VIP Gate only accepts VIP Diamond, General Gate accepts GA/Standard).
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={openGateModal}
                    style={{
                      padding: "8px 18px",
                      borderRadius: "8px",
                      fontWeight: 600,
                      fontSize: "0.9rem",
                      background: "linear-gradient(135deg, #d61f6e, #be185d)",
                      color: "#ffffff",
                      border: "none",
                      cursor: "pointer",
                      boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)",
                      whiteSpace: "nowrap"
                    }}
                  >
                    + Add New Gate
                  </button>
                </div>

                {gatesList.length === 0 ? (
                  <div style={{ padding: "32px", textAlign: "center", background: "#f8fafc", border: "1px dashed #cbd5e1", borderRadius: "8px", color: "#475569" }}>
                    No designated gates configured yet. Click <strong>+ Add New Gate</strong> to establish VIP and Standard admission channels!
                  </div>
                ) : (
                  <div className="od-table-wrap">
                    <table className="ow-table">
                      <thead>
                        <tr>
                          <th>Gate Name & Identifier</th>
                          <th>Permitted Ticket Types (Enforced Online & Offline)</th>
                          <th>Location / Operational Note</th>
                          <th style={{ textAlign: "right" }}>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {gatesList.map((gate) => (
                          <tr key={gate.gateId}>
                            <td>
                              <strong style={{ color: "#111827", fontSize: "1rem" }}>{gate.gateName}</strong>
                            </td>
                            <td>
                              <div style={{ display: "flex", flexWrap: "wrap", gap: "6px" }}>
                                {gate.allowedTicketTypeList && gate.allowedTicketTypeList.length > 0 ? (
                                  gate.allowedTicketTypeList.map((type) => {
                                    const isVip = type.toLowerCase().includes("vip");
                                    return (
                                      <span
                                        key={type}
                                        style={{
                                          background: isVip ? "#fef3c7" : "#e0f2fe",
                                          color: isVip ? "#92400e" : "#0369a1",
                                          border: `1px solid ${isVip ? "#fde68a" : "#bae6fd"}`,
                                          padding: "3px 8px",
                                          borderRadius: "6px",
                                          fontSize: "0.82rem",
                                          fontWeight: 600,
                                        }}
                                      >
                                        {type}
                                      </span>
                                    );
                                  })
                                ) : (
                                  <span style={{ background: "#f3f4f6", color: "#4b5563", padding: "2px 8px", borderRadius: "6px", fontSize: "0.82rem" }}>
                                    All Ticket Types (Mixed Entry)
                                  </span>
                                )}
                              </div>
                            </td>
                            <td style={{ color: "#4b5563" }}>{gate.description || "—"}</td>
                            <td style={{ textAlign: "right" }}>
                              <button
                                type="button"
                                className="ow-link ow-link-danger"
                                onClick={() => handleDeleteGate(gate)}
                              >
                                Delete Gate
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {/* ================= TAB 3: ADMISSION ATTENDANCE REPORT ================= */}
            {activeTab === "REPORT" && (
              <div style={{ background: "#ffffff", padding: "28px", borderRadius: "14px", border: "1px solid #e2e8f0", boxShadow: "0 1px 3px rgba(0,0,0,0.05)" }}>
                <div style={{ marginBottom: "20px", display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "12px" }}>
                  <div>
                    <h2 style={{ margin: "0 0 4px 0", fontSize: "1.35rem", fontWeight: 700, color: "#111827" }}>
                      Attendance & Admission Summary
                    </h2>
                    <p style={{ margin: 0, color: "#64748b", fontSize: "0.9rem" }}>
                      Live snapshot of check-in throughput and venue gate attendance.
                    </p>
                  </div>
                  <button
                    type="button"
                    disabled={busy || !effectiveId}
                    onClick={exportCsv}
                    style={{
                      padding: "9px 18px",
                      borderRadius: "8px",
                      fontWeight: 600,
                      fontSize: "0.9rem",
                      background: "linear-gradient(135deg, #d61f6e, #be185d)",
                      color: "#ffffff",
                      border: "none",
                      cursor: busy || !effectiveId ? "not-allowed" : "pointer",
                      boxShadow: "0 2px 6px rgba(190, 24, 93, 0.3)"
                    }}
                  >
                    Export Check-in Report (CSV)
                  </button>
                </div>

                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "16px", marginTop: "16px" }}>
                  <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "12px", padding: "20px", boxShadow: "0 1px 3px rgba(0,0,0,0.03)" }}>
                    <div style={{ fontSize: "0.82rem", fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: "8px" }}>
                      Total Issued Tickets
                    </div>
                    <div style={{ fontSize: "2rem", fontWeight: 800, color: "#0f172a" }}>
                      {summary?.totalTickets ?? 0}
                    </div>
                  </div>
                  <div style={{ background: "#ffffff", border: "1px solid #fbcfe8", borderRadius: "12px", padding: "20px", boxShadow: "0 2px 6px rgba(190, 24, 93, 0.08)" }}>
                    <div style={{ fontSize: "0.82rem", fontWeight: 600, color: "#be185d", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: "8px" }}>
                      Checked-in Attendance
                    </div>
                    <div style={{ fontSize: "2rem", fontWeight: 800, color: "#be185d" }}>
                      {summary?.checkedIn ?? 0}
                    </div>
                  </div>
                  <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "12px", padding: "20px", boxShadow: "0 1px 3px rgba(0,0,0,0.03)" }}>
                    <div style={{ fontSize: "0.82rem", fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: "8px" }}>
                      Not Checked-in
                    </div>
                    <div style={{ fontSize: "2rem", fontWeight: 800, color: "#0f172a" }}>
                      {summary?.notCheckedIn ?? 0}
                    </div>
                  </div>
                  <div style={{ background: "#ffffff", border: "1px solid #e2e8f0", borderRadius: "12px", padding: "20px", boxShadow: "0 1px 3px rgba(0,0,0,0.03)" }}>
                    <div style={{ fontSize: "0.82rem", fontWeight: 600, color: "#64748b", textTransform: "uppercase", letterSpacing: "0.04em", marginBottom: "8px" }}>
                      Refunded / Returned
                    </div>
                    <div style={{ fontSize: "2rem", fontWeight: 800, color: "#0f172a" }}>
                      {summary?.returned ?? 0}
                    </div>
                  </div>
                </div>

                <div style={{ background: "#f1f5f9", height: "10px", borderRadius: "8px", overflow: "hidden", marginTop: "28px" }}>
                  <div style={{ width: `${summary?.checkInRatePercent ?? 0}%`, height: "100%", background: "linear-gradient(90deg, #d61f6e, #be185d)", borderRadius: "8px", transition: "width 0.4s ease" }}></div>
                </div>
                <p style={{ marginTop: "10px", color: "#475569", fontSize: "0.92rem", fontWeight: 500 }}>
                  <strong>{summary?.checkInRatePercent ?? 0}%</strong> of valid ticket holders have arrived and scanned at venue gates.
                </p>

                {Array.isArray(checkin?.byTicketType) && checkin.byTicketType.length > 0 && (
                  <div style={{ marginTop: "24px" }}>
                    <h3 style={{ margin: "0 0 12px 0", fontSize: "1.05rem", fontWeight: 700, color: "#0f172a" }}>
                      Check-in Breakdown by Ticket Type
                    </h3>
                    <div className="od-table-wrap">
                      <table className="ow-table">
                        <thead>
                          <tr>
                            <th>Ticket Type</th>
                            <th>Total Valid Tickets</th>
                            <th>Checked-in</th>
                            <th>Not Checked-in</th>
                            <th>Check-in Rate</th>
                          </tr>
                        </thead>
                        <tbody>
                          {checkin.byTicketType.map((row) => (
                            <tr key={row.ticketTypeId}>
                              <td>
                                <strong>{row.ticketTypeName}</strong>
                              </td>
                              <td>{row.totalTickets}</td>
                              <td style={{ color: "#059669", fontWeight: 700 }}>{row.checkedIn}</td>
                              <td>{row.notCheckedIn}</td>
                              <td>
                                <strong>{row.checkInRatePercent}%</strong>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>

      {/* ================= MODAL: CREATE NEW GATE ================= */}
      {isGateModalOpen && (
        <div className="osp-modal-overlay" onClick={() => setIsGateModalOpen(false)}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: "560px" }}>
            <div className="osp-modal-header">
              <h2>Configure Entry Gate</h2>
              <button type="button" className="osp-modal-close" onClick={() => setIsGateModalOpen(false)}>
                ×
              </button>
            </div>
            <form onSubmit={handleSaveGate}>
              <div className="osp-modal-body">
                {gateFormError && <div className="tb-error" style={{ marginBottom: "16px" }}>{gateFormError}</div>}
                
                <div className="osp-form-group">
                  <label>Gate Name *</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. Gate A - VIP Lounge, Gate B - General Standing"
                    value={gateFormData.gateName}
                    onChange={(e) => setGateFormData({ ...gateFormData, gateName: e.target.value })}
                  />
                </div>

                <div className="osp-form-group">
                  <label>Location / Gate Directions (Optional)</label>
                  <input
                    type="text"
                    placeholder="e.g. North Wing Entrance, 2nd Floor Staircase"
                    value={gateFormData.description}
                    onChange={(e) => setGateFormData({ ...gateFormData, description: e.target.value })}
                  />
                </div>

                <div className="osp-form-group">
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
                    <label style={{ margin: 0, fontWeight: 600 }}>Permitted Ticket Types for this Gate</label>
                    {ticketTypes.length > 0 && (
                      <button
                        type="button"
                        onClick={handleSelectAllTicketTypes}
                        style={{ background: "none", border: "none", color: "#2563eb", cursor: "pointer", fontSize: "0.82rem", fontWeight: 600 }}
                      >
                        {gateFormData.selectedTicketTypes.length === ticketTypes.length ? "Deselect All" : "Select All"}
                      </button>
                    )}
                  </div>
                  
                  {ticketTypes.length === 0 ? (
                    <p style={{ fontSize: "0.85rem", color: "#6b7280" }}>
                      No ticket types created for this event yet. Gate will default to accepting all tickets.
                    </p>
                  ) : (
                    <div style={{ display: "flex", flexDirection: "column", gap: "8px", maxHeight: "180px", overflowY: "auto", padding: "10px", background: "#f9fafb", borderRadius: "8px", border: "1px solid #e5e7eb" }}>
                      {ticketTypes.map((tt) => {
                        const isChecked = gateFormData.selectedTicketTypes.includes(tt.typeName);
                        return (
                          <label
                            key={tt.ticketTypeId || tt.typeName}
                            style={{
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "space-between",
                              gap: "12px",
                              padding: "8px 12px",
                              background: isChecked ? "#fdf2f8" : "#ffffff",
                              border: `1px solid ${isChecked ? "#f472b6" : "#e2e8f0"}`,
                              borderRadius: "6px",
                              cursor: "pointer",
                              fontSize: "0.9rem",
                              transition: "all 0.15s ease"
                            }}
                          >
                            <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                              <input
                                type="checkbox"
                                checked={isChecked}
                                onChange={() => toggleTicketTypeSelection(tt.typeName)}
                                style={{ width: "16px", minWidth: "16px", height: "16px", margin: 0, padding: 0, cursor: "pointer", accentColor: "#d61f6e" }}
                              />
                              <span style={{ fontWeight: 600, color: "#1e293b" }}>{tt.typeName}</span>
                            </div>
                            {tt.price != null && (
                              <span style={{ color: "#64748b", fontSize: "0.82rem", fontWeight: 500 }}>
                                ({Number(tt.price).toLocaleString()} VND)
                              </span>
                            )}
                          </label>
                        );
                      })}
                    </div>
                  )}
                  <small style={{ color: "#6b7280", display: "block", marginTop: "6px" }}>
                    If no ticket types are checked, this gate will function as a mixed gate accepting all tickets.
                  </small>
                </div>
              </div>
              <div className="osp-modal-footer">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={() => setIsGateModalOpen(false)}
                  disabled={savingGate}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={savingGate}
                  style={{
                    padding: "8px 20px",
                    borderRadius: "8px",
                    fontWeight: 600,
                    fontSize: "0.9rem",
                    background: "linear-gradient(135deg, #d61f6e, #be185d)",
                    color: "#ffffff",
                    border: "none",
                    cursor: "pointer",
                    boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)"
                  }}
                >
                  {savingGate ? "Saving..." : "Save Gate"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ================= MODAL: EDIT STAFF SHIFT ================= */}
      {editingStaffShift && (
        <div className="osp-modal-overlay" onClick={() => setEditingStaffShift(null)}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: "480px" }}>
            <div className="osp-modal-header">
              <h2>Edit Shift: {editingStaffShift.fullName}</h2>
              <button type="button" className="osp-modal-close" onClick={() => setEditingStaffShift(null)}>
                ×
              </button>
            </div>
            <form onSubmit={handleSaveShift}>
              <div className="osp-modal-body">
                <div className="osp-form-group">
                  <label>Staff Email</label>
                  <input type="text" disabled value={editingStaffShift.email} style={{ background: "#f3f4f6" }} />
                </div>

                <div className="osp-form-group">
                  <label>Assigned Gate</label>
                  <select
                    className="ow-field"
                    value={shiftFormData.gateName}
                    onChange={(e) => setShiftFormData({ ...shiftFormData, gateName: e.target.value })}
                  >
                    {gatesList.length > 0 ? (
                      gatesList.map((g) => (
                        <option key={g.gateId} value={g.gateName}>
                          {g.gateName}
                        </option>
                      ))
                    ) : (
                      <option value="Main Gate">Main Gate</option>
                    )}
                  </select>
                </div>

                <div style={{ display: "flex", flexDirection: "column", gap: "12px", marginTop: "4px" }}>
                  <div className="osp-form-group">
                    <label>From (Shift Start Date & Time) *</label>
                    <input
                      type="datetime-local"
                      required
                      value={shiftFormData.shiftStartDateTime || ""}
                      onChange={(e) => setShiftFormData({ ...shiftFormData, shiftStartDateTime: e.target.value })}
                    />
                  </div>
                  <div className="osp-form-group">
                    <label>To (Shift End Date & Time) *</label>
                    <input
                      type="datetime-local"
                      required
                      value={shiftFormData.shiftEndDateTime || ""}
                      onChange={(e) => setShiftFormData({ ...shiftFormData, shiftEndDateTime: e.target.value })}
                    />
                  </div>
                </div>
                <small style={{ color: "#6b7280", display: "block", marginTop: "4px" }}>
                  Staff will only be allowed to scan tickets during this shift window.
                </small>

                {(() => {
                  const currentEv = eventsList.find((e) => (e.eventId || e.id) === Number(effectiveId));
                  const shiftWarning = getShiftScheduleWarning(currentEv, shiftFormData.shiftStartDateTime, shiftFormData.shiftEndDateTime);
                  if (!shiftWarning) return null;
                  return (
                    <div
                      style={{
                        marginTop: "12px",
                        padding: "10px 12px",
                        background: "#fffbeb",
                        border: "1px solid #f59e0b",
                        borderRadius: "8px",
                        color: "#92400e",
                        fontSize: "0.82rem",
                        lineHeight: "1.45",
                        fontWeight: 600,
                      }}
                    >
                      {shiftWarning}
                    </div>
                  );
                })()}
              </div>
              <div className="osp-modal-footer">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={() => setEditingStaffShift(null)}
                  disabled={savingShift}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={savingShift}
                  style={{
                    padding: "8px 20px",
                    borderRadius: "8px",
                    fontWeight: 600,
                    fontSize: "0.9rem",
                    background: "linear-gradient(135deg, #d61f6e, #be185d)",
                    color: "#ffffff",
                    border: "none",
                    cursor: "pointer",
                    boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)"
                  }}
                >
                  {savingShift ? "Saving..." : "Update Shift"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ================= MODAL: CONFIRMATION POPUP (replaces browser window.confirm) ================= */}
      {confirmModal && (
        <div className="osp-modal-overlay" onClick={() => setConfirmModal(null)} style={{ zIndex: 9999 }}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: "460px" }}>
            <div className="osp-modal-header">
              <h2 style={{ fontSize: "1.2rem", fontWeight: 700, color: "#111827", margin: 0 }}>
                {confirmModal.title}
              </h2>
              <button type="button" className="osp-modal-close" onClick={() => setConfirmModal(null)}>
                ×
              </button>
            </div>
            <div className="osp-modal-body" style={{ padding: "18px 20px" }}>
              <p style={{ margin: 0, color: "#4b5563", fontSize: "0.95rem", lineHeight: "1.55" }}>
                {confirmModal.message}
              </p>
            </div>
            <div className="osp-modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "14px 20px" }}>
              <button
                type="button"
                className="tb-btn tb-btn-outline"
                onClick={() => setConfirmModal(null)}
                style={{ padding: "8px 18px", borderRadius: "8px", border: "1px solid #d1d5db", background: "#fff", color: "#374151", cursor: "pointer", fontWeight: 600 }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={confirmModal.onConfirm}
                style={{
                  padding: "8px 20px",
                  borderRadius: "8px",
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  border: "none",
                  cursor: "pointer",
                  boxShadow: "0 2px 4px rgba(0,0,0,0.15)",
                  ...(confirmModal.confirmBtnStyle || { background: "linear-gradient(135deg, #d61f6e, #be185d)", color: "#ffffff" })
                }}
              >
                {confirmModal.confirmText || "Confirm"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Toast Notification */}
      {toast && (
        <div
          style={{
            position: "fixed",
            bottom: "24px",
            right: "24px",
            background: toast.type === "error" ? "#dc2626" : "#059669",
            color: "#fff",
            padding: "12px 20px",
            borderRadius: "8px",
            boxShadow: "0 10px 15px -3px rgba(0,0,0,0.1)",
            zIndex: 1100,
            fontSize: "0.95rem",
            fontWeight: 500,
          }}
        >
          {toast.message}
        </div>
      )}
    </OrganizerShell>
  );
}
