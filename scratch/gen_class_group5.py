# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from class_builder import ClassDiagramBuilder

def generate_uc_15_1_class():
    b = ClassDiagramBuilder("UC_15.1_ViewGateAssignment", 1400, 1200)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("dto", "GateAssignmentResponseDTO", attributes=[
        "+EventId: int",
        "+EventName: string",
        "+StaffUserId: int",
        "+GateId: string",
        "+GateName: string",
        "+Zone: string",
        "+ShiftStartTime: DateTime",
        "+ShiftEndTime: DateTime"
    ], x=40, y=160, width=280)
    
    b.add_class("ctrl", "CheckinMonitorController", attributes=[
        "-ICheckinMonitorService _monitorService"
    ], methods=[
        "+GetGateAssignment(eventId: int): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ICheckinMonitorService", methods=[
        "+GetGateAssignmentAsync(eventId: int, staffUserId: int): Task&lt;GateAssignmentResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "CheckinMonitorService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+GetGateAssignmentAsync(eventId: int, staffUserId: int): Task&lt;GateAssignmentResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("assignment", "StaffGateAssignment", attributes=[
        "+AssignmentId: int",
        "+EventId: int",
        "+StaffUserId: int",
        "+GateId: string",
        "+Zone: string",
        "+ShiftStart: DateTime",
        "+ShiftEnd: DateTime"
    ], x=560, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "assignment", "Dependency")
    
    return b.build_xml()

def generate_uc_15_2_class():
    b = ClassDiagramBuilder("UC_15.2_ScanDynamicQRTicket", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "ScanQrRequestDTO", attributes=[
        "+EventId: int",
        "+TicketCode: string",
        "+DynamicToken: string",
        "+GateId: string",
        "+DeviceTimestamp: DateTime"
    ], x=40, y=160, width=260)
    
    b.add_class("res_dto", "ScanResultResponseDTO", attributes=[
        "+Success: bool",
        "+Status: string",
        "+AttendeeName: string?",
        "+SeatNumber: string?",
        "+Zone: string?",
        "+GateId: string?",
        "+Message: string"
    ], x=40, y=340, width=260)
    
    b.add_class("ctrl", "CheckinMonitorController", attributes=[
        "-ICheckinMonitorService _monitorService"
    ], methods=[
        "+ScanQrTicket(request: ScanQrRequestDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ICheckinMonitorService", methods=[
        "+ScanQrTicketAsync(request: ScanQrRequestDTO, staffUserId: int, staffUserName: string): Task&lt;ScanResultResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "CheckinMonitorService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+ScanQrTicketAsync(request: ScanQrRequestDTO, staffUserId: int, staffUserName: string): Task&lt;ScanResultResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+EventId: int",
        "+TicketCode: string",
        "+IsCheckedIn: bool",
        "+CheckedInAt: DateTime?"
    ], x=320, y=690, width=260)
    
    b.add_class("log", "CheckinLog", attributes=[
        "+LogId: int",
        "+TicketId: int",
        "+ScannedAt: DateTime",
        "+StaffUserId: int",
        "+Status: string",
        "+GateId: string"
    ], x=720, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_dependency("svc", "log", "Dependency")
    b.add_association("ticket", "log", "Association")
    
    return b.build_xml()

def generate_uc_15_3_class():
    b = ClassDiagramBuilder("UC_15.3_ManualCheckin", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "ManualCheckinRequestDTO", attributes=[
        "+EventId: int",
        "+TicketCode: string",
        "+CccdNumber: string",
        "+PhysicalIdVerified: bool",
        "+Reason: string?",
        "+GateId: string"
    ], x=40, y=160, width=260)
    
    b.add_class("res_dto", "ScanResultResponseDTO", attributes=[
        "+Success: bool",
        "+Status: string",
        "+AttendeeName: string?",
        "+Zone: string?",
        "+Message: string"
    ], x=40, y=360, width=260)
    
    b.add_class("ctrl", "CheckinMonitorController", attributes=[
        "-ICheckinMonitorService _monitorService"
    ], methods=[
        "+ManualCheckin(request: ManualCheckinRequestDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ICheckinMonitorService", methods=[
        "+ManualCheckinAsync(request: ManualCheckinRequestDTO, staffUserId: int, staffUserName: string): Task&lt;ScanResultResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "CheckinMonitorService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+ManualCheckinAsync(request: ManualCheckinRequestDTO, staffUserId: int, staffUserName: string): Task&lt;ScanResultResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+EventId: int",
        "+TicketCode: string",
        "+OwnerCccd: string",
        "+IsCheckedIn: bool"
    ], x=320, y=690, width=260)
    
    b.add_class("log", "CheckinLog", attributes=[
        "+LogId: int",
        "+TicketId: int",
        "+CheckinType: string",
        "+ScannedAt: DateTime",
        "+StaffUserId: int",
        "+Status: string"
    ], x=720, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_dependency("svc", "log", "Dependency")
    b.add_association("ticket", "log", "Association")
    
    return b.build_xml()

def generate_uc_15_4_class():
    b = ClassDiagramBuilder("UC_15.4_LookupTicket", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "LookupTicketRequestDTO", attributes=[
        "+EventId: int",
        "+SearchType: string",
        "+QueryValue: string"
    ], x=40, y=160, width=260)
    
    b.add_class("res_dto", "LookupTicketResponseDTO", attributes=[
        "+TotalMatches: int",
        "+Tickets: List&lt;LookupTicketItemDTO&gt;"
    ], x=40, y=280, width=260)
    
    b.add_class("item_dto", "LookupTicketItemDTO", attributes=[
        "+TicketId: int",
        "+TicketCode: string",
        "+MaskedAttendeeName: string",
        "+MaskedEmail: string",
        "+MaskedPhone: string",
        "+Zone: string",
        "+IsCheckedIn: bool"
    ], x=40, y=390, width=260)
    
    b.add_class("ctrl", "CheckinMonitorController", attributes=[
        "-ICheckinMonitorService _monitorService"
    ], methods=[
        "+LookupTickets(request: LookupTicketRequestDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ICheckinMonitorService", methods=[
        "+LookupTicketsAsync(request: LookupTicketRequestDTO, staffUserId: int): Task&lt;LookupTicketResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "CheckinMonitorService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+LookupTicketsAsync(request: LookupTicketRequestDTO, staffUserId: int): Task&lt;LookupTicketResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("ticket", "Ticket", attributes=[
        "+TicketId: int",
        "+EventId: int",
        "+TicketCode: string",
        "+AttendeeName: string",
        "+AttendeeEmail: string",
        "+AttendeePhone: string",
        "+IsCheckedIn: bool"
    ], x=560, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "ticket", "Dependency")
    b.add_association("res_dto", "item_dto", "Association")
    
    return b.build_xml()

def generate_uc_15_5_class():
    b = ClassDiagramBuilder("UC_15.5_ViewCheckinScanHistory", 1400, 1300)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("res_dto", "ScanHistoryResponseDTO", attributes=[
        "+TotalLogs: int",
        "+Items: List&lt;ScanHistoryItemDTO&gt;"
    ], x=40, y=160, width=260)
    
    b.add_class("item_dto", "ScanHistoryItemDTO", attributes=[
        "+LogId: int",
        "+TicketCode: string",
        "+AttendeeName: string",
        "+GateId: string",
        "+Status: string",
        "+ScannedAt: DateTime"
    ], x=40, y=300, width=260)
    
    b.add_class("ctrl", "CheckinMonitorController", attributes=[
        "-ICheckinMonitorService _monitorService"
    ], methods=[
        "+GetScanHistory(eventId, gateId, statusFilter): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "ICheckinMonitorService", methods=[
        "+GetScanHistoryAsync(eventId: int, gateId: string?, statusFilter: string?, staffUserId: int): Task&lt;ScanHistoryResponseDTO&gt;"
    ], x=390, y=300, width=600, is_interface=True)
    
    b.add_class("svc", "CheckinMonitorService", attributes=[
        "-string _ticketDbConnection"
    ], methods=[
        "+GetScanHistoryAsync(eventId: int, gateId: string?, statusFilter: string?, staffUserId: int): Task&lt;ScanHistoryResponseDTO&gt;"
    ], x=390, y=470, width=600)
    
    b.add_class("log", "CheckinLog", attributes=[
        "+LogId: int",
        "+TicketId: int",
        "+GateId: string",
        "+Status: string",
        "+ScannedAt: DateTime",
        "+StaffUserId: int"
    ], x=560, y=690, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "log", "Dependency")
    b.add_association("res_dto", "item_dto", "Association")
    
    return b.build_xml()

print("Group 5 class diagram generators ready.")
