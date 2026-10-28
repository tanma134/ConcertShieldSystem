# -*- coding: utf-8 -*-
"""
Test Definitions for Group 5: QR Code Scanner & Check-in Interface (UC 15.1 - UC 15.5)
"""

GROUP5_FUNCTIONS = [
    # 112: UC 15.1 View Gate Assignment
    {
        'stt': 112,
        'uc_id': 'UC_15.1',
        'code': 'UC_15.1_ViewGateAssignment',
        'name': 'GetGateAssignmentAsync',
        'req_name': 'View Gate Assignment',
        'class_name': 'CheckinMonitorController / CheckinMonitorService',
        'sheet_name': 'UC_15.1_GateAssignment',
        'loc': 125.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff member views their assigned gate, entrance zone constraints, and event metadata for active shift.',
        'num_tc': 8,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has active event with gate assignment for staff', [1, 2, 3, 4]),
            ('Staff member has no gate assigned for this event', [5]),
            ('Event does not exist in DB', [6]),
            ('Caller is authenticated Staff member', [1, 2, 3, 4, 5, 6, 7]),
            ('Caller is Customer role', [8])
        ],
        'inputs': [
            ('eventId', [
                ('3001 (Active Event ID)', [1, 2, 3, 4, 5, 8]),
                ('99999 (Non-existent ID)', [6]),
                ('0 or -1 (Abnormal)', [7])
            ]),
            ('staffUserId', [
                ('101 (Assigned to Gate A)', [1]),
                ('102 (Supervisor assigned to ALL gates)', [2]),
                ('1 (Boundary: min ID)', [3]),
                ('999999 (Boundary: max ID)', [4]),
                ('103 (Staff without assignment)', [5]),
                ('101', [6, 7]),
                ('501 (Customer ID)', [8])
            ])
        ],
        'returns': [
            ('GateAssignmentResponseDTO with GateId="GATE_A", Zones=["VIP", "STANDARD"]', [1, 3, 4]),
            ('GateAssignmentResponseDTO with GateId="ALL", IsSupervisor=true', [2])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "No gate assignment found for this event")', [5]),
            ('KeyNotFoundException (404 "Event not found")', [6]),
            ('ArgumentException (400 "Event ID must be positive")', [7]),
            ('ForbiddenException (403 "Staff permission required")', [8])
        ],
        'logs': [
            ('"Fetched gate assignment for staff {UserId} at event {EventId}"', [1, 2, 3, 4]),
            ('"No gate assignment for staff {UserId}"', [5]),
            ('"Event {EventId} not found"', [6]),
            ('"Invalid eventId: {EventId}"', [7]),
            ('"Forbidden access by user {UserId}"', [8])
        ]
    },

    # 113: UC 15.2 Scan Dynamic QR Ticket
    {
        'stt': 113,
        'uc_id': 'UC_15.2',
        'code': 'UC_15.2_ScanDynamicQRTicket',
        'name': 'ScanDynamicQrTicketAsync',
        'req_name': 'Scan Dynamic QR Ticket',
        'class_name': 'CheckinMonitorController / CheckinMonitorService',
        'sheet_name': 'UC_15.2_ScanDynamicQR',
        'loc': 210.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff scans encrypted rotating TOTP dynamic QR code, verifies cryptographic signature, checks replay, and records entry.',
        'num_tc': 11,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has valid, un-scanned ticket for event', [1, 2, 3, 4]),
            ('Ticket was already scanned (USED)', [6]),
            ('Ticket is in REVOKED status', [7]),
            ('Ticket belongs to different event', [8]),
            ('Ticket is designated for Gate B (not Gate A)', [9]),
            ('Caller is authorized Staff', [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),
            ('Caller is Customer role', [11])
        ],
        'inputs': [
            ('request.EventId', [
                ('3001 (Valid Event ID)', [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11])
            ]),
            ('request.GateId', [
                ('"GATE_A"', [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11])
            ]),
            ('request.QrToken & TotpCode', [
                ('Valid Dynamic QR Token with valid TOTP code', [1, 2]),
                ('TOTP code at second 29 of 30s window (Boundary)', [3]),
                ('Scan right at gate opening timestamp (Boundary)', [4]),
                ('Expired TOTP code (timestamp drifted > 30s)', [5]),
                ('Valid token for already-used ticket', [6]),
                ('Valid token for revoked ticket', [7]),
                ('Valid token for Event 4001 (wrong event)', [8]),
                ('Valid token for Gate B (wrong gate)', [9]),
                ('Malformed/tampered QR ciphertext string', [10]),
                ('Valid dynamic QR called by Customer', [11])
            ])
        ],
        'returns': [
            ('ScanResultResponseDTO with Result="SUCCESS", AttendeeName, SeatCode, Zone="VIP"', [1, 2, 3, 4]),
            ('ScanResultResponseDTO with Result="EXPIRED_QR", Message="QR code expired"', [5]),
            ('ScanResultResponseDTO with Result="ALREADY_USED", ScannedAt="18:30:00"', [6]),
            ('ScanResultResponseDTO with Result="INVALID_STATUS", Status="REVOKED"', [7]),
            ('ScanResultResponseDTO with Result="WRONG_EVENT", Message="Ticket belongs to another event"', [8]),
            ('ScanResultResponseDTO with Result="WRONG_GATE", Message="Ticket assigned to Gate B"', [9])
        ],
        'exceptions': [
            ('SecurityException (400 "Invalid QR ciphertext payload")', [10]),
            ('ForbiddenException (403 "Staff permission required")', [11])
        ],
        'logs': [
            ('"Ticket {TicketId} checked in successfully at gate {GateId}"', [1, 2, 3, 4]),
            ('"Check-in scan rejected: {Result} for ticket {TicketId}"', [5, 6, 7, 8, 9]),
            ('"Tampered or corrupted QR token received"', [10]),
            ('"Forbidden check-in scan attempt by user {UserId}"', [11])
        ]
    },

    # 114: UC 15.3 Manual Check-in
    {
        'stt': 114,
        'uc_id': 'UC_15.3',
        'code': 'UC_15.3_ManualCheckin',
        'name': 'ManualCheckinAsync',
        'req_name': 'Manual Check-in',
        'class_name': 'CheckinMonitorController / CheckinMonitorService',
        'sheet_name': 'UC_15.3_ManualCheckin',
        'loc': 170.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff manually checks in attendee upon phone battery failure or damaged screen using ticket code and ID document.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has valid, un-scanned ticket for event', [1, 2, 3, 4, 7, 8, 9]),
            ('Ticket does not exist in DB', [5]),
            ('Ticket was already checked in', [6]),
            ('Caller is authorized Staff', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Customer role', [9])
        ],
        'inputs': [
            ('request.TicketCode', [
                ('"TK-LIVE-1001"', [1, 3, 4, 6, 7, 8, 9]),
                ('"TK-LIVE-1002"', [2]),
                ('"TK-INVALID-9999"', [5])
            ]),
            ('request.IdDocumentNumber', [
                ('"079099012345" (Citizen ID card)', [1, 2, 3, 5, 6, 8, 9]),
                ('"B1234567" (Passport number)', [4]),
                ('"" (Abnormal: empty ID document)', [7])
            ]),
            ('request.Reason', [
                ('"Attendee phone battery died; identity verified via National ID"', [1, 2, 5, 6, 7, 9]),
                ('10 chars string (Boundary: min length)', [3]),
                ('250 chars string (Boundary: max length)', [4]),
                ('"" (Abnormal: empty reason)', [8])
            ])
        ],
        'returns': [
            ('ScanResultResponseDTO with Result="SUCCESS", CheckinType="MANUAL"', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Ticket code not found")', [5]),
            ('InvalidOperationException (409 "Ticket is already checked in")', [6]),
            ('ValidationException (400 "ID document number is required")', [7]),
            ('ValidationException (400 "Manual check-in reason is required")', [8]),
            ('ForbiddenException (403 "Staff privilege required")', [9])
        ],
        'logs': [
            ('"Manual check-in completed for ticket {TicketCode} by staff {UserId}"', [1, 2, 3, 4]),
            ('"Ticket code {TicketCode} not found for manual check-in"', [5]),
            ('"Duplicate manual check-in attempt for ticket {TicketCode}"', [6]),
            ('"Manual check-in validation error: {Error}"', [7, 8]),
            ('"Unauthorized manual check-in attempt by user {UserId}"', [9])
        ]
    },

    # 115: UC 15.4 Lookup Ticket
    {
        'stt': 115,
        'uc_id': 'UC_15.4',
        'code': 'UC_15.4_LookupTicket',
        'name': 'LookupTicketsAsync',
        'req_name': 'Lookup Ticket',
        'class_name': 'CheckinMonitorController / CheckinMonitorService',
        'sheet_name': 'UC_15.4_LookupTicket',
        'loc': 140.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff searches for attendee tickets by customer email, phone number, attendee name, or order code at helpdesk.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has attendee tickets for event', [1, 2, 3, 4, 5, 8, 9]),
            ('No tickets match search keyword', [6]),
            ('Caller is authorized Staff', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Customer role', [9])
        ],
        'inputs': [
            ('request.EventId', [
                ('3001 (Valid Event ID)', [1, 2, 3, 4, 5, 6, 8, 9]),
                ('0 or -1 (Abnormal invalid Event ID)', [7])
            ]),
            ('request.SearchKeyword', [
                ('"0901234567" (Search by Phone number)', [1]),
                ('"customer@example.com" (Search by Email)', [2]),
                ('"Nguyen Van A" (Search by Customer Name)', [3]),
                ('"ORD" (Boundary: 3 chars minimum search length)', [4]),
                ('"ORD-2026-LONG-SEARCH-STRING-50-CHARS"', [5]),
                ('"NON_EXISTENT_KEYWORD"', [6]),
                ('"0901234567"', [7]),
                ('"AB" (Abnormal: length < 3 chars)', [8]),
                ('"0901234567"', [9])
            ])
        ],
        'returns': [
            ('LookupTicketResponseDTO with Matches.Count > 0', [1, 2, 3, 4, 5]),
            ('LookupTicketResponseDTO with Matches.Count == 0', [6])
        ],
        'exceptions': [
            ('ArgumentException (400 "Event ID must be positive")', [7]),
            ('ValidationException (400 "Search keyword must have at least 3 characters")', [8]),
            ('ForbiddenException (403 "Staff permission required")', [9])
        ],
        'logs': [
            ('"Found {Count} tickets matching keyword {Keyword} for event {EventId}"', [1, 2, 3, 4, 5, 6]),
            ('"Invalid lookup parameters: {Error}"', [7, 8]),
            ('"Unauthorized ticket lookup by user {UserId}"', [9])
        ]
    },

    # 116: UC 15.5 View Check-in Scan History
    {
        'stt': 116,
        'uc_id': 'UC_15.5',
        'code': 'UC_15.5_ViewCheckinScanHistory',
        'name': 'GetScanHistoryAsync',
        'req_name': 'View Check-in Scan History',
        'class_name': 'CheckinMonitorController / CheckinMonitorService',
        'sheet_name': 'UC_15.5_ScanHistory',
        'loc': 130.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff queries their personal recent scan history (status, ticket code, timestamp, gate) at scanner terminal.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database contains scan logs for staff member', [1, 2, 3, 4, 5, 8, 9]),
            ('Staff member has 0 scans performed today', [6]),
            ('Caller has Staff role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Customer role', [9])
        ],
        'inputs': [
            ('eventId', [
                ('3001 (Valid Event ID)', [1, 2, 3, 4, 5, 6, 8, 9]),
                ('0 or -1 (Abnormal)', [7])
            ]),
            ('statusFilter', [
                ('null (all scans)', [1, 4, 5, 6, 7, 8, 9]),
                ('"SUCCESS"', [2]),
                ('"FAILED"', [3])
            ]),
            ('gateId', [
                ('null (all gates)', [1, 2, 3, 5, 6, 7, 8, 9]),
                ('"GATE_A"', [4])
            ]),
            ('limit', [
                ('20 (Normal)', [1, 2, 3, 4, 6, 7, 8, 9]),
                ('50 (Boundary: max limit)', [5])
            ])
        ],
        'returns': [
            ('ScanHistoryResponseDTO with Items.Count > 0 and TotalScans >= Items.Count', [1, 2, 3, 4, 5]),
            ('ScanHistoryResponseDTO with Items.Count == 0 and TotalScans == 0', [6])
        ],
        'exceptions': [
            ('ArgumentException (400 "Event ID must be positive")', [7]),
            ('ValidationException (400 "Invalid status filter value")', [8]),
            ('ForbiddenException (403 "Staff permission required")', [9])
        ],
        'logs': [
            ('"Fetched {Count} scan history records for staff {StaffId} at event {EventId}"', [1, 2, 3, 4, 5, 6]),
            ('"Invalid scan history parameters: {Error}"', [7, 8]),
            ('"Forbidden access by user {UserId}"', [9])
        ]
    }
]
