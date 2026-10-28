# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_7_5():
    # UC_7.5 View Organizer Vouchers (Organizer)
    # Lifelines with 300px spacing
    lifelines = [
        {"name": "Organizer", "type": "actor", "cx": 100},
        {"name": "VoucherManagementPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":Voucher", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.5_ViewOrganizerVouchers", lifelines, 2800, 2000)
    
    b.add_message("1", "Click 'Voucher Management'", "Organizer", "VoucherManagementPage")
    b.add_message("2", "GET /api/v1/vouchers (scope, page, limit)", "VoucherManagementPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Organizer", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "VoucherManagementPage", is_return=True)
    b.add_message("4.2", "Display error message", "VoucherManagementPage", "Organizer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/v1/vouchers", "GatewayAPI", ":VouchersRoute")
    
    b.add_self_call("6", "Check policy (Organizer/Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Organizer/Admin", "Organizer", ":VouchersRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "VoucherManagementPage", is_return=True)
    b.add_message("7.3", "Display error message", "VoucherManagementPage", "Organizer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetVouchers(scope, filter, paging)", ":VouchersRoute", ":VouchersController")
    b.add_message("9", "GetVouchersAsync(filter, userContext)", ":VouchersController", ":VoucherService")
    
    b.add_self_call("10", "Enforce organizer scope filter", ":VoucherService")
    b.add_message("11", "GetPagedAsync(filter, paging)", ":VoucherService", ":VoucherRepository")
    b.add_message("12", "Query DbSet Vouchers with filter", ":VoucherRepository", ":Voucher")
    b.add_message("13", "Query active organizer vouchers", ":Voucher", "PaymentDB")
    b.add_message("14", "Return rows", "PaymentDB", ":Voucher", is_return=True)
    b.add_message("15", "Return entity list", ":Voucher", ":VoucherRepository", is_return=True)
    b.add_message("16", "Return PagedResultDTO<Voucher>", ":VoucherRepository", ":VoucherService", is_return=True)
    b.add_message("17", "Return PagedResultDTO<VoucherResponseDTO>", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("18", "Return 200 OK (data)", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("19", "Forward 200 OK", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("20", "Return 200 OK (vouchers data)", "GatewayAPI", "VoucherManagementPage", is_return=True)
    b.add_message("21", "Display vouchers list", "VoucherManagementPage", "Organizer", is_return=True)
    
    return b.build_xml()

def generate_uc_7_6():
    # UC_7.6 Create Organizer Voucher (Organizer)
    lifelines = [
        {"name": "Organizer", "type": "actor", "cx": 100},
        {"name": "CreateVoucherModal", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":Voucher", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.6_CreateOrganizerVoucher", lifelines, 2800, 2600)
    
    b.add_message("1", "Fill voucher form & click 'Create'", "Organizer", "CreateVoucherModal")
    b.add_self_call("2", "Validate form input fields", "CreateVoucherModal")
    f0 = b.start_opt_frame("invalid form fields", "Organizer", "CreateVoucherModal")
    b.add_message("2.1", "Display validation error", "CreateVoucherModal", "Organizer", is_return=True)
    b.end_opt_frame(f0)
    
    b.add_message("3", "POST /api/v1/vouchers (CreateVoucherDTO)", "CreateVoucherModal", "GatewayAPI")
    b.add_self_call("4", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Organizer", "GatewayAPI")
    b.add_message("5.1", "Return 401 Unauthorized", "GatewayAPI", "CreateVoucherModal", is_return=True)
    b.add_message("5.2", "Display error message", "CreateVoucherModal", "Organizer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("6", "Forward POST /api/v1/vouchers", "GatewayAPI", ":VouchersRoute")
    b.add_self_call("7", "Check policy (Organizer/Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Organizer/Admin", "Organizer", ":VouchersRoute")
    b.add_message("8.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("8.2", "Return 403 Forbidden", "GatewayAPI", "CreateVoucherModal", is_return=True)
    b.add_message("8.3", "Display error message", "CreateVoucherModal", "Organizer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("9", "CreateVoucher(dto)", ":VouchersRoute", ":VouchersController")
    b.add_message("10", "CreateVoucherAsync(dto, userContext)", ":VouchersController", ":VoucherService")
    
    b.add_self_call("11", "Validate voucher constraints", ":VoucherService")
    f3 = b.start_opt_frame("validation failed", "Organizer", ":VoucherService")
    b.add_message("12.1", "Return error (ArgumentException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("12.2", "Return 400 Bad Request", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("12.3", "Return 400 Bad Request", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("12.4", "Return 400 Bad Request", "GatewayAPI", "CreateVoucherModal", is_return=True)
    b.add_message("12.5", "Display error message", "CreateVoucherModal", "Organizer", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("13", "CodeExistsAsync(code)", ":VoucherService", ":VoucherRepository")
    b.add_message("14", "Query code in DbSet Vouchers", ":VoucherRepository", ":Voucher")
    b.add_message("15", "Check code uniqueness", ":Voucher", "PaymentDB")
    b.add_message("16", "Return count", "PaymentDB", ":Voucher", is_return=True)
    b.add_message("17", "Return bool exists", ":Voucher", ":VoucherRepository", is_return=True)
    b.add_message("18", "Return bool exists", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("19", "Check code existence", ":VoucherService")
    f4 = b.start_opt_frame("code already exists", "Organizer", ":VoucherService")
    b.add_message("20.1", "Return error (InvalidOperationException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("20.2", "Return 400 Bad Request", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("20.3", "Return 400 Bad Request", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("20.4", "Return 400 Bad Request", "GatewayAPI", "CreateVoucherModal", is_return=True)
    b.add_message("20.5", "Display error message", "CreateVoucherModal", "Organizer", is_return=True)
    b.end_opt_frame(f4)
    
    b.add_message("21", "CreateAsync(voucher)", ":VoucherService", ":VoucherRepository")
    b.add_message("22", "AddAsync(voucher) to DbSet", ":VoucherRepository", ":Voucher")
    b.add_message("23", "SaveChangesAsync()", ":VoucherRepository", "PaymentDB")
    b.add_message("24", "Insert voucher record", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("25", "Return created voucher", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("26", "Check save result", ":VoucherService")
    fa = b.start_alt_frame("save failed", "save success", "Organizer", ":VoucherService")
    # Branch 1: save failed
    b.add_message("27.1", "Return error (DbUpdateException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("27.2", "Return 500 Internal Server Error", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("27.3", "Return 500 Internal Server Error", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("27.4", "Return 500 Internal Server Error", "GatewayAPI", "CreateVoucherModal", is_return=True)
    b.add_message("27.5", "Display error message", "CreateVoucherModal", "Organizer", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("28", "Return VoucherResponseDTO", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("29", "Return 201 Created (VoucherResponseDTO)", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("30", "Forward 201 Created", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("31", "Return 201 Created", "GatewayAPI", "CreateVoucherModal", is_return=True)
    b.add_message("32", "Close modal & display success alert", "CreateVoucherModal", "Organizer", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

def generate_uc_7_7():
    # UC_7.7 View Voucher Usage Details (Organizer)
    lifelines = [
        {"name": "Organizer", "type": "actor", "cx": 100},
        {"name": "VoucherUsageModal", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":VoucherUsage", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.7_ViewVoucherUsageDetails", lifelines, 2800, 2200)
    
    b.add_message("1", "Click 'View Usages' on voucher row", "Organizer", "VoucherUsageModal")
    b.add_message("2", "GET /api/v1/vouchers/{id}/usages", "VoucherUsageModal", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Organizer", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "VoucherUsageModal", is_return=True)
    b.add_message("4.2", "Display error message", "VoucherUsageModal", "Organizer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/v1/vouchers/{id}/usages", "GatewayAPI", ":VouchersRoute")
    b.add_self_call("6", "Check policy (Organizer/Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Organizer/Admin", "Organizer", ":VouchersRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "VoucherUsageModal", is_return=True)
    b.add_message("7.3", "Display error message", "VoucherUsageModal", "Organizer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetVoucherUsages(id)", ":VouchersRoute", ":VouchersController")
    b.add_message("9", "GetVoucherUsagesAsync(id, userContext)", ":VouchersController", ":VoucherService")
    b.add_message("10", "GetByIdAsync(id)", ":VoucherService", ":VoucherRepository")
    b.add_message("11", "Query voucher details by ID", ":VoucherRepository", "PaymentDB")
    b.add_message("12", "Return voucher row (or null)", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("13", "Return voucher entity", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("14", "Check voucher existence", ":VoucherService")
    f3 = b.start_opt_frame("voucher is null", "Organizer", ":VoucherService")
    b.add_message("15.1", "Return error (KeyNotFoundException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("15.2", "Return 404 Not Found", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("15.3", "Return 404 Not Found", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("15.4", "Return 404 Not Found", "GatewayAPI", "VoucherUsageModal", is_return=True)
    b.add_message("15.5", "Display error message", "VoucherUsageModal", "Organizer", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_self_call("16", "Check voucher ownership", ":VoucherService")
    f4 = b.start_opt_frame("caller is not owner", "Organizer", ":VoucherService")
    b.add_message("17.1", "Return error (UnauthorizedAccessException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("17.2", "Return 403 Forbidden", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("17.3", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("17.4", "Return 403 Forbidden", "GatewayAPI", "VoucherUsageModal", is_return=True)
    b.add_message("17.5", "Display error message", "VoucherUsageModal", "Organizer", is_return=True)
    b.end_opt_frame(f4)
    
    b.add_message("18", "GetUsagesAsync(id)", ":VoucherService", ":VoucherRepository")
    b.add_message("19", "Query DbSet VoucherUsages", ":VoucherRepository", ":VoucherUsage")
    b.add_message("20", "Query voucher usage history", ":VoucherUsage", "PaymentDB")
    b.add_message("21", "Return usage rows", "PaymentDB", ":VoucherUsage", is_return=True)
    b.add_message("22", "Return entity list", ":VoucherUsage", ":VoucherRepository", is_return=True)
    b.add_message("23", "Return List<VoucherUsage>", ":VoucherRepository", ":VoucherService", is_return=True)
    b.add_message("24", "Map to List<VoucherUsageDTO>", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("25", "Return 200 OK (data)", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("26", "Forward 200 OK", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("27", "Return 200 OK", "GatewayAPI", "VoucherUsageModal", is_return=True)
    b.add_message("28", "Render voucher usages table", "VoucherUsageModal", "Organizer", is_return=True)
    
    return b.build_xml()

print("generate_all.py updated.")
