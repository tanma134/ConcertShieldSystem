# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_20_1():
    # UC_20.1 View Notifications (Customer / Organizer)
    lifelines = [
        {"name": "Customer", "type": "actor", "cx": 100},
        {"name": "NotificationCenterPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":NotificationsRoute", "type": "standard", "cx": 1000},
        {"name": ":NotificationsController", "type": "standard", "cx": 1300},
        {"name": ":NotificationService", "type": "standard", "cx": 1600},
        {"name": ":NotificationRepository", "type": "standard", "cx": 1900},
        {"name": "NotificationDB", "type": "db", "cx": 2200}
    ]
    b = SequenceDiagramBuilder("UC_20.1_ViewNotifications", lifelines, 2500, 1600)
    b.add_message("1", "Click Notification Bell", "Customer", "NotificationCenterPage")
    b.add_message("2", "GET /api/notifications", "NotificationCenterPage", "GatewayAPI")
    
    b.add_message("3", "Forward GET /api/notifications", "GatewayAPI", ":NotificationsRoute")
    b.add_message("4", "GetNotifications()", ":NotificationsRoute", ":NotificationsController")
    b.add_message("5", "GetUserNotificationsAsync(userId)", ":NotificationsController", ":NotificationService")
    b.add_message("6", "GetUserNotificationsAsync(userId)", ":NotificationService", ":NotificationRepository")
    b.add_message("7", "Query active notifications", ":NotificationRepository", "NotificationDB")
    b.add_message("8", "Return notifications", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("9", "Return notifications list", ":NotificationRepository", ":NotificationService", is_return=True)
    b.add_message("10", "GetUnreadCountAsync(userId)", ":NotificationService", ":NotificationRepository")
    b.add_message("11", "Query unread notification count", ":NotificationRepository", "NotificationDB")
    b.add_message("12", "Return unread count", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("13", "Return unreadCount", ":NotificationRepository", ":NotificationService", is_return=True)
    b.add_message("14", "Return NotificationListDTO", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("15", "Return 200 OK (data)", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("16", "Forward 200 OK", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("17", "Return 200 OK", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("18", "Render notification list & badge", "NotificationCenterPage", "Customer", is_return=True)
    return b.build_xml()

def generate_uc_20_2():
    # UC_20.2 Mark Notification as Read (Customer / Organizer)
    lifelines = [
        {"name": "Customer", "type": "actor", "cx": 100},
        {"name": "NotificationCenterPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":NotificationsRoute", "type": "standard", "cx": 1000},
        {"name": ":NotificationsController", "type": "standard", "cx": 1300},
        {"name": ":NotificationService", "type": "standard", "cx": 1600},
        {"name": ":NotificationRepository", "type": "standard", "cx": 1900},
        {"name": "NotificationDB", "type": "db", "cx": 2200}
    ]
    b = SequenceDiagramBuilder("UC_20.2_MarkNotificationAsRead", lifelines, 2500, 2100)
    b.add_message("1", "Click unread notification item", "Customer", "NotificationCenterPage")
    b.add_message("2", "PUT /api/notifications/{id}/read", "NotificationCenterPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Customer", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("4.2", "Display error message", "NotificationCenterPage", "Customer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward PUT /api/notifications/{id}/read", "GatewayAPI", ":NotificationsRoute")
    b.add_message("6", "MarkAsRead(id)", ":NotificationsRoute", ":NotificationsController")
    b.add_message("7", "MarkAsReadAsync(id, userId)", ":NotificationsController", ":NotificationService")
    b.add_message("8", "GetByIdAsync(id)", ":NotificationService", ":NotificationRepository")
    b.add_message("9", "Query notification by ID", ":NotificationRepository", "NotificationDB")
    b.add_message("10", "Return notification (or null)", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("11", "Return Notification entity", ":NotificationRepository", ":NotificationService", is_return=True)
    
    b.add_self_call("12", "Check notification existence", ":NotificationService")
    f2 = b.start_opt_frame("notification is null", "Customer", ":NotificationService")
    b.add_message("13.1", "Return error (KeyNotFoundException)", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("13.2", "Return 404 Not Found", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("13.3", "Return 404 Not Found", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("13.4", "Return 404 Not Found", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("13.5", "Display error message", "NotificationCenterPage", "Customer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_self_call("14", "Check ownership permission", ":NotificationService")
    f3 = b.start_opt_frame("caller unauthorized", "Customer", ":NotificationService")
    b.add_message("15.1", "Return error (UnauthorizedException)", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("15.2", "Return 403 Forbidden", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("15.3", "Return 403 Forbidden", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("15.4", "Return 403 Forbidden", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("15.5", "Display error message", "NotificationCenterPage", "Customer", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("16", "UpdateAsync(notification, isRead=true)", ":NotificationService", ":NotificationRepository")
    b.add_message("17", "Update notification (is_read = true)", ":NotificationRepository", "NotificationDB")
    b.add_message("18", "Return updated rows", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("19", "Return save result", ":NotificationRepository", ":NotificationService", is_return=True)
    b.add_message("20", "GetUnreadCountAsync(userId)", ":NotificationService", ":NotificationRepository")
    b.add_message("21", "Query remaining unread count", ":NotificationRepository", "NotificationDB")
    b.add_message("22", "Return remaining unreadCount", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("23", "Return unreadCount", ":NotificationRepository", ":NotificationService", is_return=True)
    b.add_message("24", "Return 200 OK (unreadCount)", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("25", "Return 200 OK", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("26", "Forward 200 OK", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("27", "Return 200 OK", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("28", "Update notification style & badge", "NotificationCenterPage", "Customer", is_return=True)
    return b.build_xml()

def generate_uc_20_3():
    # UC_20.3 Delete Notification (Customer / Organizer)
    lifelines = [
        {"name": "Customer", "type": "actor", "cx": 100},
        {"name": "NotificationCenterPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":NotificationsRoute", "type": "standard", "cx": 1000},
        {"name": ":NotificationsController", "type": "standard", "cx": 1300},
        {"name": ":NotificationService", "type": "standard", "cx": 1600},
        {"name": ":NotificationRepository", "type": "standard", "cx": 1900},
        {"name": "NotificationDB", "type": "db", "cx": 2200}
    ]
    b = SequenceDiagramBuilder("UC_20.3_DeleteNotification", lifelines, 2500, 2100)
    b.add_message("1", "Click 'Delete' icon", "Customer", "NotificationCenterPage")
    b.add_message("2", "DELETE /api/notifications/{id}", "NotificationCenterPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Customer", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("4.2", "Display error message", "NotificationCenterPage", "Customer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward DELETE /api/notifications/{id}", "GatewayAPI", ":NotificationsRoute")
    b.add_message("6", "DeleteNotification(id)", ":NotificationsRoute", ":NotificationsController")
    b.add_message("7", "DeleteNotificationAsync(id, userId)", ":NotificationsController", ":NotificationService")
    b.add_message("8", "GetByIdAsync(id)", ":NotificationService", ":NotificationRepository")
    b.add_message("9", "Query notification by ID", ":NotificationRepository", "NotificationDB")
    b.add_message("10", "Return notification (or null)", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("11", "Return Notification entity", ":NotificationRepository", ":NotificationService", is_return=True)
    
    b.add_self_call("12", "Check notification existence", ":NotificationService")
    f2 = b.start_opt_frame("notification is null", "Customer", ":NotificationService")
    b.add_message("13.1", "Return error (KeyNotFoundException)", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("13.2", "Return 404 Not Found", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("13.3", "Return 404 Not Found", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("13.4", "Return 404 Not Found", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("13.5", "Display error message", "NotificationCenterPage", "Customer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_self_call("14", "Check ownership permission", ":NotificationService")
    f3 = b.start_opt_frame("caller unauthorized", "Customer", ":NotificationService")
    b.add_message("15.1", "Return error (UnauthorizedException)", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("15.2", "Return 403 Forbidden", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("15.3", "Return 403 Forbidden", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("15.4", "Return 403 Forbidden", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("15.5", "Display error message", "NotificationCenterPage", "Customer", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("16", "UpdateAsync(notification, isDeleted=true)", ":NotificationService", ":NotificationRepository")
    b.add_message("17", "Update notification (is_deleted = true)", ":NotificationRepository", "NotificationDB")
    b.add_message("18", "Return updated rows", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("19", "Return save result", ":NotificationRepository", ":NotificationService", is_return=True)
    b.add_message("20", "GetUnreadCountAsync(userId)", ":NotificationService", ":NotificationRepository")
    b.add_message("21", "Query remaining unread count", ":NotificationRepository", "NotificationDB")
    b.add_message("22", "Return remaining unreadCount", "NotificationDB", ":NotificationRepository", is_return=True)
    b.add_message("23", "Return unreadCount", ":NotificationRepository", ":NotificationService", is_return=True)
    b.add_message("24", "Return 200 OK (success)", ":NotificationService", ":NotificationsController", is_return=True)
    b.add_message("25", "Return 200 OK", ":NotificationsController", ":NotificationsRoute", is_return=True)
    b.add_message("26", "Forward 200 OK", ":NotificationsRoute", "GatewayAPI", is_return=True)
    b.add_message("27", "Return 200 OK", "GatewayAPI", "NotificationCenterPage", is_return=True)
    b.add_message("28", "Remove item & update badge", "NotificationCenterPage", "Customer", is_return=True)
    return b.build_xml()

print("Group 6 complete.")
