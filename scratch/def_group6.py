# -*- coding: utf-8 -*-
"""
Test Definitions for Group 6: Real-time Notification Management (UC 20.1 - UC 20.3)
"""

GROUP6_FUNCTIONS = [
    # 155: UC 20.1 View Notifications
    {
        'stt': 155,
        'uc_id': 'UC_20.1',
        'code': 'UC_20.1_ViewNotifications',
        'name': 'GetUserNotificationsAsync',
        'req_name': 'View Notifications',
        'class_name': 'NotificationsController / NotificationService',
        'sheet_name': 'UC_20.1_ViewNotifs',
        'loc': 120.0,
        'lack_of_tc': 0.0,
        'test_req': 'Customer and Organizer retrieve notifications inbox with unread counts, timestamps, and deep-link payload.',
        'num_tc': 8,
        'tc_types':  ['N', 'N', 'B', 'B', 'B', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has notifications for user', [1, 2, 4, 5]),
            ('User has 0 notifications (new account)', [3]),
            ('User is authenticated (Customer or Organizer)', [1, 2, 3, 4, 5]),
            ('User account does not exist / deleted', [6]),
            ('User is unauthenticated (anonymous)', [7, 8])
        ],
        'inputs': [
            ('userId', [
                ('101 (Customer with 5 unread, 10 total)', [1]),
                ('201 (Organizer with notifications)', [2]),
                ('301 (User with zero notifications)', [3]),
                ('1 (Boundary: min ID)', [4]),
                ('999999 (Boundary: high ID)', [5]),
                ('88888 (Deleted user ID)', [6]),
                ('0 (Boundary: invalid)', [7]),
                ('-1 (Abnormal: negative)', [8])
            ])
        ],
        'returns': [
            ('NotificationListResponseDTO with UnreadCount=5, Notifications.Count=10', [1]),
            ('NotificationListResponseDTO with UnreadCount > 0, Count > 0', [2, 4, 5]),
            ('NotificationListResponseDTO with UnreadCount=0, Notifications.Count=0', [3])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "User account not found")', [6]),
            ('UnauthorizedAccessException (401 "User authentication required")', [7, 8])
        ],
        'logs': [
            ('"Retrieved notifications for user {UserId}: {Unread} unread out of {Total}"', [1, 2, 3, 4, 5]),
            ('"User {UserId} not found"', [6]),
            ('"Anonymous request to notification inbox rejected"', [7, 8])
        ]
    },

    # 156: UC 20.2 Mark Notification as Read
    {
        'stt': 156,
        'uc_id': 'UC_20.2',
        'code': 'UC_20.2_MarkNotificationAsRead',
        'name': 'MarkAsReadAsync',
        'req_name': 'Mark Notification as Read',
        'class_name': 'NotificationsController / NotificationService',
        'sheet_name': 'UC_20.2_MarkRead',
        'loc': 95.0,
        'lack_of_tc': 0.0,
        'test_req': 'Recipient marks a specific notification as read, updating IsRead=true and setting ReadAt timestamp.',
        'num_tc': 8,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has unread notification belonging to caller', [1, 3, 4]),
            ('Notification is already marked as read (IsRead=true)', [2]),
            ('Notification does not exist in DB', [5]),
            ('Notification belongs to another user', [6]),
            ('Caller is authenticated', [1, 2, 3, 4, 5, 6, 7]),
            ('Caller is unauthenticated', [8])
        ],
        'inputs': [
            ('id', [
                ('1001 (Unread notification ID)', [1]),
                ('1002 (Already read notification ID)', [2]),
                ('1 (Boundary: min ID)', [3]),
                ('999999 (Boundary: high ID)', [4]),
                ('88888 (Non-existent ID)', [5]),
                ('2001 (Notification owned by User 99)', [6]),
                ('0 or -1 (Abnormal invalid ID)', [7]),
                ('1001 (Called without token)', [8])
            ]),
            ('userId', [
                ('101 (Owner user ID)', [1, 2, 3, 4, 5]),
                ('101 (Non-owner user ID)', [6]),
                ('101', [7]),
                ('0 (Unauthenticated)', [8])
            ])
        ],
        'returns': [
            ('Object { success: true, isRead: true }', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Notification not found")', [5]),
            ('ForbiddenException (403 "You do not have access to this notification")', [6]),
            ('ArgumentException (400 "Notification ID must be positive")', [7]),
            ('UnauthorizedAccessException (401 "Authentication required")', [8])
        ],
        'logs': [
            ('"Notification {Id} marked as read by user {UserId}"', [1, 2, 3, 4]),
            ('"Notification {Id} not found"', [5]),
            ('"Access denied: notification {Id} does not belong to user {UserId}"', [6]),
            ('"Invalid notification ID: {Id}"', [7]),
            ('"Unauthenticated mark-as-read attempt"', [8])
        ]
    },

    # 157: UC 20.3 Delete Notification
    {
        'stt': 157,
        'uc_id': 'UC_20.3',
        'code': 'UC_20.3_DeleteNotification',
        'name': 'DeleteNotificationAsync',
        'req_name': 'Delete Notification',
        'class_name': 'NotificationsController / NotificationService',
        'sheet_name': 'UC_20.3_DeleteNotif',
        'loc': 90.0,
        'lack_of_tc': 0.0,
        'test_req': 'Recipient deletes a notification from their inbox (soft-delete or permanent removal).',
        'num_tc': 8,
        'tc_types':  ['N', 'N', 'B', 'B', 'A', 'A', 'A', 'A'],
        'tc_status': ['P', 'P', 'P', 'P', 'P', 'P', 'P', 'P'],
        'preconditions': [
            ('Database has notification belonging to caller', [1, 2, 3, 4]),
            ('Notification does not exist in DB', [5]),
            ('Notification belongs to another user', [6]),
            ('Caller is authenticated', [1, 2, 3, 4, 5, 6, 7]),
            ('Caller is unauthenticated', [8])
        ],
        'inputs': [
            ('id', [
                ('1001 (Read notification ID)', [1]),
                ('1002 (Unread notification ID)', [2]),
                ('1 (Boundary: min ID)', [3]),
                ('999999 (Boundary: high ID)', [4]),
                ('88888 (Non-existent ID)', [5]),
                ('3001 (Owned by User 99)', [6]),
                ('0 or -1 (Abnormal invalid ID)', [7]),
                ('1001 (Unauthenticated call)', [8])
            ]),
            ('userId', [
                ('101 (Owner user ID)', [1, 2, 3, 4, 5]),
                ('101 (Non-owner)', [6]),
                ('101', [7]),
                ('0 (Anonymous)', [8])
            ])
        ],
        'returns': [
            ('Object { success: true, message: "Notification deleted successfully" }', [1, 2, 3, 4])
        ],
        'exceptions': [
            ('KeyNotFoundException (404 "Notification not found")', [5]),
            ('ForbiddenException (403 "You do not have access to this notification")', [6]),
            ('ArgumentException (400 "Notification ID must be positive")', [7]),
            ('UnauthorizedAccessException (401 "Authentication required")', [8])
        ],
        'logs': [
            ('"Notification {Id} deleted by user {UserId}"', [1, 2, 3, 4]),
            ('"Notification {Id} not found for deletion"', [5]),
            ('"Unauthorized delete attempt on notification {Id} by user {UserId}"', [6]),
            ('"Invalid notification ID: {Id}"', [7]),
            ('"Unauthenticated delete request"', [8])
        ]
    }
]
