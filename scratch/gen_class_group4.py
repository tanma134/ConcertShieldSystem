# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from class_builder import ClassDiagramBuilder

def generate_uc_14_1_class():
    b = ClassDiagramBuilder("UC_14.1_MonitorRealtimeCheckinStatus", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("res_dto", "CheckinMonitorResponseDTO", attributes=[
        "+EventId: int",
        "+TotalTickets: int",
        "+TotalCheckedIn: int",
        "+CheckinPercentage: double",
        "+ActiveGates: int",
        "+GateStats: List&lt;GateStatDTO&gt;",
        "+ZoneStats: List&lt;ZoneStatDTO&gt;",
        "+RecentActivity: List&lt;CheckinActivityDTO&gt;"
    ], x=40, y=160, width=280)
    
    b.add_class("ctrl", "CheckinMonitorController", attributes=[
        "-ICheckinMonitorService _monitorService"
    ], methods=[
        "+GetMonitorStatus(eventId, gateId, zone): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ICheckinMonitorService", methods=[
        "+GetMonitorStatusAsync(eventId: int, gateId: string?, zone: string?, currentUserId: int, isAdmin: bool): Task&lt;CheckinMonitorResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "CheckinMonitorService", attributes=[
        "-string _ticketDbConnection",
        "-string _eventDbConnection"
    ], methods=[
        "+GetMonitorStatusAsync(eventId: int, gateId: string?, zone: string?, currentUserId: int, isAdmin: bool): Task&lt;CheckinMonitorResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+EventId: int",
        "+TicketCode: string",
        "+IsCheckedIn: bool",
        "+Zone: string",
        "+Gate: string"
    ], x=320, y=690, width=260)
    
    b.add_class("scan_log", "CheckinLog", attributes=[
        "+LogId: int",
        "+TicketId: int",
        "+ScannedAt: DateTime",
        "+StaffUserId: int",
        "+Status: string",
        "+GateId: string"
    ], x=720, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_dependency("svc", "scan_log", "Dependency")
    b.add_association("ticket", "scan_log", "Association")
    
    return b.build_xml()

def generate_uc_14_5_class():
    b = ClassDiagramBuilder("UC_14.5_EnableOfflineCheckinMode", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("pkg_dto", "OfflinePackageResponseDTO", attributes=[
        "+EventId: int",
        "+EventName: string",
        "+PackageVersion: string",
        "+GeneratedAt: DateTime",
        "+TotalTickets: int",
        "+HmacSecretKey: string",
        "+Tickets: List&lt;OfflineTicketRecordDTO&gt;"
    ], x=40, y=160, width=280)
    
    b.add_class("item_dto", "OfflineTicketRecordDTO", attributes=[
        "+TicketId: int",
        "+TicketCode: string",
        "+AttendeeName: string",
        "+Zone: string",
        "+Gate: string",
        "+IsCheckedIn: bool"
    ], x=40, y=390, width=280)
    
    b.add_class("ctrl", "TicketGovernanceController", attributes=[
        "-ITicketGovernanceService _governanceService"
    ], methods=[
        "+GetOfflinePackage(eventId: int): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ITicketGovernanceService", methods=[
        "+GetOfflinePackageAsync(eventId: int): Task&lt;OfflinePackageResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "TicketGovernanceService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+GetOfflinePackageAsync(eventId: int): Task&lt;OfflinePackageResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+EventId: int",
        "+TicketCode: string",
        "+Status: string",
        "+IsCheckedIn: bool"
    ], x=560, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "pkg_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_association("pkg_dto", "item_dto", "Association")
    
    return b.build_xml()

def generate_uc_14_6_class():
    b = ClassDiagramBuilder("UC_14.6_SyncOfflineCheckinRecords", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "OfflineSyncRequestDTO", attributes=[
        "+EventId: int",
        "+DeviceIdentifier: string",
        "+Scans: List&lt;OfflineScanItemDTO&gt;"
    ], x=40, y=160, width=280)
    
    b.add_class("res_dto", "OfflineSyncResponseDTO", attributes=[
        "+BatchId: string",
        "+TotalSynced: int",
        "+SuccessCount: int",
        "+ConflictCount: int",
        "+Conflicts: List&lt;OfflineSyncConflictDTO&gt;"
    ], x=40, y=330, width=280)
    
    b.add_class("ctrl", "TicketGovernanceController", attributes=[
        "-ITicketGovernanceService _governanceService"
    ], methods=[
        "+SyncOfflineScans(request: OfflineSyncRequestDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ITicketGovernanceService", methods=[
        "+SyncOfflineScansAsync(request: OfflineSyncRequestDTO, staffUserId: int): Task&lt;OfflineSyncResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "TicketGovernanceService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+SyncOfflineScansAsync(request: OfflineSyncRequestDTO, staffUserId: int): Task&lt;OfflineSyncResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+TicketCode: string",
        "+IsCheckedIn: bool",
        "+CheckedInAt: DateTime?"
    ], x=320, y=690, width=260)
    
    b.add_class("batch", "OfflineSyncBatch", attributes=[
        "+BatchId: string",
        "+EventId: int",
        "+DeviceIdentifier: string",
        "+SyncedAt: DateTime",
        "+SuccessCount: int",
        "+ConflictCount: int"
    ], x=720, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_dependency("svc", "batch", "Dependency")
    b.add_association("ticket", "batch", "Association")
    
    return b.build_xml()

def generate_uc_14_7_class():
    b = ClassDiagramBuilder("UC_14.7_ResolveOfflineSyncConflicts", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "ResolveConflictRequestDTO", attributes=[
        "+Resolution: string",
        "+AdminNote: string?"
    ], x=40, y=160, width=260)
    
    b.add_class("res_dto", "OfflineSyncConflictDTO", attributes=[
        "+ConflictId: string",
        "+TicketId: int",
        "+TicketCode: string",
        "+ConflictReason: string",
        "+Status: string",
        "+ResolvedAt: DateTime?",
        "+ResolvedBy: string?"
    ], x=40, y=300, width=260)
    
    b.add_class("ctrl", "TicketGovernanceController", attributes=[
        "-ITicketGovernanceService _governanceService"
    ], methods=[
        "+ResolveConflict(conflictId: string, request: ResolveConflictRequestDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ITicketGovernanceService", methods=[
        "+ResolveOfflineConflictAsync(conflictId: string, request: ResolveConflictRequestDTO, userId: int, userName: string): Task&lt;OfflineSyncConflictDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "TicketGovernanceService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+ResolveOfflineConflictAsync(conflictId: string, request: ResolveConflictRequestDTO, userId: int, userName: string): Task&lt;OfflineSyncConflictDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("conflict", "OfflineSyncConflict", attributes=[
        "+ConflictId: string",
        "+TicketId: int",
        "+ConflictReason: string",
        "+Status: string",
        "+ResolvedAt: DateTime?",
        "+ResolvedByUserId: int?"
    ], x=320, y=690, width=260)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+TicketCode: string",
        "+IsCheckedIn: bool",
        "+CheckedInAt: DateTime?"
    ], x=720, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "conflict", "Dependency")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_association("conflict", "ticket", "Association")
    
    return b.build_xml()

print("Group 4 class diagram generators ready.")
