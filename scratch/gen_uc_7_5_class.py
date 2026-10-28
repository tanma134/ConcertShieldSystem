# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from test_class_builder import ClassDiagramBuilder

def generate_uc_7_5_class():
    b = ClassDiagramBuilder("UC_7.5_ViewOrganizerVouchers_ClassDiagram", 1400, 1400)
    
    # ControllerBase
    b.add_class("cb", "ControllerBase", x=550, y=30, width=160)
    
    # DTOs on left
    b.add_class("paged_dto", "PagedResultDTO&lt;T&gt;", attributes=[
        "+Items: List&lt;T&gt;",
        "+TotalCount: int",
        "+Page: int",
        "+PageSize: int",
        "+TotalPages: int"
    ], x=40, y=140, width=220)
    
    b.add_class("voucher_dto", "VoucherResponseDTO", attributes=[
        "+VoucherId: int",
        "+Code: string",
        "+Scope: string",
        "+OrganizerId: int?",
        "+DiscountType: string",
        "+DiscountAmount: long?",
        "+DiscountPercent: decimal?",
        "+TotalQuantity: int",
        "+UsedQuantity: int",
        "+StartsAt: DateTime",
        "+EndsAt: DateTime",
        "+IsActive: bool"
    ], x=40, y=280, width=220)
    
    # Controller
    b.add_class("ctrl", "VouchersController", attributes=[
        "-IVoucherService _voucherService"
    ], methods=[
        "+GetVouchers(page: int, limit: int, scope: string?, isActive: bool?, searchCode: string?, organizerId: int?): Task&lt;IActionResult&gt;"
    ], x=440, y=150, width=440)
    
    # Service Interface
    b.add_class("isvc", "IVoucherService", methods=[
        "+GetVouchersAsync(page: int, limit: int, scope: string?, isActive: bool?, searchCode: string?, organizerId: int?, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;PagedResultDTO&lt;VoucherResponseDTO&gt;&gt;"
    ], x=390, y=320, width=540, is_interface=True)
    
    # Service Impl
    b.add_class("svc", "VoucherService", attributes=[
        "-IVoucherRepository _voucherRepository",
        "-ITicketApiClient _ticketApiClient",
        "-IEventApiClient _eventApiClient"
    ], methods=[
        "+GetVouchersAsync(page: int, limit: int, scope: string?, isActive: bool?, searchCode: string?, organizerId: int?, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;PagedResultDTO&lt;VoucherResponseDTO&gt;&gt;"
    ], x=390, y=470, width=540)
    
    # Repository Interface
    b.add_class("irepo", "IVoucherRepository", methods=[
        "+GetPagedAsync(page: int, limit: int, scope: string?, isActive: bool?, searchCode: string?, organizerId: int?, isOrganizerOnly: bool): Task&lt;PagedResultDTO&lt;Voucher&gt;&gt;"
    ], x=420, y=650, width=480, is_interface=True)
    
    # Repository Impl
    b.add_class("repo", "VoucherRepository", attributes=[
        "-PaymentDbContext _context"
    ], methods=[
        "+GetPagedAsync(page: int, limit: int, scope: string?, isActive: bool?, searchCode: string?, organizerId: int?, isOrganizerOnly: bool): Task&lt;PagedResultDTO&lt;Voucher&gt;&gt;"
    ], x=420, y=790, width=480)
    
    # Domain Models at bottom
    b.add_class("voucher", "Voucher", attributes=[
        "+VoucherId: int",
        "+Code: string",
        "+Scope: string",
        "+OrganizerId: int?",
        "+DiscountType: string",
        "+DiscountAmount: long?",
        "+DiscountPercent: decimal?",
        "+TotalQuantity: int",
        "+UsedQuantity: int",
        "+StartsAt: DateTime",
        "+EndsAt: DateTime",
        "+IsActive: bool",
        "+CreatedBy: int",
        "+CreatedAt: DateTime",
        "+IsDeleted: bool"
    ], x=300, y=980, width=220)
    
    b.add_class("usage", "VoucherUsage", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=720, y=1020, width=220)
    
    # Relationships
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "paged_dto", "Dependency")
    b.add_dependency("ctrl", "voucher_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "voucher", "Dependency")
    b.add_association("voucher", "usage", "Association", mult_source="1", mult_target="0..*")
    
    return b.build_xml()

if __name__ == "__main__":
    xml = generate_uc_7_5_class()
    with open("scratch/uc_7_5_class_diagram.xml", "w", encoding="utf-8") as f:
        f.write(xml)
    print("UC 7.5 Class diagram generated, size:", len(xml))
