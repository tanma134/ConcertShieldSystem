# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from class_builder import ClassDiagramBuilder

def generate_uc_8_7_class():
    b = ClassDiagramBuilder("UC_8.7_BulkRevokeTickets", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "BulkRevokeRequestDTO", attributes=[
        "+EventId: int",
        "+ReasonType: string",
        "+ReasonDetails: string",
        "+TicketIds: List&lt;int&gt;?",
        "+ZoneFilter: string?",
        "+RefundPolicy: string"
    ], x=40, y=160, width=260)
    
    b.add_class("res_dto", "BulkRevokeResponseDTO", attributes=[
        "+Success: bool",
        "+BatchId: string",
        "+TotalRevoked: int",
        "+ExecutionTime: DateTime",
        "+Message: string"
    ], x=40, y=380, width=260)
    
    b.add_class("ctrl", "TicketGovernanceController", attributes=[
        "-ITicketGovernanceService _governanceService",
        "-ILogger&lt;TicketGovernanceController&gt; _logger"
    ], methods=[
        "+BulkRevokeTickets(request: BulkRevokeRequestDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ITicketGovernanceService", methods=[
        "+BulkRevokeTicketsAsync(request: BulkRevokeRequestDTO, adminId: int, adminName: string): Task&lt;BulkRevokeResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "TicketGovernanceService", attributes=[
        "-string _ticketDbConnection",
        "-string _eventDbConnection",
        "-ILogger&lt;TicketGovernanceService&gt; _logger"
    ], methods=[
        "+BulkRevokeTicketsAsync(request: BulkRevokeRequestDTO, adminId: int, adminName: string): Task&lt;BulkRevokeResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+EventId: int",
        "+TicketCode: string",
        "+Status: string",
        "+IsCheckedIn: bool",
        "+UpdatedAt: DateTime"
    ], x=320, y=690, width=260)
    
    b.add_class("revocation", "TicketRevocation", attributes=[
        "+RevocationId: int",
        "+TicketId: int",
        "+BatchId: string",
        "+ReasonType: string",
        "+RevokedByAdminId: int",
        "+RevokedAt: DateTime"
    ], x=720, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_dependency("svc", "revocation", "Dependency")
    b.add_association("ticket", "revocation", "Association")
    
    return b.build_xml()

def generate_uc_8_8_class():
    b = ClassDiagramBuilder("UC_8.8_ViewTicketRevocationHistory", 1400, 1200)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("res_dto", "RevocationHistoryResponseDTO", attributes=[
        "+Items: List&lt;RevocationHistoryItemDTO&gt;",
        "+TotalCount: int",
        "+Page: int",
        "+PageSize: int"
    ], x=40, y=160, width=260)
    
    b.add_class("item_dto", "RevocationHistoryItemDTO", attributes=[
        "+RevocationId: int",
        "+TicketCode: string",
        "+AttendeeName: string",
        "+ReasonType: string",
        "+RevokedAt: DateTime",
        "+AdminName: string"
    ], x=40, y=340, width=260)
    
    b.add_class("ctrl", "TicketGovernanceController", attributes=[
        "-ITicketGovernanceService _governanceService"
    ], methods=[
        "+GetRevocationHistory(eventId, search, reasonType, page, pageSize): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ITicketGovernanceService", methods=[
        "+GetRevocationHistoryAsync(eventId, search, reasonType, page, pageSize): Task&lt;RevocationHistoryResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "TicketGovernanceService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+GetRevocationHistoryAsync(eventId, search, reasonType, page, pageSize): Task&lt;RevocationHistoryResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("revocation", "TicketRevocation", attributes=[
        "+RevocationId: int",
        "+TicketId: int",
        "+BatchId: string",
        "+ReasonType: string",
        "+ReasonDetails: string",
        "+RevokedByAdminId: int",
        "+RevokedAt: DateTime"
    ], x=560, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "revocation", "Dependency")
    b.add_association("res_dto", "item_dto", "Association")
    
    return b.build_xml()

def generate_uc_8_10_class():
    b = ClassDiagramBuilder("UC_8.10_ViewReissueHistory", 1400, 1200)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("res_dto", "ReissueHistoryResponseDTO", attributes=[
        "+Items: List&lt;ReissueHistoryItemDTO&gt;",
        "+TotalCount: int",
        "+Page: int",
        "+PageSize: int"
    ], x=40, y=160, width=260)
    
    b.add_class("item_dto", "ReissueHistoryItemDTO", attributes=[
        "+ReissueId: int",
        "+OldTicketCode: string",
        "+NewTicketCode: string",
        "+AttendeeName: string",
        "+Reason: string",
        "+ReissuedAt: DateTime",
        "+StaffName: string"
    ], x=40, y=340, width=260)
    
    b.add_class("ctrl", "TicketGovernanceController", attributes=[
        "-ITicketGovernanceService _governanceService"
    ], methods=[
        "+GetReissueHistory(eventId, search, page, pageSize): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ITicketGovernanceService", methods=[
        "+GetReissueHistoryAsync(eventId, search, page, pageSize): Task&lt;ReissueHistoryResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "TicketGovernanceService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+GetReissueHistoryAsync(eventId, search, page, pageSize): Task&lt;ReissueHistoryResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("reissue", "TicketReissueLog", attributes=[
        "+ReissueId: int",
        "+OldTicketId: int",
        "+NewTicketId: int",
        "+Reason: string",
        "+ReissuedByUserId: int",
        "+ReissuedAt: DateTime"
    ], x=560, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "reissue", "Dependency")
    b.add_association("res_dto", "item_dto", "Association")
    
    return b.build_xml()

print("Group 2 class diagram generators ready.")
