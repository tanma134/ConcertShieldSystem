# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from class_builder import ClassDiagramBuilder

def generate_uc_7_5_class():
    b = ClassDiagramBuilder("UC_7.5_ViewOrganizerVouchers", 1400, 1500)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("paged_dto", "PagedResultDTO&lt;T&gt;", attributes=[
        "+Items: List&lt;T&gt;",
        "+TotalCount: int",
        "+Page: int",
        "+PageSize: int",
        "+TotalPages: int"
    ], x=40, y=160, width=260)
    
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
    ], x=40, y=340, width=260)
    
    b.add_class("ctrl", "VouchersController", attributes=[
        "-IVoucherService _voucherService"
    ], methods=[
        "+GetVouchers(page, limit, scope, isActive, searchCode, organizerId): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "IVoucherService", methods=[
        "+GetVouchersAsync(page, limit, scope, isActive, searchCode, organizerId, userId, isAdmin, isOrg): Task&lt;PagedResultDTO&lt;VoucherResponseDTO&gt;&gt;"
    ], x=380, y=300, width=620, is_interface=True)
    
    b.add_class("svc", "VoucherService", attributes=[
        "-IVoucherRepository _voucherRepository",
        "-ITicketApiClient _ticketApiClient",
        "-IEventApiClient _eventApiClient"
    ], methods=[
        "+GetVouchersAsync(page, limit, scope, isActive, searchCode, organizerId, userId, isAdmin, isOrg): Task&lt;PagedResultDTO&lt;VoucherResponseDTO&gt;&gt;"
    ], x=380, y=470, width=620)
    
    b.add_class("irepo", "IVoucherRepository", methods=[
        "+GetPagedAsync(page, limit, scope, isActive, searchCode, organizerId, isOrganizerOnly): Task&lt;PagedResultDTO&lt;Voucher&gt;&gt;"
    ], x=400, y=690, width=580, is_interface=True)
    
    b.add_class("repo", "VoucherRepository", attributes=[
        "-PaymentDbContext _context"
    ], methods=[
        "+GetPagedAsync(page, limit, scope, isActive, searchCode, organizerId, isOrganizerOnly): Task&lt;PagedResultDTO&lt;Voucher&gt;&gt;"
    ], x=400, y=860, width=580)
    
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
    ], x=300, y=1060, width=260)
    
    b.add_class("usage", "VoucherUsage", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=720, y=1100, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "paged_dto", "Dependency")
    b.add_dependency("ctrl", "voucher_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "voucher", "Dependency")
    b.add_association("voucher", "usage", "Association")
    
    return b.build_xml()

def generate_uc_7_6_class():
    b = ClassDiagramBuilder("UC_7.6_CreateOrganizerVoucher", 1400, 1500)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("create_dto", "CreateVoucherDTO", attributes=[
        "+Code: string",
        "+Scope: string",
        "+OrganizerId: int?",
        "+EventId: int?",
        "+DiscountType: string",
        "+DiscountAmount: long?",
        "+DiscountPercent: decimal?",
        "+MaxDiscountAmount: long?",
        "+MinOrderAmount: long?",
        "+TotalQuantity: int",
        "+MaxUsagePerUser: int",
        "+StartsAt: DateTime",
        "+EndsAt: DateTime"
    ], x=40, y=160, width=260)
    
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
    ], x=40, y=440, width=260)
    
    b.add_class("ctrl", "VouchersController", attributes=[
        "-IVoucherService _voucherService"
    ], methods=[
        "+CreateVoucher(dto: CreateVoucherDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "IVoucherService", methods=[
        "+CreateVoucherAsync(dto: CreateVoucherDTO, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;VoucherResponseDTO&gt;"
    ], x=380, y=300, width=620, is_interface=True)
    
    b.add_class("svc", "VoucherService", attributes=[
        "-IVoucherRepository _voucherRepository",
        "-ITicketApiClient _ticketApiClient",
        "-IEventApiClient _eventApiClient"
    ], methods=[
        "+CreateVoucherAsync(dto: CreateVoucherDTO, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;VoucherResponseDTO&gt;"
    ], x=380, y=470, width=620)
    
    b.add_class("irepo", "IVoucherRepository", methods=[
        "+CodeExistsAsync(code: string, excludeId: int?): Task&lt;bool&gt;",
        "+CreateAsync(voucher: Voucher): Task&lt;Voucher&gt;"
    ], x=400, y=690, width=580, is_interface=True)
    
    b.add_class("repo", "VoucherRepository", attributes=[
        "-PaymentDbContext _context"
    ], methods=[
        "+CodeExistsAsync(code: string, excludeId: int?): Task&lt;bool&gt;",
        "+CreateAsync(voucher: Voucher): Task&lt;Voucher&gt;"
    ], x=400, y=860, width=580)
    
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
    ], x=300, y=1080, width=260)
    
    b.add_class("usage", "VoucherUsage", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=720, y=1120, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "create_dto", "Dependency")
    b.add_dependency("ctrl", "voucher_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "voucher", "Dependency")
    b.add_association("voucher", "usage", "Association")
    
    return b.build_xml()

def generate_uc_7_7_class():
    b = ClassDiagramBuilder("UC_7.7_ViewVoucherUsageDetails", 1400, 1400)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("usage_dto", "VoucherUsageDTO", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=40, y=200, width=260)
    
    b.add_class("ctrl", "VouchersController", attributes=[
        "-IVoucherService _voucherService"
    ], methods=[
        "+GetVoucherUsages(id: int): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "IVoucherService", methods=[
        "+GetVoucherUsagesAsync(voucherId: int, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;List&lt;VoucherUsageDTO&gt;&gt;"
    ], x=380, y=300, width=620, is_interface=True)
    
    b.add_class("svc", "VoucherService", attributes=[
        "-IVoucherRepository _voucherRepository",
        "-ITicketApiClient _ticketApiClient",
        "-IEventApiClient _eventApiClient"
    ], methods=[
        "+GetVoucherUsagesAsync(voucherId: int, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;List&lt;VoucherUsageDTO&gt;&gt;"
    ], x=380, y=470, width=620)
    
    b.add_class("irepo", "IVoucherRepository", methods=[
        "+GetByIdAsync(id: int, includeDeleted: bool): Task&lt;Voucher?&gt;",
        "+GetUsagesByVoucherIdAsync(voucherId: int): Task&lt;List&lt;VoucherUsage&gt;&gt;"
    ], x=400, y=690, width=580, is_interface=True)
    
    b.add_class("repo", "VoucherRepository", attributes=[
        "-PaymentDbContext _context"
    ], methods=[
        "+GetByIdAsync(id: int, includeDeleted: bool): Task&lt;Voucher?&gt;",
        "+GetUsagesByVoucherIdAsync(voucherId: int): Task&lt;List&lt;VoucherUsage&gt;&gt;"
    ], x=400, y=860, width=580)
    
    b.add_class("voucher", "Voucher", attributes=[
        "+VoucherId: int",
        "+Code: string",
        "+Scope: string",
        "+OrganizerId: int?",
        "+DiscountType: string",
        "+DiscountAmount: long?",
        "+TotalQuantity: int",
        "+UsedQuantity: int",
        "+StartsAt: DateTime",
        "+EndsAt: DateTime",
        "+IsActive: bool"
    ], x=300, y=1060, width=260)
    
    b.add_class("usage", "VoucherUsage", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=720, y=1090, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "usage_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "voucher", "Dependency")
    b.add_dependency("repo", "usage", "Dependency")
    b.add_association("voucher", "usage", "Association")
    
    return b.build_xml()

def generate_uc_7_8_class():
    return generate_uc_7_5_class().replace("UC_7.5_ViewOrganizerVouchers", "UC_7.8_ViewSystemVouchers")

def generate_uc_7_9_class():
    return generate_uc_7_6_class().replace("UC_7.6_CreateOrganizerVoucher", "UC_7.9_CreateSystemVoucher")

def generate_uc_7_10_class():
    b = ClassDiagramBuilder("UC_7.10_UpdateSystemVoucher", 1400, 1500)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("update_dto", "UpdateVoucherDTO", attributes=[
        "+Scope: string?",
        "+DiscountType: string?",
        "+DiscountAmount: long?",
        "+DiscountPercent: decimal?",
        "+MaxDiscountAmount: long?",
        "+MinOrderAmount: long?",
        "+TotalQuantity: int?",
        "+MaxUsagePerUser: int?",
        "+StartsAt: DateTime?",
        "+EndsAt: DateTime?",
        "+IsActive: bool?"
    ], x=40, y=160, width=260)
    
    b.add_class("voucher_dto", "VoucherResponseDTO", attributes=[
        "+VoucherId: int",
        "+Code: string",
        "+Scope: string",
        "+OrganizerId: int?",
        "+DiscountType: string",
        "+DiscountAmount: long?",
        "+TotalQuantity: int",
        "+UsedQuantity: int",
        "+StartsAt: DateTime",
        "+EndsAt: DateTime",
        "+IsActive: bool"
    ], x=40, y=420, width=260)
    
    b.add_class("ctrl", "VouchersController", attributes=[
        "-IVoucherService _voucherService"
    ], methods=[
        "+UpdateVoucher(id: int, dto: UpdateVoucherDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "IVoucherService", methods=[
        "+UpdateVoucherAsync(id: int, dto: UpdateVoucherDTO, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;VoucherResponseDTO&gt;"
    ], x=380, y=300, width=620, is_interface=True)
    
    b.add_class("svc", "VoucherService", attributes=[
        "-IVoucherRepository _voucherRepository",
        "-ITicketApiClient _ticketApiClient",
        "-IEventApiClient _eventApiClient"
    ], methods=[
        "+UpdateVoucherAsync(id: int, dto: UpdateVoucherDTO, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;VoucherResponseDTO&gt;"
    ], x=380, y=470, width=620)
    
    b.add_class("irepo", "IVoucherRepository", methods=[
        "+GetByIdAsync(id: int, includeDeleted: bool): Task&lt;Voucher?&gt;",
        "+UpdateAsync(voucher: Voucher): Task&lt;Voucher&gt;"
    ], x=400, y=690, width=580, is_interface=True)
    
    b.add_class("repo", "VoucherRepository", attributes=[
        "-PaymentDbContext _context"
    ], methods=[
        "+GetByIdAsync(id: int, includeDeleted: bool): Task&lt;Voucher?&gt;",
        "+UpdateAsync(voucher: Voucher): Task&lt;Voucher&gt;"
    ], x=400, y=860, width=580)
    
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
    ], x=300, y=1060, width=260)
    
    b.add_class("usage", "VoucherUsage", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=720, y=1100, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "update_dto", "Dependency")
    b.add_dependency("ctrl", "voucher_dto", "Dependency")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "voucher", "Dependency")
    b.add_association("voucher", "usage", "Association")
    
    return b.build_xml()

def generate_uc_7_11_class():
    b = ClassDiagramBuilder("UC_7.11_DeleteSystemVoucher", 1400, 1400)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("ctrl", "VouchersController", attributes=[
        "-IVoucherService _voucherService"
    ], methods=[
        "+DeleteVoucher(id: int): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("isvc", "IVoucherService", methods=[
        "+SoftDeleteVoucherAsync(id: int, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;bool&gt;"
    ], x=380, y=300, width=620, is_interface=True)
    
    b.add_class("svc", "VoucherService", attributes=[
        "-IVoucherRepository _voucherRepository",
        "-ITicketApiClient _ticketApiClient",
        "-IEventApiClient _eventApiClient"
    ], methods=[
        "+SoftDeleteVoucherAsync(id: int, currentUserId: int, isAdmin: bool, isOrganizer: bool): Task&lt;bool&gt;"
    ], x=380, y=470, width=620)
    
    b.add_class("irepo", "IVoucherRepository", methods=[
        "+GetByIdAsync(id: int, includeDeleted: bool): Task&lt;Voucher?&gt;",
        "+UpdateAsync(voucher: Voucher): Task&lt;Voucher&gt;"
    ], x=400, y=690, width=580, is_interface=True)
    
    b.add_class("repo", "VoucherRepository", attributes=[
        "-PaymentDbContext _context"
    ], methods=[
        "+GetByIdAsync(id: int, includeDeleted: bool): Task&lt;Voucher?&gt;",
        "+UpdateAsync(voucher: Voucher): Task&lt;Voucher&gt;"
    ], x=400, y=860, width=580)
    
    b.add_class("voucher", "Voucher", attributes=[
        "+VoucherId: int",
        "+Code: string",
        "+Scope: string",
        "+OrganizerId: int?",
        "+DiscountType: string",
        "+DiscountAmount: long?",
        "+TotalQuantity: int",
        "+UsedQuantity: int",
        "+IsActive: bool",
        "+IsDeleted: bool",
        "+DeletedAt: DateTime?",
        "+DeletedBy: int?"
    ], x=300, y=1060, width=260)
    
    b.add_class("usage", "VoucherUsage", attributes=[
        "+VoucherUsageId: int",
        "+VoucherId: int",
        "+OrderId: int",
        "+UserId: int",
        "+DiscountAmount: long",
        "+UsedAt: DateTime"
    ], x=720, y=1090, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "isvc", "Dependency")
    b.add_realization("svc", "isvc", "Implements")
    b.add_dependency("svc", "irepo", "Dependency")
    b.add_realization("repo", "irepo", "Implements")
    b.add_dependency("repo", "voucher", "Dependency")
    b.add_association("voucher", "usage", "Association")
    
    return b.build_xml()

print("Group 1 class diagram generators ready.")
