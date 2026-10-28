# -*- coding: utf-8 -*-
"""
Test Definitions for Group 1: Voucher & Checkout (UC 7.5 - UC 7.11)
"""

GROUP1_FUNCTIONS = [
    # 49: UC 7.5 View Organizer Vouchers
    {
        'stt': 49,
        'uc_id': 'UC_7.5',
        'code': 'UC_7.5_GetOrganizerVouchers',
        'name': 'GetVouchersAsync',
        'req_name': 'View Organizer Vouchers',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.5_ViewOrgVouchers',
        'loc': 145.0,
        'lack_of_tc': 0.0,
        'test_req': 'Retrieve paginated vouchers filtered by scope=ORGANIZER, active status, search keyword with caller ownership enforcement.',
        'num_tc': 10,
        'tc_types':  ['N', 'N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible and seeded with vouchers', [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),
            ('User is authenticated with Organizer role', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('User is authenticated with Customer role (unauthorized)', [10])
        ],
        'inputs': [
            ('page', [
                ('1 (Normal first page)', [1, 2, 3, 4, 5, 6, 10]),
                ('0 (Boundary: invalid)', [8]),
                ('-1 (Abnormal: negative)', [9]),
                ('9999 (Boundary: out of range)', [7])
            ]),
            ('limit', [
                ('10 (Normal page size)', [1, 2, 3, 4, 7, 8, 9, 10]),
                ('1 (Boundary: min limit)', [5]),
                ('100 (Boundary: max limit)', [6])
            ]),
            ('isActive', [
                ('null (all vouchers)', [1, 5, 6, 7, 8, 9, 10]),
                ('true (active only)', [2]),
                ('false (inactive only)', [3])
            ]),
            ('searchCode', [
                ('null (no search)', [1, 2, 3, 5, 6, 7, 8, 9, 10]),
                ('"VIP2026" (matches vouchers)', [4])
            ])
        ],
        'returns': [
            ('PagedResultDTO with Items.Count > 0 and TotalCount >= Items.Count', [1, 2, 3, 4, 5, 6]),
            ('PagedResultDTO with Items.Count == 0 and TotalCount >= 0', [7])
        ],
        'exceptions': [
            ('ArgumentException ("Page must be >= 1")', [8, 9]),
            ('ForbiddenException (403 Forbidden)', [10])
        ],
        'logs': [
            ('"Retrieved {Count} organizer vouchers successfully"', [1, 2, 3, 4, 5, 6, 7]),
            ('"Invalid pagination parameters page={page}"', [8, 9]),
            ('"User {UserId} unauthorized to view organizer vouchers"', [10])
        ]
    },

    # 50: UC 7.6 Create Organizer Voucher
    {
        'stt': 50,
        'uc_id': 'UC_7.6',
        'code': 'UC_7.6_CreateOrganizerVoucher',
        'name': 'CreateVoucherAsync',
        'req_name': 'Create Organizer Voucher',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.6_CreateOrgVoucher',
        'loc': 160.0,
        'lack_of_tc': 0.0,
        'test_req': 'Validate and create a new organizer voucher with discount rules, quantity, and valid date range.',
        'num_tc': 10,
        'tc_types':  ['N', 'N', 'B', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible', [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),
            ('Caller is authenticated Organizer', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Caller is Customer role', [10])
        ],
        'inputs': [
            ('dto.Code', [
                ('"SUMMER20"', [1]),
                ('"FIXED50K"', [2, 4, 5]),
                ('"MINPERCENT"', [3]),
                ('Existing code "EXISTING_CODE"', [6]),
                ('"" or null', [7])
            ]),
            ('dto.DiscountType', [
                ('"PERCENT"', [1, 3]),
                ('"FIXED_AMOUNT"', [2, 4, 5, 6, 7, 8, 9, 10])
            ]),
            ('dto.DiscountPercent', [
                ('20.0 (Normal percent)', [1]),
                ('1.0 (Boundary: min percent)', [3]),
                ('100.0 (Boundary: max percent)', [4]),
                ('105.0 (Abnormal: > 100%)', [8])
            ]),
            ('dto.DiscountAmount', [
                ('50000 (Normal amount)', [2, 6, 7, 10]),
                ('0 or -1000 (Abnormal amount)', [9])
            ]),
            ('dto.TotalQuantity', [
                ('100 (Normal quantity)', [1, 2, 3, 4, 6, 7, 8, 9, 10]),
                ('1 (Boundary: min quantity)', [5])
            ])
        ],
        'returns': [
            ('VoucherResponseDTO with VoucherId > 0, Status="Active"', [1, 2, 3, 4, 5])
        ],
        'exceptions': [
            ('ConflictException (409 "Voucher code already exists")', [6]),
            ('ValidationException (400 "Code is required")', [7]),
            ('ValidationException (400 "Discount percent must be between 1 and 100")', [8]),
            ('ValidationException (400 "Discount amount must be positive")', [9]),
            ('ForbiddenException (403 Forbidden)', [10])
        ],
        'logs': [
            ('"Voucher {Code} created successfully with Id {Id}"', [1, 2, 3, 4, 5]),
            ('"Duplicate voucher code detected: {Code}"', [6]),
            ('"Voucher validation failed: {Error}"', [7, 8, 9]),
            ('"Access denied for user {UserId}"', [10])
        ]
    },

    # 51: UC 7.7 View Voucher Usage Details
    {
        'stt': 51,
        'uc_id': 'UC_7.7',
        'code': 'UC_7.7_ViewVoucherUsageDetails',
        'name': 'GetVoucherUsagesAsync',
        'req_name': 'View Voucher Usage Details',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.7_VoucherUsages',
        'loc': 110.0,
        'lack_of_tc': 0.0,
        'test_req': 'View usage history list (orderId, userId, discountAmount, usedAt) for a specified voucher.',
        'num_tc': 8,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Target voucher exists and belongs to caller Organizer', [1, 2, 3, 4]),
            ('Target voucher belongs to another Organizer', [6]),
            ('Target voucher does not exist in DB', [5])
        ],
        'inputs': [
            ('voucherId', [
                ('101 (Voucher with 5 usages)', [1]),
                ('102 (Voucher with 0 usages)', [2]),
                ('1 (Boundary: min ID)', [3]),
                ('999999 (Boundary: high valid ID)', [4]),
                ('88888 (Non-existent ID)', [5]),
                ('205 (Other organizer voucher)', [6]),
                ('0 (Boundary: invalid ID)', [7]),
                ('-5 (Abnormal: negative ID)', [8])
            ]),
            ('isAdmin', [
                ('false', [1, 2, 3, 4, 5, 6, 7, 8])
            ])
        ],
        'returns': [
            ('List<VoucherUsageDTO> with Count == 5', [1, 3, 4]),
            ('List<VoucherUsageDTO> with Count == 0', [2])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Voucher not found")', [5]),
            ('ForbiddenException (403 "You do not have access to this voucher")', [6]),
            ('ArgumentException (400 "Voucher ID must be positive")', [7, 8])
        ],
        'logs': [
            ('"Retrieved {Count} usage records for voucher {VoucherId}"', [1, 2, 3, 4]),
            ('"Voucher {VoucherId} not found"', [5]),
            ('"Unauthorized access attempt for voucher {VoucherId}"', [6]),
            ('"Invalid voucher ID: {VoucherId}"', [7, 8])
        ]
    },

    # 52: UC 7.8 View System Vouchers
    {
        'stt': 52,
        'uc_id': 'UC_7.8',
        'code': 'UC_7.8_ViewSystemVouchers',
        'name': 'GetVouchersAsync',
        'req_name': 'View System Vouchers',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.8_ViewSysVouchers',
        'loc': 130.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin views system-wide vouchers with pagination and active/inactive status filters.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible with SYSTEM vouchers', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Caller is authenticated Admin', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller is Organizer (not Admin)', [9])
        ],
        'inputs': [
            ('scope', [
                ('"SYSTEM"', [1, 2, 3, 4, 5, 6, 7, 8, 9])
            ]),
            ('page', [
                ('1 (Normal page)', [1, 2, 3, 4, 5, 9]),
                ('0 (Boundary: invalid)', [7]),
                ('-1 (Abnormal: negative)', [8]),
                ('500 (Boundary: page beyond total)', [6])
            ]),
            ('limit', [
                ('10 (Normal)', [1, 2, 3, 6, 7, 8, 9]),
                ('1 (Boundary: min limit)', [4]),
                ('100 (Boundary: max limit)', [5])
            ]),
            ('isActive', [
                ('null (all system vouchers)', [1, 4, 5, 6, 7, 8, 9]),
                ('true (active system vouchers)', [2]),
                ('false (inactive system vouchers)', [3])
            ])
        ],
        'returns': [
            ('PagedResultDTO with Items.Count > 0 and Scope="SYSTEM"', [1, 2, 3, 4, 5]),
            ('PagedResultDTO with Items.Count == 0', [6])
        ],
        'exceptions': [
            ('ArgumentException (400 "Page must be >= 1")', [7, 8]),
            ('ForbiddenException (403 "Admin access required")', [9])
        ],
        'logs': [
            ('"Retrieved {Count} system vouchers"', [1, 2, 3, 4, 5, 6]),
            ('"Invalid page parameter: {page}"', [7, 8]),
            ('"Non-admin caller attempted to view system vouchers"', [9])
        ]
    },

    # 53: UC 7.9 Create System Voucher
    {
        'stt': 53,
        'uc_id': 'UC_7.9',
        'code': 'UC_7.9_CreateSystemVoucher',
        'name': 'CreateVoucherAsync',
        'req_name': 'Create System Voucher',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.9_CreateSysVoucher',
        'loc': 150.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin creates platform-wide discount voucher applicable across all events.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible', [1, 2, 3, 4, 5, 6, 7, 8, 9]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Customer role', [9])
        ],
        'inputs': [
            ('dto.Scope', [
                ('"SYSTEM"', [1, 2, 3, 4, 5, 6, 7, 8, 9])
            ]),
            ('dto.Code', [
                ('"PLATFORM10"', [1]),
                ('"SYS_FIXED100K"', [2, 3, 4]),
                ('"DUPLICATE_SYS"', [5]),
                ('"" (empty code)', [6]),
                ('"VALID_CODE"', [7, 8, 9])
            ]),
            ('dto.DiscountPercent', [
                ('10.0', [1]),
                ('100.0 (Boundary: max)', [3]),
                ('0.0 (Abnormal: zero)', [7])
            ]),
            ('dto.DiscountAmount', [
                ('100000', [2, 4, 5, 6, 8, 9])
            ]),
            ('dto.TotalQuantity', [
                ('1000', [1, 2, 3, 5, 6, 7, 8, 9]),
                ('1 (Boundary: min)', [4])
            ]),
            ('dto.EndsAt', [
                ('DateTime.UtcNow.AddMonths(1) (Future)', [1, 2, 3, 4, 5, 6, 7, 9]),
                ('DateTime.UtcNow.AddDays(-1) (Past)', [8])
            ])
        ],
        'returns': [
            ('VoucherResponseDTO with Scope="SYSTEM", Status="Active"', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('ConflictException (409 "System voucher code already exists")', [5]),
            ('ValidationException (400 "Code is required")', [6]),
            ('ValidationException (400 "Discount percent must be > 0")', [7]),
            ('ValidationException (400 "End date must be in the future")', [8]),
            ('ForbiddenException (403 "Admin role required")', [9])
        ],
        'logs': [
            ('"Created system voucher {Code} with Id {Id}"', [1, 2, 3, 4]),
            ('"System voucher code duplicate: {Code}"', [5]),
            ('"Validation failed: {Error}"', [6, 7, 8]),
            ('"Access denied: {UserId} is not Admin"', [9])
        ]
    },

    # 54: UC 7.10 Update System Voucher
    {
        'stt': 54,
        'uc_id': 'UC_7.10',
        'code': 'UC_7.10_UpdateSystemVoucher',
        'name': 'UpdateVoucherAsync',
        'req_name': 'Update System Voucher',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.10_UpdateSysVoucher',
        'loc': 140.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin modifies system voucher properties including validity dates, active toggle, and quantity.',
        'num_tc': 9,
        'tc_types':  ['N', 'N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible and target SYSTEM voucher exists', [1, 2, 3, 4, 5, 7, 8, 9]),
            ('Target voucher does not exist in DB', [6]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7, 8]),
            ('Caller has Organizer role', [9])
        ],
        'inputs': [
            ('id', [
                ('10 (Valid System Voucher ID)', [1, 2, 3, 4, 5, 7, 8, 9]),
                ('99999 (Non-existent ID)', [6])
            ]),
            ('dto.IsActive', [
                ('false (Deactivate)', [1]),
                ('true (Reactivate)', [2, 3, 4, 5, 6, 7, 8, 9])
            ]),
            ('dto.TotalQuantity', [
                ('500 (Increase quantity from 200 to 500)', [2]),
                ('200 (Keep unchanged)', [1, 3]),
                ('50 (Boundary: equal to UsedQuantity=50)', [4]),
                ('30 (Abnormal: less than UsedQuantity=50)', [7])
            ]),
            ('dto.EndsAt', [
                ('DateTime.UtcNow.AddMonths(3) (Extend)', [3]),
                ('DateTime.UtcNow (Boundary: immediate expire)', [5]),
                ('DateTime.UtcNow.AddDays(-10) (Abnormal: past)', [8])
            ])
        ],
        'returns': [
            ('VoucherResponseDTO with updated fields and Status="OK"', [1, 2, 3, 4, 5])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "System voucher not found")', [6]),
            ('ValidationException (400 "TotalQuantity cannot be less than UsedQuantity")', [7]),
            ('ValidationException (400 "EndsAt cannot be in the past")', [8]),
            ('ForbiddenException (403 "Admin privilege required")', [9])
        ],
        'logs': [
            ('"Updated system voucher {VoucherId} successfully"', [1, 2, 3, 4, 5]),
            ('"Voucher {VoucherId} not found for update"', [6]),
            ('"Validation error updating voucher: {Error}"', [7, 8]),
            ('"Forbidden access attempt on system voucher update"', [9])
        ]
    },

    # 55: UC 7.11 Delete System Voucher
    {
        'stt': 55,
        'uc_id': 'UC_7.11',
        'code': 'UC_7.11_DeleteSystemVoucher',
        'name': 'SoftDeleteVoucherAsync',
        'req_name': 'Delete System Voucher',
        'class_name': 'VouchersController / VoucherService',
        'sheet_name': 'UC_7.11_DeleteSysVoucher',
        'loc': 115.0,
        'lack_of_tc': 0.0,
        'test_req': 'Admin marks system voucher as soft-deleted (IsDeleted=true) preserving historical audit integrity.',
        'num_tc': 8,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database is accessible and target SYSTEM voucher exists', [1, 2, 3, 4, 7, 8]),
            ('Target voucher is already soft-deleted (IsDeleted=true)', [6]),
            ('Target voucher does not exist in DB', [5]),
            ('Caller has Admin role', [1, 2, 3, 4, 5, 6, 7]),
            ('Caller has Organizer role', [8])
        ],
        'inputs': [
            ('id', [
                ('15 (Unused System Voucher ID)', [1]),
                ('16 (Partially used System Voucher ID)', [2]),
                ('1 (Boundary: min ID)', [3]),
                ('20 (Boundary: inactive voucher)', [4]),
                ('99999 (Non-existent ID)', [5]),
                ('30 (Already deleted ID)', [6]),
                ('0 or -1 (Abnormal invalid ID)', [7]),
                ('15 (Valid ID called by non-admin)', [8])
            ])
        ],
        'returns': [
            ('true (Boolean indicating soft-delete successful)', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Voucher not found")', [5]),
            ('InvalidOperationException (400 "Voucher is already deleted")', [6]),
            ('ArgumentException (400 "Voucher ID must be positive")', [7]),
            ('ForbiddenException (403 "Admin access required")', [8])
        ],
        'logs': [
            ('"Soft-deleted system voucher {VoucherId}"', [1, 2, 3, 4]),
            ('"Voucher {VoucherId} not found"', [5]),
            ('"Voucher {VoucherId} already deleted"', [6]),
            ('"Invalid voucher ID: {VoucherId}"', [7]),
            ('"Permission denied for user {UserId}"', [8])
        ]
    }
]
