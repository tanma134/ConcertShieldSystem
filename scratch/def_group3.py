# -*- coding: utf-8 -*-
"""
Test Definitions for Group 3: Governance, Policy Versioning & Compliance (UC 11.1 - UC 11.5)
"""

GROUP3_FUNCTIONS = [
    # 85: UC 11.1 View Policy Version History
    {
        'stt': 85,
        'uc_id': 'UC_11.1',
        'code': 'UC_11.1_ViewPolicyVersionHistory',
        'name': 'GetPolicyVersions',
        'req_name': 'View Policy Version History (Price/Refund/Queue/Risk)',
        'class_name': 'SystemPoliciesController',
        'sheet_name': 'UC_11.1_PolicyHistory',
        'loc': 125.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin views historical versions and active configurations of platform governance policies.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible and seeded with policy versions', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is authenticated Admin', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Customer role', [9])
        ],
        'inputs': [
            ('category', [
                ('null (all policy categories)', [1, 4, 5, 7, 8, 9]),
                ('"REFUND"', [2]),
                ('"RISK"', [3]),
                ('"INVALID_CAT"', [6])
            ]),
            ('status', [
                ('null (all statuses)', [1, 3, 4, 5, 6, 7, 8, 9]),
                ('"ACTIVE"', [2])
            ]),
            ('page', [
                ('1 (Normal)', [1, 2, 3, 4, 5, 6, 9]),
                ('0 (Boundary: invalid)', [7]),
                ('-1 (Abnormal)', [8])
            ]),
            ('pageSize', [
                ('10 (Normal)', [1, 2, 3, 6, 7, 8, 9]),
                ('1 (Boundary min)', [4]),
                ('50 (Boundary max)', [5])
            ])
        ],
        'returns': [
            ('PagedResult<PolicyVersionDTO> with Items.Count > 0', [1, 2, 3, 4, 5])
        ],
        'exceptions': [
            ('ValidationException (400 "Invalid policy category")', [6]),
            ('ArgumentException (400 "Page must be >= 1")', [7, 8]),
            ('ForbiddenException (403 "Admin access required")', [9])
        ],
        'logs': [
            ('"Fetched {Count} policy versions for category={Category}"', [1, 2, 3, 4, 5]),
            ('"Validation error: category {Category} is not recognized"', [6]),
            ('"Invalid pagination request"', [7, 8]),
            ('"Access denied for user {UserId}"', [9])
        ]
    },

    # 86: UC 11.2 Create New Policy Version
    {
        'stt': 86,
        'uc_id': 'UC_11.2',
        'code': 'UC_11.2_CreatePolicyVersion',
        'name': 'CreatePolicyVersion',
        'req_name': 'Create New Policy Version',
        'class_name': 'SystemPoliciesController',
        'sheet_name': 'UC_11.2_CreatePolicyVersion',
        'loc': 155.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin creates and deploys a new version of system policy rules (e.g. Refund deadline, Risk threshold).',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Policy version number does not exist for category', [1, 2, 3, 4, 6, 7, 8, 9]),
            ('Policy version number already exists (duplicate)', [5]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Organizer role', [9])
        ],
        'inputs': [
            ('dto.Category', [
                ('"REFUND"', [1, 3, 5, 6, 7, 8, 9]),
                ('"RISK"', [2, 4])
            ]),
            ('dto.VersionNumber', [
                ('"2.1.0"', [1]),
                ('"1.5.0"', [2]),
                ('"3.0.0"', [3, 4]),
                ('"2.0.0" (Existing duplicate version)', [5]),
                ('"" (Empty version)', [6]),
                ('"2.2.0"', [7, 8, 9])
            ]),
            ('dto.RulesJson', [
                ('Valid JSON string \'{"feePercent": 5, "cutoffHours": 24}\'', [1, 2, 3, 5, 8, 9]),
                ('Minimal valid JSON \'{"k":1}\'', [4]),
                ('Invalid JSON syntax \'{feePercent: 5}\'', [7])
            ]),
            ('dto.EffectiveDate', [
                ('DateTime.UtcNow.AddDays(7) (Future)', [1, 2, 4, 5, 6, 7, 9]),
                ('DateTime.UtcNow (Boundary: immediate effect)', [3]),
                ('DateTime.UtcNow.AddDays(-5) (Abnormal: past)', [8])
            ])
        ],
        'returns': [
            ('PolicyVersionDTO with Id > 0 and Status="DRAFT" or "ACTIVE"', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('ConflictException (409 "Policy version already exists")', [5]),
            ('ValidationException (400 "Version number is required")', [6]),
            ('ValidationException (400 "RulesJson must be valid JSON")', [7]),
            ('ValidationException (400 "EffectiveDate cannot be in the past")', [8]),
            ('ForbiddenException (403 "Admin role required")', [9])
        ],
        'logs': [
            ('"Created policy version {Category} v{Version} successfully"', [1, 2, 3, 4]),
            ('"Duplicate policy version {Category} v{Version}"', [5]),
            ('"Policy validation error: {Error}"', [6, 7, 8]),
            ('"Unauthorized attempt to create policy by user {UserId}"', [9])
        ]
    },

    # 87: UC 11.3 Submit Organizer Verification Documents
    {
        'stt': 87,
        'uc_id': 'UC_11.3',
        'code': 'UC_11.3_SubmitOrganizerVerification',
        'name': 'SubmitVerification',
        'req_name': 'Submit Organizer Verification Documents',
        'class_name': 'OrganizerVerificationController',
        'sheet_name': 'UC_11.3_SubmitVerification',
        'loc': 165.0,
        'lack_of_tc': 0.0,
        'test_req': 'Organizer submits business credentials (business name, tax code, document file URLs, contact info) for KYC review.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Organizer does not have an active PENDING request', [1, 2, 3, 4, 6, 7, 8, 9]),
            ('Organizer already has a PENDING request awaiting review', [5]),
            ('Caller is authenticated Organizer', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Customer without organizer profile', [9])
        ],
        'inputs': [
            ('dto.BusinessName', [
                ('"Saigon Sound LLC"', [1, 3, 4, 5, 6, 7, 8, 9]),
                ('"Hanoi Live Events Co."', [2])
            ]),
            ('dto.TaxCode', [
                ('"0102030405" (10-digit valid tax code)', [1, 3, 4, 5, 6, 8, 9]),
                ('"0102030405-001" (13-digit valid tax code)', [2]),
                ('"INVALID_TAX" (Invalid alphanumeric/format)', [7])
            ]),
            ('dto.DocumentUrls', [
                ('["https://s3.bucket/doc1.pdf", "https://s3.bucket/doc2.pdf"]', [1, 2, 5, 7, 8, 9]),
                ('["https://s3.bucket/doc1.pdf"] (Boundary: min 1 doc)', [3]),
                ('5 document URLs (Boundary: max 5 docs)', [4]),
                ('null or [] (Abnormal: empty list)', [6])
            ]),
            ('dto.ContactEmail', [
                ('"contact@saigonsound.com"', [1, 2, 3, 4, 5, 6, 7, 9]),
                ('"invalid-email-format"', [8])
            ])
        ],
        'returns': [
            ('VerificationRequestResponseDTO with Status="PENDING" and RequestId > 0', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('ConflictException (409 "You already have a pending verification request")', [5]),
            ('ValidationException (400 "At least one document URL is required")', [6]),
            ('ValidationException (400 "Invalid tax code format")', [7]),
            ('ValidationException (400 "Invalid contact email address")', [8]),
            ('ForbiddenException (403 "Organizer role required to submit KYC")', [9])
        ],
        'logs': [
            ('"Verification request submitted by organizer {UserId} successfully"', [1, 2, 3, 4]),
            ('"Duplicate pending verification request for organizer {UserId}"', [5]),
            ('"Validation failed for verification submission: {Error}"', [6, 7, 8]),
            ('"Non-organizer user {UserId} attempted verification submission"', [9])
        ]
    },

    # 88: UC 11.4 Review Organizer Verification Documents
    {
        'stt': 88,
        'uc_id': 'UC_11.4',
        'code': 'UC_11.4_ReviewOrganizerVerification',
        'name': 'GetVerificationRequests',
        'req_name': 'Review Organizer Verification Documents',
        'class_name': 'OrganizerVerificationController',
        'sheet_name': 'UC_11.4_ReviewVerification',
        'loc': 140.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin queries pending and historical organizer verification requests with document links and business details.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has verification requests', [1, 2, 3, 4, 5, 7, 8]),
            ('Database has 0 pending requests', [6]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Organizer role', [9])
        ],
        'inputs': [
            ('status', [
                ('null (all requests)', [1, 4, 5, 7, 8, 9]),
                ('"PENDING"', [2, 6]),
                ('"APPROVED"', [3])
            ]),
            ('page', [
                ('1 (Normal)', [1, 2, 3, 4, 5, 6, 9]),
                ('0 (Boundary: invalid)', [7]),
                ('-1 (Abnormal)', [8])
            ]),
            ('limit', [
                ('10 (Normal)', [1, 2, 3, 6, 7, 8, 9]),
                ('1 (Boundary min)', [4]),
                ('100 (Boundary max)', [5])
            ])
        ],
        'returns': [
            ('List<VerificationRequestResponseDTO> with Count > 0', [1, 2, 3, 4, 5]),
            ('List<VerificationRequestResponseDTO> with Count == 0', [6])
        ],
        'exceptions': [
            ('ArgumentException (400 "Page must be >= 1")', [7, 8]),
            ('ForbiddenException (403 "Admin access required")', [9])
        ],
        'logs': [
            ('"Retrieved {Count} verification requests for review"', [1, 2, 3, 4, 5, 6]),
            ('"Invalid query parameters: page={Page}, limit={Limit}"', [7, 8]),
            ('"Forbidden access by user {UserId}"', [9])
        ]
    },

    # 89: UC 11.5 Approve/Reject Organizer Verification
    {
        'stt': 89,
        'uc_id': 'UC_11.5',
        'code': 'UC_11.5_ApproveRejectVerification',
        'name': 'ReviewVerification',
        'req_name': 'Approve/Reject Organizer Verification',
        'class_name': 'OrganizerVerificationController',
        'sheet_name': 'UC_11.5_ApproveRejectKYC',
        'loc': 150.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin approves or rejects organizer verification, updating account status and notifying the organizer.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has target verification request in PENDING status', [1, 2, 3, 4, 7, 8, 9]),
            ('Target request does not exist in DB', [5]),
            ('Target request is already APPROVED / REJECTED', [6]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Organizer role', [9])
        ],
        'inputs': [
            ('id', [
                ('501 (Valid Request ID)', [1, 2, 3, 4, 7, 8, 9]),
                ('99999 (Non-existent ID)', [5]),
                ('502 (Already resolved ID)', [6])
            ]),
            ('dto.Decision', [
                ('"APPROVE"', [1, 3, 5, 6, 9]),
                ('"REJECT"', [2, 4, 7]),
                ('"INVALID_ACTION"', [8])
            ]),
            ('dto.RejectionReason', [
                ('null or "" (Allowed for APPROVE)', [1, 3, 5, 6, 8, 9]),
                ('"Business registration certificate expired"', [2]),
                ('500 characters string (Boundary: max length)', [4]),
                ('null or "" (Abnormal: empty for REJECT)', [7])
            ])
        ],
        'returns': [
            ('VerificationRequestResponseDTO with Status="APPROVED"', [1, 3]),
            ('VerificationRequestResponseDTO with Status="REJECTED"', [2, 4])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Verification request not found")', [5]),
            ('InvalidOperationException (400 "Request has already been processed")', [6]),
            ('ValidationException (400 "RejectionReason is required when rejecting")', [7]),
            ('ValidationException (400 "Invalid decision value")', [8]),
            ('ForbiddenException (403 "Admin privilege required")', [9])
        ],
        'logs': [
            ('"Verification request {Id} approved successfully"', [1, 3]),
            ('"Verification request {Id} rejected: {Reason}"', [2, 4]),
            ('"Verification request {Id} not found"', [5]),
            ('"Cannot process request {Id}: already finalized"', [6]),
            ('"Validation failed for KYC decision: {Error}"', [7, 8]),
            ('"Forbidden access by user {UserId}"', [9])
        ]
    }
]
