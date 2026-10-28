# -*- coding: utf-8 -*-
"""
Test Definitions for Group 2: Dynamic QR Code & Ticket Wallet (UC 8.7, 8.8, 8.10)
"""

GROUP2_FUNCTIONS = [
    # 66: UC 8.7 Bulk Revoke Tickets (event change)
    {
        'stt': 66,
        'uc_id': 'UC_8.7',
        'code': 'UC_8.7_BulkRevokeTickets',
        'name': 'BulkRevokeTicketsAsync',
        'req_name': 'Bulk Revoke Tickets (event change)',
        'class_name': 'TicketGovernanceController / TicketGovernanceService',
        'sheet_name': 'UC_8.7_BulkRevokeTickets',
        'loc': 185.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin bulk revokes tickets due to event cancellation or schedule changes, invalidating QR tokens and generating audit logs.',
        'num_tc': 10,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible and tickets exist in VALID status', [1, 2, 3, 4, 5, 8, 9, 10]),
            ('Event does not exist in DB', [6]),
            ('Tickets already in REVOKED status', [8]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Caller has Staff role (non-admin)', [10])
        ],
        'inputs': [
            ('request.EventId', [
                ('1001 (Valid Event ID)', [1, 2, 3, 4, 5, 7, 8, 9, 10]),
                ('99999 (Non-existent Event ID)', [6])
            ]),
            ('request.TicketIds', [
                ('[201, 202, 203] (Normal 3 tickets)', [1]),
                ('[301, 302] (With SendNotification=true)', [2]),
                ('[401, 402] (Reason="SCHEDULE_CHANGE")', [3]),
                ('[501] (Boundary: single ticket)', [4]),
                ('List of 500 ticket IDs (Boundary: max batch size)', [5]),
                ('null or [] (Empty list)', [7]),
                ('[601, 602] (Already revoked tickets)', [8]),
                ('[701, 702] (With empty reason)', [9]),
                ('[801, 802] (Called by Staff)', [10])
            ]),
            ('request.ReasonType', [
                ('"EVENT_CANCELLED"', [1, 2, 4, 5, 6, 8, 10]),
                ('"SCHEDULE_CHANGE"', [3]),
                ('"FORCE_MAJEURE"', [7, 9])
            ]),
            ('request.SendNotification', [
                ('false', [1, 3, 4, 5, 6, 7, 8, 9, 10]),
                ('true (trigger notification dispatch)', [2])
            ])
        ],
        'returns': [
            ('BulkRevokeResponseDTO with RevokedCount == TicketIds.Count', [1, 2, 3, 4, 5])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Event not found")', [6]),
            ('ValidationException (400 "TicketIds list cannot be empty")', [7]),
            ('InvalidOperationException (400 "Tickets are already revoked")', [8]),
            ('ValidationException (400 "Revocation reason is required")', [9]),
            ('ForbiddenException (403 "Admin privilege required")', [10])
        ],
        'logs': [
            ('"Bulk revoked {Count} tickets for event {EventId} successfully"', [1, 2, 3, 4, 5]),
            ('"Event {EventId} not found for bulk revocation"', [6]),
            ('"Validation error: {Message}"', [7, 9]),
            ('"Conflict: tickets already revoked for event {EventId}"', [8]),
            ('"Unauthorized bulk revoke attempt by user {UserId}"', [10])
        ]
    },

    # 67: UC 8.8 View Ticket Revocation History
    {
        'stt': 67,
        'uc_id': 'UC_8.8',
        'code': 'UC_8.8_ViewRevocationHistory',
        'name': 'GetRevocationHistoryAsync',
        'req_name': 'View Ticket Revocation History',
        'class_name': 'TicketGovernanceController / TicketGovernanceService',
        'sheet_name': 'UC_8.8_RevocationHistory',
        'loc': 135.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin views audit logs of revoked tickets with filters for event, reason type, search keyword, and pagination.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has revocation audit records for EventId', [1, 2, 3, 4, 5, 8, 9]),
            ('Event has 0 revocation records', [6]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Customer role', [9])
        ],
        'inputs': [
            ('eventId', [
                ('1001 (Valid Event ID)', [1, 2, 3, 4, 5, 9]),
                ('1002 (Event with zero revocations)', [6]),
                ('0 (Boundary: invalid)', [7]),
                ('-1 (Abnormal: negative)', [8])
            ]),
            ('reasonType', [
                ('null (all reasons)', [1, 4, 5, 6, 7, 8, 9]),
                ('"EVENT_CANCELLED"', [2]),
                ('"FRAUD"', [3])
            ]),
            ('page', [
                ('1 (First page)', [1, 2, 3, 4, 5, 6, 9]),
                ('0 (Boundary invalid)', [7]),
                ('-1 (Abnormal negative)', [8])
            ]),
            ('pageSize', [
                ('10 (Normal)', [1, 2, 3, 6, 7, 8, 9]),
                ('1 (Boundary min)', [4]),
                ('100 (Boundary max)', [5])
            ])
        ],
        'returns': [
            ('RevocationHistoryResponseDTO with Items.Count > 0 and TotalCount >= Items.Count', [1, 2, 3, 4, 5]),
            ('RevocationHistoryResponseDTO with Items.Count == 0 and TotalCount == 0', [6])
        ],
        'exceptions': [
            ('ArgumentException (400 "EventId must be positive")', [7, 8]),
            ('ForbiddenException (403 "Admin access required")', [9])
        ],
        'logs': [
            ('"Retrieved {Count} revocation history records for event {EventId}"', [1, 2, 3, 4, 5, 6]),
            ('"Invalid input parameters eventId={EventId}, page={Page}"', [7, 8]),
            ('"Forbidden access by user {UserId}"', [9])
        ]
    },

    # 69: UC 8.10 View Reissue History
    {
        'stt': 69,
        'uc_id': 'UC_8.10',
        'code': 'UC_8.10_ViewReissueHistory',
        'name': 'GetReissueHistoryAsync',
        'req_name': 'View Reissue History',
        'class_name': 'TicketGovernanceController / TicketGovernanceService',
        'sheet_name': 'UC_8.10_ReissueHistory',
        'loc': 130.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin queries audit trail of reissued tickets (old ticket code, new ticket code, reissue reason, timestamp).',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database contains reissue logs for event', [1, 2, 3, 4, 5, 8, 9]),
            ('Event has no reissued tickets', [6]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Customer role', [9])
        ],
        'inputs': [
            ('eventId', [
                ('1001 (Valid Event ID)', [1, 2, 3, 4, 5, 9]),
                ('1003 (Event with no reissues)', [6]),
                ('0 (Boundary: invalid)', [7]),
                ('-5 (Abnormal: negative)', [8])
            ]),
            ('search', [
                ('null (all reissues)', [1, 4, 5, 6, 7, 8, 9]),
                ('"TK-OLD-999" (Search by old ticket code)', [2]),
                ('"TK-NEW-888" (Search by new ticket code)', [3])
            ]),
            ('page', [
                ('1 (Normal)', [1, 2, 3, 4, 5, 6, 9]),
                ('0 (Boundary invalid)', [7]),
                ('-1 (Abnormal)', [8])
            ]),
            ('pageSize', [
                ('10 (Normal)', [1, 2, 3, 6, 7, 8, 9]),
                ('1 (Boundary min)', [4]),
                ('100 (Boundary max)', [5])
            ])
        ],
        'returns': [
            ('ReissueHistoryResponseDTO with Items.Count > 0 and TotalCount >= Items.Count', [1, 2, 3, 4, 5]),
            ('ReissueHistoryResponseDTO with Items.Count == 0 and TotalCount == 0', [6])
        ],
        'exceptions': [
            ('ArgumentException (400 "EventId must be positive")', [7, 8]),
            ('ForbiddenException (403 "Admin access required")', [9])
        ],
        'logs': [
            ('"Retrieved {Count} reissue history items for event {EventId}"', [1, 2, 3, 4, 5, 6]),
            ('"Invalid query parameters: eventId={EventId}, page={Page}"', [7, 8]),
            ('"Unauthorized attempt to access reissue history"', [9])
        ]
    }
]
