# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from class_builder import ClassDiagramBuilder

def generate_uc_20_1_class():
    b = ClassDiagramBuilder("UC_20.1_ViewNotifications", 1400, 1400)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("res_dto", "NotificationListResponseDTO", attributes=[
        "+TotalCount: int",
        "+UnreadCount: int",
        "+Notifications: List&lt;NotificationItemDTO&gt;"
    ], x=40, y=160, width=280)
    
    b.add_class("item_dto", "NotificationItemDTO", attributes=[
        "+NotificationId: int",
        "+Title: string",
        "+Message: string",
        "+Category: string",
        "+TargetUrl: string?",
        "+IsRead: bool",
        "+CreatedAt: DateTime"
    ], x=40, y=300, width=280)
    
    b.add_class("ctrl", "NotificationsController", attributes=[
        "-INotificationService _notificationService",
        "-ILogger&lt;NotificationsController&gt; _logger"
    ], methods=[
        "+GetNotifications(): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "INotificationService", methods=[
        "+GetUserNotificationsAsync(userId: int): Task&lt;NotificationListResponseDTO&gt;"
    ], x=400, y=300, width=580, is_interface=True)
    
    b.add_class("svc", "NotificationService", attributes=[
        "-INotificationRepository _notificationRepository"
    ], methods=[
        "+GetUserNotificationsAsync(userId: int): Task&lt;NotificationListResponseDTO&gt;"
    ], x=400, y=470, width=580)
    
    b.add_class("irepo", "INotificationRepository", methods=[
        "+GetUserNotificationsAsync(userId: int): Task&lt;List&lt;Notification&gt;&gt;",
        "+GetUnreadCountAsync(userId: int): Task&lt;int&gt;"
    ], x=420, y=690, width=540, is_interface=True)
    
    b.add_class("repo", "NotificationRepository", attributes=[
        "-NotificationDbContext _context"
    ], methods=[
        "+GetUserNotificationsAsync(userId: int): Task&lt;List&lt;Notification&gt;&gt;",
        "+GetUnreadCountAsync(userId: int): Task&lt;int&gt;"
    ], x=420, y=860, width=540)
    
    b.add_class("model", "Notification", attributes=[
        "+NotificationId: int",
        "+UserId: int",
        "+Title: string",
        "+Message: string",
        "+Category: string",
        "+TargetUrl: string?",
        "+IsRead: bool",
        "+CreatedAt: DateTime",
        "+IsDeleted: bool"
    ], x=560, y=1060, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "model", "Dependency")
    b.add_association("res_dto", "item_dto", "Association")
    
    return b.build_xml()

def generate_uc_20_2_class():
    b = ClassDiagramBuilder("UC_20.2_MarkNotificationAsRead", 1400, 1400)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("ctrl", "NotificationsController", attributes=[
        "-INotificationService _notificationService"
    ], methods=[
        "+MarkAsRead(id: int): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "INotificationService", methods=[
        "+MarkAsReadAsync(id: int, userId: int): Task&lt;object&gt;"
    ], x=400, y=300, width=580, is_interface=True)
    
    b.add_class("svc", "NotificationService", attributes=[
        "-INotificationRepository _notificationRepository"
    ], methods=[
        "+MarkAsReadAsync(id: int, userId: int): Task&lt;object&gt;"
    ], x=400, y=470, width=580)
    
    b.add_class("irepo", "INotificationRepository", methods=[
        "+GetByIdAsync(id: int): Task&lt;Notification?&gt;",
        "+UpdateAsync(notification: Notification): Task&lt;Notification&gt;",
        "+GetUnreadCountAsync(userId: int): Task&lt;int&gt;"
    ], x=420, y=690, width=540, is_interface=True)
    
    b.add_class("repo", "NotificationRepository", attributes=[
        "-NotificationDbContext _context"
    ], methods=[
        "+GetByIdAsync(id: int): Task&lt;Notification?&gt;",
        "+UpdateAsync(notification: Notification): Task&lt;Notification&gt;",
        "+GetUnreadCountAsync(userId: int): Task&lt;int&gt;"
    ], x=420, y=860, width=540)
    
    b.add_class("model", "Notification", attributes=[
        "+NotificationId: int",
        "+UserId: int",
        "+Title: string",
        "+IsRead: bool",
        "+ReadAt: DateTime?",
        "+IsDeleted: bool"
    ], x=560, y=1060, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "model", "Dependency")
    
    return b.build_xml()

def generate_uc_20_3_class():
    b = ClassDiagramBuilder("UC_20.3_DeleteNotification", 1400, 1400)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("ctrl", "NotificationsController", attributes=[
        "-INotificationService _notificationService"
    ], methods=[
        "+DeleteNotification(id: int): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "INotificationService", methods=[
        "+DeleteNotificationAsync(id: int, userId: int): Task&lt;object&gt;"
    ], x=400, y=300, width=580, is_interface=True)
    
    b.add_class("svc", "NotificationService", attributes=[
        "-INotificationRepository _notificationRepository"
    ], methods=[
        "+DeleteNotificationAsync(id: int, userId: int): Task&lt;object&gt;"
    ], x=400, y=470, width=580)
    
    b.add_class("irepo", "INotificationRepository", methods=[
        "+GetByIdAsync(id: int): Task&lt;Notification?&gt;",
        "+UpdateAsync(notification: Notification): Task&lt;Notification&gt;",
        "+GetUnreadCountAsync(userId: int): Task&lt;int&gt;"
    ], x=420, y=690, width=540, is_interface=True)
    
    b.add_class("repo", "NotificationRepository", attributes=[
        "-NotificationDbContext _context"
    ], methods=[
        "+GetByIdAsync(id: int): Task&lt;Notification?&gt;",
        "+UpdateAsync(notification: Notification): Task&lt;Notification&gt;",
        "+GetUnreadCountAsync(userId: int): Task&lt;int&gt;"
    ], x=420, y=860, width=540)
    
    b.add_class("model", "Notification", attributes=[
        "+NotificationId: int",
        "+UserId: int",
        "+Title: string",
        "+IsDeleted: bool",
        "+DeletedAt: DateTime?"
    ], x=560, y=1060, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "model", "Dependency")
    
    return b.build_xml()

print("Group 6 class diagram generators ready.")
