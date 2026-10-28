# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_15_1():
    # UC_15.1 View Gate Assignment (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOperationsRoute", "type": "standard", "cx": 1000},
        {"name": ":CheckinMonitorController", "type": "standard", "cx": 1300},
        {"name": ":CheckinMonitorService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_15.1_ViewGateAssignment", lifelines, 2200, 1600)
    b.add_message("1", "Open Scanner interface", "Staff", "CheckinScannerPage")
    b.add_message("2", "GET /api/checkin/gate-assignment", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/gate-assignment", "GatewayAPI", ":CheckinOperationsRoute")
    b.add_message("6", "GetGateAssignment(eventId)", ":CheckinOperationsRoute", ":CheckinMonitorController")
    b.add_message("7", "GetGateAssignmentAsync(eventId, userId)", ":CheckinMonitorController", ":CheckinMonitorService")
    b.add_message("8", "Query gate assignment & shift", ":CheckinMonitorService", "TicketDB")
    b.add_message("9", "Return staff assignment rows", "TicketDB", ":CheckinMonitorService", is_return=True)
    b.add_message("10", "Return GateAssignmentDTO", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("11", "Return 200 OK (data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("12", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("13", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("14", "Display gate, zone & shift timer", "CheckinScannerPage", "Staff", is_return=True)
    return b.build_xml()

def generate_uc_15_2():
    # UC_15.2 Scan Dynamic QR Ticket (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOperationsRoute", "type": "standard", "cx": 1000},
        {"name": ":CheckinMonitorController", "type": "standard", "cx": 1300},
        {"name": ":CheckinMonitorService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_15.2_ScanDynamicQRTicket", lifelines, 2200, 2400)
    b.add_message("1", "Camera scans attendee QR code", "Staff", "CheckinScannerPage")
    b.add_message("2", "POST /api/checkin/scan-qr (dto)", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/checkin/scan-qr", "GatewayAPI", ":CheckinOperationsRoute")
    b.add_message("6", "ScanQrTicket(request)", ":CheckinOperationsRoute", ":CheckinMonitorController")
    b.add_message("7", "ScanQrTicketAsync(dto, userContext)", ":CheckinMonitorController", ":CheckinMonitorService")
    b.add_message("8", "Query ticket by code & eventId", ":CheckinMonitorService", "TicketDB")
    b.add_message("9", "Return ticket row (or null)", "TicketDB", ":CheckinMonitorService", is_return=True)
    
    b.add_self_call("10", "Validate QR ticket (signature, expiry)", ":CheckinMonitorService")
    f2 = b.start_opt_frame("ticket invalid or expired", "Staff", ":CheckinMonitorService")
    b.add_message("11.1", "Insert scan log (INVALID_QR / EXPIRED)", ":CheckinMonitorService", "TicketDB")
    b.add_message("11.2", "Return ScanResultDTO (INVALID_QR)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("11.3", "Return 200 OK (success: false, data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("11.4", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("11.5", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("11.6", "Display invalid badge & sound buzz", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_self_call("12", "Check if already checked in", ":CheckinMonitorService")
    f3 = b.start_opt_frame("already checked in", "Staff", ":CheckinMonitorService")
    b.add_message("13.1", "Insert scan log (ALREADY_CHECKED_IN)", ":CheckinMonitorService", "TicketDB")
    b.add_message("13.2", "Return ScanResultDTO (ALREADY_CHECKED_IN)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("13.3", "Return 200 OK (success: false, data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("13.4", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("13.5", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("13.6", "Display warning: Ticket already used", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("14", "Update ticket: is_checked_in = true", ":CheckinMonitorService", "TicketDB")
    b.add_message("15", "Insert scan log (SUCCESS)", ":CheckinMonitorService", "TicketDB")
    b.add_message("16", "Return update & log result", "TicketDB", ":CheckinMonitorService", is_return=True)
    
    b.add_self_call("17", "Check save result", ":CheckinMonitorService")
    f4 = b.start_alt_frame("save failed", "save success", "Staff", ":CheckinMonitorService")
    # Branch 1: save failed
    b.add_message("18.1", "Return ScanResultDTO (SERVER_ERROR)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("18.2", "Return 500 Internal Server Error", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("18.3", "Return 500 Internal Server Error", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("18.4", "Return 500 Internal Server Error", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("18.5", "Display error alert & retry scan", "CheckinScannerPage", "Staff", is_return=True)
    
    b.split_alt_frame(f4)
    
    # Branch 2: save success
    b.add_message("19", "Return ScanResultDTO (SUCCESS)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("20", "Return 200 OK (data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("21", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("22", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("23", "Display 'Access Granted' banner", "CheckinScannerPage", "Staff", is_return=True)
    b.end_alt_frame(f4)
    
    return b.build_xml()

def generate_uc_15_3():
    # UC_15.3 Manual Check-in (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOperationsRoute", "type": "standard", "cx": 1000},
        {"name": ":CheckinMonitorController", "type": "standard", "cx": 1300},
        {"name": ":CheckinMonitorService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_15.3_ManualCheckin", lifelines, 2200, 2200)
    b.add_message("1", "Input TicketCode, CCCD & verify ID", "Staff", "CheckinScannerPage")
    b.add_message("2", "POST /api/checkin/manual-checkin (dto)", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/checkin/manual-checkin", "GatewayAPI", ":CheckinOperationsRoute")
    b.add_message("6", "ManualCheckin(request)", ":CheckinOperationsRoute", ":CheckinMonitorController")
    b.add_message("7", "ManualCheckinAsync(dto, userContext)", ":CheckinMonitorController", ":CheckinMonitorService")
    
    b.add_self_call("8", "Validate CCCD & physical ID (BR-290)", ":CheckinMonitorService")
    f2 = b.start_opt_frame("CCCD invalid or unverified", "Staff", ":CheckinMonitorService")
    b.add_message("9.1", "Return ScanResultDTO (INVALID_CCCD)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("9.2", "Return 200 OK (success: false)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("9.3", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("9.4", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("9.5", "Display 'Physical CCCD verification required'", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("10", "Query ticket by code & eventId", ":CheckinMonitorService", "TicketDB")
    b.add_message("11", "Return ticket row", "TicketDB", ":CheckinMonitorService", is_return=True)
    b.add_message("12", "Update ticket: is_checked_in = true", ":CheckinMonitorService", "TicketDB")
    b.add_message("13", "Insert manual scan log", ":CheckinMonitorService", "TicketDB")
    b.add_message("14", "Return update & log result", "TicketDB", ":CheckinMonitorService", is_return=True)
    
    b.add_self_call("15", "Check save result", ":CheckinMonitorService")
    f3 = b.start_alt_frame("save failed", "save success", "Staff", ":CheckinMonitorService")
    # Branch 1: save failed
    b.add_message("16.1", "Return 500 Internal Server Error", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("16.2", "Return 500 Internal Server Error", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("16.3", "Return 500 Internal Server Error", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("16.4", "Return 500 Internal Server Error", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("16.5", "Display error alert", "CheckinScannerPage", "Staff", is_return=True)
    
    b.split_alt_frame(f3)
    
    # Branch 2: save success
    b.add_message("17", "Return ScanResultDTO (SUCCESS)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("18", "Return 200 OK (data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("19", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("20", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("21", "Display 'Manual Check-in Approved'", "CheckinScannerPage", "Staff", is_return=True)
    b.end_alt_frame(f3)
    
    return b.build_xml()

def generate_uc_15_4():
    # UC_15.4 Lookup Ticket (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOperationsRoute", "type": "standard", "cx": 1000},
        {"name": ":CheckinMonitorController", "type": "standard", "cx": 1300},
        {"name": ":CheckinMonitorService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_15.4_LookupTicket", lifelines, 2200, 1700)
    b.add_message("1", "Enter search query (Phone/Email/CCCD)", "Staff", "CheckinScannerPage")
    b.add_message("2", "GET /api/checkin/lookup-ticket (query)", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/lookup-ticket", "GatewayAPI", ":CheckinOperationsRoute")
    b.add_message("6", "LookupTickets(eventId, query)", ":CheckinOperationsRoute", ":CheckinMonitorController")
    b.add_message("7", "LookupTicketsAsync(query, userId)", ":CheckinMonitorController", ":CheckinMonitorService")
    b.add_message("8", "Query tickets by filter (Phone/Email/CCCD)", ":CheckinMonitorService", "TicketDB")
    b.add_message("9", "Return matching ticket rows", "TicketDB", ":CheckinMonitorService", is_return=True)
    
    b.add_self_call("10", "Apply PII masking (BR-294)", ":CheckinMonitorService")
    b.add_message("11", "Return LookupTicketDTO (masked list)", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("12", "Return 200 OK (data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("13", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("14", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("15", "Display matching tickets with Check-in button", "CheckinScannerPage", "Staff", is_return=True)
    return b.build_xml()

def generate_uc_15_5():
    # UC_15.5 View Check-in Scan History (Staff)
    lifelines = [
        {"name": "Staff", "type": "actor", "cx": 100},
        {"name": "CheckinScannerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":CheckinOperationsRoute", "type": "standard", "cx": 1000},
        {"name": ":CheckinMonitorController", "type": "standard", "cx": 1300},
        {"name": ":CheckinMonitorService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_15.5_ViewCheckinScanHistory", lifelines, 2200, 1600)
    b.add_message("1", "Click 'Scan History'", "Staff", "CheckinScannerPage")
    b.add_message("2", "GET /api/checkin/scan-history (filter)", "CheckinScannerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Staff", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("4.2", "Display error message", "CheckinScannerPage", "Staff", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/scan-history", "GatewayAPI", ":CheckinOperationsRoute")
    b.add_message("6", "GetScanHistory(eventId, filter)", ":CheckinOperationsRoute", ":CheckinMonitorController")
    b.add_message("7", "GetScanHistoryAsync(eventId, filter, userId)", ":CheckinMonitorController", ":CheckinMonitorService")
    b.add_message("8", "Query staff scan logs history", ":CheckinMonitorService", "TicketDB")
    b.add_message("9", "Return scan log rows", "TicketDB", ":CheckinMonitorService", is_return=True)
    b.add_message("10", "Return ScanHistoryDTO", ":CheckinMonitorService", ":CheckinMonitorController", is_return=True)
    b.add_message("11", "Return 200 OK (data)", ":CheckinMonitorController", ":CheckinOperationsRoute", is_return=True)
    b.add_message("12", "Forward 200 OK", ":CheckinOperationsRoute", "GatewayAPI", is_return=True)
    b.add_message("13", "Return 200 OK", "GatewayAPI", "CheckinScannerPage", is_return=True)
    b.add_message("14", "Display scan activity log entries", "CheckinScannerPage", "Staff", is_return=True)
    return b.build_xml()

print("Group 5 complete.")
