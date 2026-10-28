# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_7_8():
    # UC_7.8 View System Vouchers (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "AdminVouchersPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":Voucher", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.8_ViewSystemVouchers", lifelines, 2800, 2000)
    b.add_message("1", "Navigate to System Vouchers page", "Admin", "AdminVouchersPage")
    b.add_message("2", "GET /api/v1/vouchers?scope=SYSTEM (page, limit)", "AdminVouchersPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("4.2", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/v1/vouchers", "GatewayAPI", ":VouchersRoute")
    b.add_self_call("6", "Check policy (Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":VouchersRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("7.3", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetVouchers(scope='SYSTEM', paging)", ":VouchersRoute", ":VouchersController")
    b.add_message("9", "GetVouchersAsync(filter, isAdmin=true)", ":VouchersController", ":VoucherService")
    b.add_message("10", "GetPagedAsync(filter, paging)", ":VoucherService", ":VoucherRepository")
    b.add_message("11", "Query DbSet Vouchers (SYSTEM)", ":VoucherRepository", ":Voucher")
    b.add_message("12", "Query system vouchers", ":Voucher", "PaymentDB")
    b.add_message("13", "Return rows", "PaymentDB", ":Voucher", is_return=True)
    b.add_message("14", "Return entity list", ":Voucher", ":VoucherRepository", is_return=True)
    b.add_message("15", "Return PagedResultDTO<Voucher>", ":VoucherRepository", ":VoucherService", is_return=True)
    b.add_message("16", "Return PagedResultDTO<VoucherResponseDTO>", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("17", "Return 200 OK (data)", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("18", "Forward 200 OK", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("19", "Return 200 OK (system vouchers)", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("20", "Display system vouchers table", "AdminVouchersPage", "Admin", is_return=True)
    return b.build_xml()

def generate_uc_7_9():
    # UC_7.9 Create System Voucher (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "AdminVouchersPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":Voucher", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.9_CreateSystemVoucher", lifelines, 2800, 2600)
    b.add_message("1", "Fill System Voucher form & click 'Create'", "Admin", "AdminVouchersPage")
    b.add_self_call("2", "Validate input fields", "AdminVouchersPage")
    f0 = b.start_opt_frame("invalid form fields", "Admin", "AdminVouchersPage")
    b.add_message("2.1", "Display form validation error", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f0)
    
    b.add_message("3", "POST /api/v1/vouchers (scope='SYSTEM')", "AdminVouchersPage", "GatewayAPI")
    b.add_self_call("4", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("5.1", "Return 401 Unauthorized", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("5.2", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("6", "Forward POST /api/v1/vouchers", "GatewayAPI", ":VouchersRoute")
    b.add_self_call("7", "Check policy (Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":VouchersRoute")
    b.add_message("8.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("8.2", "Return 403 Forbidden", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("8.3", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("9", "CreateVoucher(dto)", ":VouchersRoute", ":VouchersController")
    b.add_message("10", "CreateVoucherAsync(dto, isAdmin=true)", ":VouchersController", ":VoucherService")
    
    b.add_self_call("11", "Validate voucher constraints", ":VoucherService")
    f3 = b.start_opt_frame("validation failed", "Admin", ":VoucherService")
    b.add_message("12.1", "Return error (ArgumentException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("12.2", "Return 400 Bad Request", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("12.3", "Return 400 Bad Request", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("12.4", "Return 400 Bad Request", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("12.5", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("13", "CodeExistsAsync(code)", ":VoucherService", ":VoucherRepository")
    b.add_message("14", "Check code uniqueness", ":VoucherRepository", "PaymentDB")
    b.add_message("15", "Return count", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("16", "Return exists (bool)", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("17", "Check code existence", ":VoucherService")
    f4 = b.start_opt_frame("code already exists", "Admin", ":VoucherService")
    b.add_message("18.1", "Return error (InvalidOperationException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("18.2", "Return 400 Bad Request", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("18.3", "Return 400 Bad Request", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("18.4", "Return 400 Bad Request", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("18.5", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f4)
    
    b.add_message("19", "CreateAsync(voucher)", ":VoucherService", ":VoucherRepository")
    b.add_message("20", "AddAsync(voucher)", ":VoucherRepository", ":Voucher")
    b.add_message("21", "SaveChangesAsync()", ":VoucherRepository", "PaymentDB")
    b.add_message("22", "Insert system voucher", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("23", "Return created voucher", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("24", "Check save result", ":VoucherService")
    fa = b.start_alt_frame("save failed", "save success", "Admin", ":VoucherService")
    # Branch 1: save failed
    b.add_message("25.1", "Return error (DbUpdateException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("25.2", "Return 500 Internal Server Error", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("25.3", "Return 500 Internal Server Error", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("25.4", "Return 500 Internal Server Error", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("25.5", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("26", "Return VoucherResponseDTO", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("27", "Return 201 Created", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("28", "Forward 201 Created", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("29", "Return 201 Created", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("30", "Close modal & refresh system vouchers", "AdminVouchersPage", "Admin", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

def generate_uc_7_10():
    # UC_7.10 Update System Voucher (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "EditVoucherModal", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":Voucher", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.10_UpdateSystemVoucher", lifelines, 2800, 2600)
    b.add_message("1", "Modify voucher fields & click 'Update'", "Admin", "EditVoucherModal")
    b.add_message("2", "PUT /api/v1/vouchers/{id} (UpdateVoucherDTO)", "EditVoucherModal", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "EditVoucherModal", is_return=True)
    b.add_message("4.2", "Display error message", "EditVoucherModal", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward PUT /api/v1/vouchers/{id}", "GatewayAPI", ":VouchersRoute")
    b.add_self_call("6", "Check policy (Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":VouchersRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "EditVoucherModal", is_return=True)
    b.add_message("7.3", "Display error message", "EditVoucherModal", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "UpdateVoucher(id, dto)", ":VouchersRoute", ":VouchersController")
    b.add_message("9", "UpdateVoucherAsync(id, dto, isAdmin=true)", ":VouchersController", ":VoucherService")
    b.add_message("10", "GetByIdAsync(id)", ":VoucherService", ":VoucherRepository")
    b.add_message("11", "Query voucher details by ID", ":VoucherRepository", "PaymentDB")
    b.add_message("12", "Return voucher (or null)", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("13", "Return voucher entity", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("14", "Check voucher existence", ":VoucherService")
    f3 = b.start_opt_frame("voucher is null", "Admin", ":VoucherService")
    b.add_message("15.1", "Return error (KeyNotFoundException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("15.2", "Return 404 Not Found", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("15.3", "Return 404 Not Found", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("15.4", "Return 404 Not Found", "GatewayAPI", "EditVoucherModal", is_return=True)
    b.add_message("15.5", "Display error message", "EditVoucherModal", "Admin", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_self_call("16", "Check mutability rules", ":VoucherService")
    f4 = b.start_opt_frame("voucher already used, immutable field changed", "Admin", ":VoucherService")
    b.add_message("17.1", "Return error (InvalidOperationException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("17.2", "Return 400 Bad Request", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("17.3", "Return 400 Bad Request", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("17.4", "Return 400 Bad Request", "GatewayAPI", "EditVoucherModal", is_return=True)
    b.add_message("17.5", "Display error message", "EditVoucherModal", "Admin", is_return=True)
    b.end_opt_frame(f4)
    
    b.add_message("18", "UpdateAsync(voucher)", ":VoucherService", ":VoucherRepository")
    b.add_message("19", "Update entity in DbSet", ":VoucherRepository", ":Voucher")
    b.add_message("20", "Save changes (Update voucher)", ":VoucherRepository", "PaymentDB")
    b.add_message("21", "Return updated rows", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("22", "Return updated voucher", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("23", "Check save result", ":VoucherService")
    fa = b.start_alt_frame("save failed", "save success", "Admin", ":VoucherService")
    # Branch 1: save failed
    b.add_message("24.1", "Return error (DbUpdateException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("24.2", "Return 500 Internal Server Error", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("24.3", "Return 500 Internal Server Error", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("24.4", "Return 500 Internal Server Error", "GatewayAPI", "EditVoucherModal", is_return=True)
    b.add_message("24.5", "Display error message", "EditVoucherModal", "Admin", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("25", "Return VoucherResponseDTO", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("26", "Return 200 OK", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("27", "Forward 200 OK", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("28", "Return 200 OK", "GatewayAPI", "EditVoucherModal", is_return=True)
    b.add_message("29", "Close modal & notify update successful", "EditVoucherModal", "Admin", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

def generate_uc_7_11():
    # UC_7.11 Delete System Voucher (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "AdminVouchersPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":VouchersRoute", "type": "standard", "cx": 1000},
        {"name": ":VouchersController", "type": "standard", "cx": 1300},
        {"name": ":VoucherService", "type": "standard", "cx": 1600},
        {"name": ":VoucherRepository", "type": "standard", "cx": 1900},
        {"name": ":Voucher", "type": "standard", "cx": 2200},
        {"name": "PaymentDB", "type": "db", "cx": 2500}
    ]
    b = SequenceDiagramBuilder("UC_7.11_DeleteSystemVoucher", lifelines, 2800, 2400)
    b.add_message("1", "Click 'Delete Voucher' button", "Admin", "AdminVouchersPage")
    b.add_message("2", "DELETE /api/v1/vouchers/{id}", "AdminVouchersPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("4.2", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward DELETE /api/v1/vouchers/{id}", "GatewayAPI", ":VouchersRoute")
    b.add_self_call("6", "Check policy (Admin)", ":VouchersRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":VouchersRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("7.3", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "DeleteVoucher(id)", ":VouchersRoute", ":VouchersController")
    b.add_message("9", "SoftDeleteVoucherAsync(id, isAdmin=true)", ":VouchersController", ":VoucherService")
    b.add_message("10", "GetByIdAsync(id)", ":VoucherService", ":VoucherRepository")
    b.add_message("11", "Query voucher by ID", ":VoucherRepository", "PaymentDB")
    b.add_message("12", "Return voucher (or null)", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("13", "Return voucher entity", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("14", "Check voucher existence", ":VoucherService")
    f3 = b.start_opt_frame("voucher is null", "Admin", ":VoucherService")
    b.add_message("15.1", "Return error (KeyNotFoundException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("15.2", "Return 404 Not Found", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("15.3", "Return 404 Not Found", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("15.4", "Return 404 Not Found", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("15.5", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("16", "UpdateAsync(is_deleted=true)", ":VoucherService", ":VoucherRepository")
    b.add_message("17", "Soft delete voucher", ":VoucherRepository", "PaymentDB")
    b.add_message("18", "Return update result", "PaymentDB", ":VoucherRepository", is_return=True)
    b.add_message("19", "Return updated voucher", ":VoucherRepository", ":VoucherService", is_return=True)
    
    b.add_self_call("20", "Check save result", ":VoucherService")
    fa = b.start_alt_frame("save failed", "save success", "Admin", ":VoucherService")
    # Branch 1: save failed
    b.add_message("21.1", "Return error (DbUpdateException)", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("21.2", "Return 500 Internal Server Error", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("21.3", "Return 500 Internal Server Error", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("21.4", "Return 500 Internal Server Error", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("21.5", "Display error message", "AdminVouchersPage", "Admin", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("22", "Return true", ":VoucherService", ":VouchersController", is_return=True)
    b.add_message("23", "Return 200 OK (id, message)", ":VouchersController", ":VouchersRoute", is_return=True)
    b.add_message("24", "Forward 200 OK", ":VouchersRoute", "GatewayAPI", is_return=True)
    b.add_message("25", "Return 200 OK", "GatewayAPI", "AdminVouchersPage", is_return=True)
    b.add_message("26", "Remove voucher from view & show success toast", "AdminVouchersPage", "Admin", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

print("gen_group1.py updated.")
