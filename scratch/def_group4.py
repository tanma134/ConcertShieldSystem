# -*- coding: utf-8 -*-
"""
Test Definitions for Group 4: Real-time Check-in Monitoring (UC 14.1, 14.5, 14.6, 14.7)
"""

GROUP4_FUNCTIONS = [
    # 105: UC 14.1 Monitor Real-time Check-in Status
    {
        'stt': 105,
        'uc_id': 'UC_14.1',
        'code': 'UC_14.1_MonitorCheckinStatus',
        'name': 'GetMonitorStatusAsync',
        'req_name': 'Monitor Real-time Check-in Status',
        'class_name': 'CheckinMonitorController / CheckinMonitorService',
        'sheet_name': 'UC_14.1_MonitorCheckin',
        'loc': 165.0,
        'lack_of_tc': 0.0,
        'test_req': 'Organizer and Admin query live aggregate check-in throughput, capacity percentages, and per-gate traffic stats.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has active event with check-in records', [1, 2, 3, 4, 5, 8, 9]),
            ('Event has 0 check-ins scanned so far', [4]),
            ('Event does not exist in DB', [6]),
            ('Caller is authorized Organizer or Admin', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Customer role', [9])
        ],
        'inputs': [
            ('eventId', [
                ('2001 (Active Concert Event)', [1, 2, 3, 4, 5, 8, 9]),
                ('99999 (Non-existent Event ID)', [6]),
                ('0 or -1 (Abnormal invalid Event ID)', [7])
            ]),
            ('gateId', [
                ('null (all gates summary)', [1, 4, 5, 6, 7, 8, 9]),
                ('"GATE_A" (Filter Gate A)', [2])
            ]),
            ('zone', [
                ('null (all zones)', [1, 2, 4, 5, 6, 7, 8, 9]),
                ('"VIP" (Filter VIP zone)', [3])
            ]),
            ('currentUserId', [
                ('10 (Organizer owner ID)', [1, 2, 3, 4, 5, 6, 7]),
                ('99 (Non-owner organizer)', [8]),
                ('500 (Customer ID)', [9])
            ])
        ],
        'returns': [
            ('CheckinMonitorResponseDTO with ScannedTickets > 0, CheckinRate > 0%', [1, 2, 3, 5]),
            ('CheckinMonitorResponseDTO with ScannedTickets == 0, CheckinRate == 0.0%', [4])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Event not found")', [6]),
            ('ArgumentException (400 "Event ID must be positive")', [7]),
            ('ForbiddenException (403 "You are not authorized to view this event")', [8, 9])
        ],
        'logs': [
            ('"Generated checkin monitor dashboard for event {EventId}"', [1, 2, 3, 4, 5]),
            ('"Event {EventId} not found"', [6]),
            ('"Invalid eventId: {EventId}"', [7]),
            ('"Access denied to monitoring dashboard for user {UserId}"', [8, 9])
        ]
    },

    # 109: UC 14.5 Enable Offline Check-in Mode
    {
        'stt': 109,
        'uc_id': 'UC_14.5',
        'code': 'UC_14.5_EnableOfflineMode',
        'name': 'GetOfflinePackageAsync',
        'req_name': 'Enable Offline Check-in Mode',
        'class_name': 'TicketGovernanceController / TicketGovernanceService',
        'sheet_name': 'UC_14.5_EnableOffline',
        'loc': 175.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff downloads signed & encrypted offline ticket manifest package to allow gate scanning during internet outage.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has published event with active tickets', [1, 2, 3, 4, 7, 8, 9]),
            ('Event has 1 ticket (Boundary)', [3]),
            ('Event is in DRAFT status (not published)', [6]),
            ('Event does not exist in DB', [5]),
            ('Caller is assigned Staff / Organizer / Admin', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Customer role', [9])
        ],
        'inputs': [
            ('eventId', [
                ('2001 (Valid Event ID)', [1, 2, 4, 8, 9]),
                ('2002 (Small Event with 1 ticket)', [3]),
                ('99999 (Non-existent ID)', [5]),
                ('3001 (Draft event)', [6]),
                ('0 or -1 (Abnormal)', [7])
            ]),
            ('staffUserId', [
                ('50 (Assigned Gate Staff)', [1, 2, 3, 4, 5, 6, 7]),
                ('88 (Unassigned Staff)', [8]),
                ('200 (Customer)', [9])
            ])
        ],
        'returns': [
            ('OfflinePackageResponseDTO with PackageHash, TicketCount > 1, SignedPayload', [1, 2, 4]),
            ('OfflinePackageResponseDTO with TicketCount == 1', [3])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Event not found")', [5]),
            ('InvalidOperationException (400 "Cannot generate offline package for unpublished event")', [6]),
            ('ArgumentException (400 "EventId must be positive")', [7]),
            ('ForbiddenException (403 "Staff member is not assigned to this event")', [8]),
            ('ForbiddenException (403 "Staff or Organizer permission required")', [9])
        ],
        'logs': [
            ('"Generated encrypted offline package for event {EventId}, version={Version}"', [1, 2, 3, 4]),
            ('"Event {EventId} not found"', [5]),
            ('"Invalid event status for offline export: {Status}"', [6]),
            ('"Invalid eventId: {EventId}"', [7]),
            ('"Unauthorized offline package download attempt by user {UserId}"', [8, 9])
        ]
    },

    # 110: UC 14.6 Sync Offline Check-in Records
    {
        'stt': 110,
        'uc_id': 'UC_14.6',
        'code': 'UC_14.6_SyncOfflineRecords',
        'name': 'SyncOfflineScansAsync',
        'req_name': 'Sync Offline Check-in Records',
        'class_name': 'TicketGovernanceController / TicketGovernanceService',
        'sheet_name': 'UC_14.6_SyncOffline',
        'loc': 190.0,
        'lack_of_tc': 0.0,
        'test_req': 'Staff uploads offline scan batch upon network recovery, reconciling tickets and flagging duplicates as conflicts.',
        'num_tc': 10,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible and event exists', [1, 2, 3, 4, 5, 7, 8, 9, 10]),
            ('Event does not exist in DB', [6]),
            ('Caller has Staff role', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Caller has Customer role', [10])
        ],
        'inputs': [
            ('request.EventId', [
                ('2001 (Valid Event ID)', [1, 2, 3, 4, 5, 7, 8, 9, 10]),
                ('99999 (Non-existent Event ID)', [6])
            ]),
            ('request.Scans', [
                ('50 clean scans (no conflicts)', [1]),
                ('30 scans containing 2 duplicate tickets', [2]),
                ('20 scans across Gates A and B', [3]),
                ('1 scan (Boundary: min batch)', [4]),
                ('500 scans (Boundary: max batch size)', [5]),
                ('null or [] (Abnormal: empty scans)', [7]),
                ('Scans with tampered/invalid offline signature', [8]),
                ('Scans with future timestamp (+2 days)', [9]),
                ('50 scans by Customer', [10])
            ])
        ],
        'returns': [
            ('OfflineSyncResponseDTO with SyncedCount=50, ConflictCount=0', [1]),
            ('OfflineSyncResponseDTO with SyncedCount=28, ConflictCount=2', [2]),
            ('OfflineSyncResponseDTO with SyncedCount=20, ConflictCount=0', [3]),
            ('OfflineSyncResponseDTO with SyncedCount=1, ConflictCount=0', [4]),
            ('OfflineSyncResponseDTO with SyncedCount=500, ConflictCount=0', [5])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Event not found")', [6]),
            ('ValidationException (400 "Scans list cannot be empty")', [7]),
            ('SecurityException (400 "Invalid scan payload digital signature")', [8]),
            ('ValidationException (400 "Scan timestamp cannot be in the future")', [9]),
            ('ForbiddenException (403 "Staff permission required to sync offline records")', [10])
        ],
        'logs': [
            ('"Processed offline sync batch: {Synced} synced, {Conflicts} conflicts"', [1, 2, 3, 4, 5]),
            ('"Event {EventId} not found"', [6]),
            ('"Validation failed: {Error}"', [7, 9]),
            ('"Security alert: tampered offline scan signature detected from device {DeviceId}"', [8]),
            ('"Forbidden access by user {UserId}"', [10])
        ]
    },

    # 111: UC 14.7 Resolve Offline Sync Conflicts
    {
        'stt': 111,
        'uc_id': 'UC_14.7',
        'code': 'UC_14.7_ResolveOfflineConflicts',
        'name': 'ResolveOfflineConflictAsync',
        'req_name': 'Resolve Offline Sync Conflicts',
        'class_name': 'TicketGovernanceController / TicketGovernanceService',
        'sheet_name': 'UC_14.7_ResolveConflicts',
        'loc': 160.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin or Organizer inspects conflicting scan records and applies resolution (ACCEPT_OFFLINE, KEEP_ONLINE, MANUAL_OVERRIDE).',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has conflict record in UNRESOLVED status', [1, 2, 3, 4, 5, 7, 8, 9]),
            ('Conflict record does not exist in DB', [6]),
            ('Caller has Admin or Organizer role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Staff role (without resolution rights)', [9])
        ],
        'inputs': [
            ('conflictId', [
                ('"CONF-101" (Valid Conflict ID)', [1, 2, 3, 4, 5, 7, 8, 9]),
                ('"CONF-999" (Non-existent ID)', [6])
            ]),
            ('request.Resolution', [
                ('"ACCEPT_OFFLINE"', [1]),
                ('"KEEP_ONLINE"', [2]),
                ('"MANUAL_OVERRIDE"', [3, 4, 5]),
                ('"INVALID_ACTION"', [7]),
                ('"KEEP_ONLINE"', [8, 9])
            ]),
            ('request.Reason', [
                ('"Offline scan occurred earlier than online duplicate"', [1]),
                ('"Online entry verified with live biometric match"', [2]),
                ('"Supervisor authorized entry at Gate A"', [3]),
                ('500 chars note (Boundary: max length)', [4]),
                ('10 chars note (Boundary: min length)', [5]),
                ('"" (Abnormal: empty reason)', [8])
            ])
        ],
        'returns': [
            ('OfflineSyncConflictDTO with Status="RESOLVED", Resolution="ACCEPT_OFFLINE"', [1]),
            ('OfflineSyncConflictDTO with Status="RESOLVED", Resolution="KEEP_ONLINE"', [2]),
            ('OfflineSyncConflictDTO with Status="RESOLVED", Resolution="MANUAL_OVERRIDE"', [3, 4, 5])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Conflict record not found")', [6]),
            ('ValidationException (400 "Invalid resolution action")', [7]),
            ('ValidationException (400 "Resolution reason is required")', [8]),
            ('ForbiddenException (403 "Admin or Organizer role required to resolve conflicts")', [9])
        ],
        'logs': [
            ('"Conflict {ConflictId} resolved with action {Resolution} by user {UserId}"', [1, 2, 3, 4, 5]),
            ('"Conflict {ConflictId} not found"', [6]),
            ('"Validation failed: {Error}"', [7, 8]),
            ('"Access denied: {UserId} lacks conflict resolution permission"', [9])
        ]
    }
]
