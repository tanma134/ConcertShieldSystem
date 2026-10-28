# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_14_1():
    # UC_14.1 Monitor Real-time Check-in Status (Organizer)
    lifelines = [
        {"name": "Organizer", "type": "actor", "cx": 100},
        {"name": "CheckinMonitorPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinMonitorRoute", "type": "standard", "cx": 1000},
        {"name": ":CheckinMonitorController", "type": "standard", "cx": 1300},
        {"name": ":CheckinMonitorService", "type": "standard", "cx": 1600},
        {"name": "EventDB", "type": "db", "cx": 1900},
        {"name": "TicketDB", "type": "db", "cx": 2200}
    ]
    b = SequenceDiagramBuilder("UC_14.1_MonitorRealTimeCheckinStatus", lifelines, 2500, 2000)
    b.add_message("1", "Open Real-time Check-in Dashboard for event", "Organizer", "CheckinMonitorPage")
    b.add_message("2", "GET /api/checkin/monitor?eventId={id}", "CheckinMonitorPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Organizer", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinMonitorPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinMonitorPage", "Organizer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/monitor", "GatewayAPI", ":CheckinMonitorRoute")
    b.add_message("6", "GetMonitorStatus(eventId, gateId, zone)", ":CheckinMonitorRoute", ":CheckinMonitorController")
    b.add_message("7", "GetMonitorStatusAsync(eventId, filter)", ":CheckinMonitorController", ":CheckinMonitorService")
    b.add_message("8", "Query event details", ":CheckinMonitorService", "EventDB")
    b.add_message("9", "Return event row (or null)", "EventDB", ":CheckinMonitorService", is_return=True)
    
    b.add_self_call("10", "Check event existence & ownership", ":CheckinMonitorService")
    f2 = b.start_opt_frame("user is not event owner", "Organizer", ":CheckinMonitorService")
    b.add_message("11.1", "Return error (UnauthorizedAccessException)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("11.2", "Return 403 Forbidden", ":CheckinMonitorController", ":CheckinMonitorRoute", is_return=True)
    b.add_message("11.3", "Return 403 Forbidden", ":CheckinMonitorRoute", "GatewayAPI", is_return=True)
    b.add_message("11.4", "Return 403 Forbidden", "GatewayAPI", "CheckinMonitorPage", is_return=True)
    b.add_message("11.5", "Display error message", "CheckinMonitorPage", "Organizer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("12", "Query ticket counts", ":CheckinMonitorService", "TicketDB")
    b.add_message("13", "Return counts", "TicketDB", ":CheckinMonitorService", is_return=True)
    b.add_message("14", "Query zone metrics", ":CheckinMonitorService", "TicketDB")
    b.add_message("15", "Return zone metrics", "TicketDB", ":CheckinMonitorService", is_return=True)
    b.add_message("16", "Query recent 10 scan logs", ":CheckinMonitorService", "TicketDB")
    b.add_message("17", "Return recent scans", "TicketDB", ":CheckinMonitorService", is_return=True)
    b.add_message("18", "Return CheckinMonitorResponseDTO", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("19", "Return 200 OK (data)", ":CheckinMonitorController", ":CheckinMonitorRoute", is_return=True)
    b.add_message("20", "Forward 200 OK", ":CheckinMonitorRoute", "GatewayAPI", is_return=True)
    b.add_message("21", "Return 200 OK", "GatewayAPI", "CheckinMonitorPage", is_return=True)
    b.add_message("22", "Render metrics & recent scans", "CheckinMonitorPage", "Organizer", is_return=True)
    return b.build_xml()

def generate_uc_14_5():
    # UC_14.5 Enable Offline Check-in Mode (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOfflineRoute", "type": "standard", "cx": 1000},
        {"name": ":TicketGovernanceController", "type": "standard", "cx": 1300},
        {"name": ":TicketGovernanceService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_14.5_EnableOfflineCheckinMode", lifelines, 2200, 1800)
    b.add_message("1", "Click 'Download Offline Manifest' button", "Staff", "CheckinScannerPage")
    b.add_message("2", "GET /api/checkin/offline-package?eventId={id}", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/offline-package", "GatewayAPI", ":CheckinOfflineRoute")
    b.add_message("6", "GetOfflinePackage(eventId)", ":CheckinOfflineRoute", ":TicketGovernanceController")
    b.add_message("7", "GetOfflinePackageAsync(eventId)", ":TicketGovernanceController", ":TicketGovernanceService")
    b.add_message("8", "Query tickets for offline cache", ":TicketGovernanceService", "TicketDB")
    b.add_message("9", "Return valid tickets rows", "TicketDB", ":TicketGovernanceService", is_return=True)
    b.add_message("10", "Return OfflinePackageResponseDTO", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("11", "Return 200 OK (data)", ":TicketGovernanceController", ":CheckinOfflineRoute", is_return=True)
    b.add_message("12", "Forward 200 OK", ":CheckinOfflineRoute", "GatewayAPI", is_return=True)
    b.add_message("13", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_self_call("14", "Save package to IndexedDB", "CheckinScannerPage")
    b.add_message("15", "Display 'Offline Mode Ready'", "CheckinScannerPage", "Staff", is_return=True)
    return b.build_xml()

def generate_uc_14_6():
    # UC_14.6 Sync Offline Check-in Records (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOfflineRoute", "type": "standard", "cx": 1000},
        {"name": ":TicketGovernanceController", "type": "standard", "cx": 1300},
        {"name": ":TicketGovernanceService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_14.6_SyncOfflineCheckinRecords", lifelines, 2200, 2200)
    b.add_message("1", "Network restored -> Click 'Sync'", "Staff", "CheckinScannerPage")
    b.add_self_call("2", "Read pending scans from IndexedDB", "CheckinScannerPage")
    b.add_message("3", "POST /api/checkin/offline-sync (dto)", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("4", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("5.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("5.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("6", "Forward POST /api/checkin/offline-sync", "GatewayAPI", ":CheckinOfflineRoute")
    b.add_message("7", "SyncOfflineScans(request)", ":CheckinOfflineRoute", ":TicketGovernanceController")
    b.add_message("8", "SyncOfflineScansAsync(request, staffId)", ":TicketGovernanceController", ":TicketGovernanceService")
    b.add_message("9", "Query current ticket status", ":TicketGovernanceService", "TicketDB")
    b.add_message("10", "Return current server status", "TicketDB", ":TicketGovernanceService", is_return=True)
    
    b.add_self_call("11", "Detect sync conflicts", ":TicketGovernanceService")
    b.add_message("12", "Update tickets status to checked-in", ":TicketGovernanceService", "TicketDB")
    b.add_message("13", "Insert offline sync conflict records", ":TicketGovernanceService", "TicketDB")
    b.add_message("14", "Insert scan logs audit trail", ":TicketGovernanceService", "TicketDB")
    b.add_message("15", "Return sync transaction result", "TicketDB", ":TicketGovernanceService", is_return=True)
    
    b.add_self_call("16", "Check save result", ":TicketGovernanceService")
    f2 = b.start_alt_frame("save failed", "save success", "Staff", ":TicketGovernanceService")
    # Branch 1: save failed
    b.add_message("17.1", "Return error (NpgsqlException)", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("17.2", "Return 500 Internal Server Error", ":TicketGovernanceController", ":CheckinOfflineRoute", is_return=True)
    b.add_message("17.3", "Return 500 Internal Server Error", ":CheckinOfflineRoute", "GatewayAPI", is_return=True)
    b.add_message("17.4", "Return 500 Internal Server Error", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("17.5", "Display error & retain local scans", "CheckinScannerPage", "Staff", is_return=True)
    
    b.split_alt_frame(f2)
    
    # Branch 2: save success
    b.add_message("18", "Return OfflineSyncResponseDTO", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("19", "Return 200 OK (data)", ":TicketGovernanceController", ":CheckinOfflineRoute", is_return=True)
    b.add_message("20", "Forward 200 OK", ":CheckinOfflineRoute", "GatewayAPI", is_return=True)
    b.add_message("21", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_self_call("22", "Clear synced scans in IndexedDB", "CheckinScannerPage")
    b.add_message("23", "Display 'Synced successfully'", "CheckinScannerPage", "Staff", is_return=True)
    b.end_alt_frame(f2)
    
    return b.build_xml()

def generate_uc_14_7():
    # UC_14.7 Resolve Offline Sync Conflicts (Organizer / Admin / Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "OfflineConflictModal", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOfflineRoute", "type": "standard", "cx": 1000},
        {"name": ":TicketGovernanceController", "type": "standard", "cx": 1300},
        {"name": ":TicketGovernanceService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_14.7_ResolveOfflineSyncConflicts", lifelines, 2200, 2200)
    b.add_message("1", "Select resolution action & Submit", "Staff", "OfflineConflictModal")
    b.add_message("2", "POST /api/checkin/conflicts/{id}/resolve (dto)", "OfflineConflictModal", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "OfflineConflictModal", is_return=True)
    b.add_message("4.2", "Display error message", "OfflineConflictModal", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/checkin/conflicts/{id}/resolve", "GatewayAPI", ":CheckinOfflineRoute")
    b.add_message("6", "ResolveOfflineConflict(id, request)", ":CheckinOfflineRoute", ":TicketGovernanceController")
    b.add_message("7", "ResolveOfflineConflictAsync(id, dto, user)", ":TicketGovernanceController", ":TicketGovernanceService")
    b.add_message("8", "Query conflict record by ID", ":TicketGovernanceService", "TicketDB")
    b.add_message("9", "Return conflict row (or null)", "TicketDB", ":TicketGovernanceService", is_return=True)
    
    b.add_self_call("10", "Check conflict existence", ":TicketGovernanceService")
    f2 = b.start_opt_frame("conflict is null", "Staff", ":TicketGovernanceService")
    b.add_message("11.1", "Return error (KeyNotFoundException)", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("11.2", "Return 404 Not Found", ":TicketGovernanceController", ":CheckinOfflineRoute", is_return=True)
    b.add_message("11.3", "Return 404 Not Found", ":CheckinOfflineRoute", "GatewayAPI", is_return=True)
    b.add_message("11.4", "Return 404 Not Found", "GatewayAPI", "OfflineConflictModal", is_return=True)
    b.add_message("11.5", "Display error message", "OfflineConflictModal", "Staff", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("12", "Update conflict resolution ('Resolved')", ":TicketGovernanceService", "TicketDB")
    b.add_message("13", "Update ticket status", ":TicketGovernanceService", "TicketDB")
    b.add_message("14", "Return update rows count", "TicketDB", ":TicketGovernanceService", is_return=True)
    
    b.add_self_call("15", "Check save result", ":TicketGovernanceService")
    f3 = b.start_alt_frame("save failed", "save success", "Staff", ":TicketGovernanceService")
    # Branch 1: save failed
    b.add_message("16.1", "Return error (NpgsqlException)", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("16.2", "Return 500 Internal Server Error", ":TicketGovernanceController", ":CheckinOfflineRoute", is_return=True)
    b.add_message("16.3", "Return 500 Internal Server Error", ":CheckinOfflineRoute", "GatewayAPI", is_return=True)
    b.add_message("16.4", "Return 500 Internal Server Error", "GatewayAPI", "OfflineConflictModal", is_return=True)
    b.add_message("16.5", "Display error message", "OfflineConflictModal", "Staff", is_return=True)
    
    b.split_alt_frame(f3)
    
    # Branch 2: save success
    b.add_message("17", "Return OfflineSyncConflictDTO", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("18", "Return 200 OK (data)", ":TicketGovernanceController", ":CheckinOfflineRoute", is_return=True)
    b.add_message("19", "Forward 200 OK", ":CheckinOfflineRoute", "GatewayAPI", is_return=True)
    b.add_message("20", "Return 200 OK", "GatewayAPI", "OfflineConflictModal", is_return=True)
    b.add_message("21", "Close modal & refresh badge", "OfflineConflictModal", "Staff", is_return=True)
    b.end_alt_frame(f3)
    
    return b.build_xml()

print("Group 4 complete.")
