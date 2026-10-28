# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_8_7():
    # UC_8.7 Bulk Revoke Tickets (event change) (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "TicketGovernancePage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":TicketGovernanceRoute", "type": "standard", "cx": 1000},
        {"name": ":TicketGovernanceController", "type": "standard", "cx": 1300},
        {"name": ":TicketGovernanceService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_8.7_BulkRevokeTickets", lifelines, 2200, 2400)
    b.add_message("1", "Select event, reason & click 'Bulk Revoke'", "Admin", "TicketGovernancePage")
    b.add_message("2", "POST /api/checkin/governance/bulk-revoke (dto)", "TicketGovernancePage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("4.2", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/checkin/governance/bulk-revoke", "GatewayAPI", ":TicketGovernanceRoute")
    b.add_self_call("6", "Check policy (Admin)", ":TicketGovernanceRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":TicketGovernanceRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("7.3", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "BulkRevokeTickets(request)", ":TicketGovernanceRoute", ":TicketGovernanceController")
    b.add_message("9", "BulkRevokeTicketsAsync(request, adminId)", ":TicketGovernanceController", ":TicketGovernanceService")
    
    b.add_self_call("10", "Validate eventId > 0", ":TicketGovernanceService")
    f3 = b.start_opt_frame("eventId <= 0", "Admin", ":TicketGovernanceService")
    b.add_message("11.1", "Return error (ArgumentException)", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("11.2", "Return 500 Internal Server Error (Lỗi thu hồi vé)", ":TicketGovernanceController", ":TicketGovernanceRoute", is_return=True)
    b.add_message("11.3", "Return 500 Internal Server Error", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("11.4", "Return 500 Internal Server Error", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("11.5", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("12", "Bulk revoke tickets (status = 'Revoked')", ":TicketGovernanceService", "TicketDB")
    b.add_message("13", "Return affected rows count", "TicketDB", ":TicketGovernanceService", is_return=True)
    b.add_message("14", "Insert revocation records & histories", ":TicketGovernanceService", "TicketDB")
    b.add_message("15", "Return log result", "TicketDB", ":TicketGovernanceService", is_return=True)
    
    b.add_self_call("16", "Check save result", ":TicketGovernanceService")
    fa = b.start_alt_frame("save failed", "save success", "Admin", ":TicketGovernanceService")
    # Branch 1: save failed
    b.add_message("17.1", "Return error (NpgsqlException)", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("17.2", "Return 500 Internal Server Error", ":TicketGovernanceController", ":TicketGovernanceRoute", is_return=True)
    b.add_message("17.3", "Return 500 Internal Server Error", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("17.4", "Return 500 Internal Server Error", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("17.5", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("18", "Return BulkRevokeResponseDTO", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("19", "Return 200 OK (data)", ":TicketGovernanceController", ":TicketGovernanceRoute", is_return=True)
    b.add_message("20", "Forward 200 OK", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("21", "Return 200 OK", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("22", "Display bulk revocation summary", "TicketGovernancePage", "Admin", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

def generate_uc_8_8():
    # UC_8.8 View Ticket Revocation History (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "TicketGovernancePage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":TicketGovernanceRoute", "type": "standard", "cx": 1000},
        {"name": ":TicketGovernanceController", "type": "standard", "cx": 1300},
        {"name": ":TicketGovernanceService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_8.8_ViewTicketRevocationHistory", lifelines, 2200, 1800)
    b.add_message("1", "Select 'Revocation History' tab", "Admin", "TicketGovernancePage")
    b.add_message("2", "GET /api/checkin/governance/revocation-history (query)", "TicketGovernancePage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("4.2", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/governance/revocation-history", "GatewayAPI", ":TicketGovernanceRoute")
    b.add_self_call("6", "Check policy (Admin)", ":TicketGovernanceRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":TicketGovernanceRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("7.3", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetRevocationHistory(query, paging)", ":TicketGovernanceRoute", ":TicketGovernanceController")
    b.add_message("9", "GetRevocationHistoryAsync(query, paging)", ":TicketGovernanceController", ":TicketGovernanceService")
    b.add_message("10", "Query revocation audit records", ":TicketGovernanceService", "TicketDB")
    b.add_message("11", "Return revocation rows and total count", "TicketDB", ":TicketGovernanceService", is_return=True)
    b.add_message("12", "Return RevocationHistoryResponseDTO", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("13", "Return 200 OK (data)", ":TicketGovernanceController", ":TicketGovernanceRoute", is_return=True)
    b.add_message("14", "Forward 200 OK", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("15", "Return 200 OK", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("16", "Render revocation history table", "TicketGovernancePage", "Admin", is_return=True)
    return b.build_xml()

def generate_uc_8_10():
    # UC_8.10 View Reissue History (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "TicketGovernancePage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":TicketGovernanceRoute", "type": "standard", "cx": 1000},
        {"name": ":TicketGovernanceController", "type": "standard", "cx": 1300},
        {"name": ":TicketGovernanceService", "type": "standard", "cx": 1600},
        {"name": "TicketDB", "type": "db", "cx": 1900}
    ]
    b = SequenceDiagramBuilder("UC_8.10_ViewReissueHistory", lifelines, 2200, 1800)
    b.add_message("1", "Select 'Reissue History' tab", "Admin", "TicketGovernancePage")
    b.add_message("2", "GET /api/checkin/governance/reissue-history (query)", "TicketGovernancePage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("4.2", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/checkin/governance/reissue-history", "GatewayAPI", ":TicketGovernanceRoute")
    b.add_self_call("6", "Check policy (Admin)", ":TicketGovernanceRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":TicketGovernanceRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("7.3", "Display error message", "TicketGovernancePage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetReissueHistory(query, paging)", ":TicketGovernanceRoute", ":TicketGovernanceController")
    b.add_message("9", "GetReissueHistoryAsync(query, paging)", ":TicketGovernanceController", ":TicketGovernanceService")
    b.add_message("10", "Query reissue audit records", ":TicketGovernanceService", "TicketDB")
    b.add_message("11", "Return reissue rows and total count", "TicketDB", ":TicketGovernanceService", is_return=True)
    b.add_message("12", "Return ReissueHistoryResponseDTO", ":TicketGovernanceService", ":TicketGovernanceController", is_return=True)
    b.add_message("13", "Return 200 OK (data)", ":TicketGovernanceController", ":TicketGovernanceRoute", is_return=True)
    b.add_message("14", "Forward 200 OK", ":TicketGovernanceRoute", "GatewayAPI", is_return=True)
    b.add_message("15", "Return 200 OK", "GatewayAPI", "TicketGovernancePage", is_return=True)
    b.add_message("16", "Render reissue history table", "TicketGovernancePage", "Admin", is_return=True)
    return b.build_xml()

print("gen_group2.py updated.")
