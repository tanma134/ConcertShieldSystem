# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from builder import SequenceDiagramBuilder

def generate_uc_11_1():
    # UC_11.1 View Policy Version History (Price/Refund/Queue/Risk) (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "PolicyVersioningPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":AdminPoliciesRoute", "type": "standard", "cx": 1000},
        {"name": ":SystemPoliciesController", "type": "standard", "cx": 1300},
        {"name": "AdminDB", "type": "db", "cx": 1600}
    ]
    b = SequenceDiagramBuilder("UC_11.1_ViewPolicyVersionHistory", lifelines, 1900, 1800)
    b.add_message("1", "Select policy category (Price/Refund/Queue/Risk)", "Admin", "PolicyVersioningPage")
    b.add_message("2", "GET /api/admin/policies/history?category={cat}", "PolicyVersioningPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("4.2", "Display error message", "PolicyVersioningPage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/admin/policies/history", "GatewayAPI", ":AdminPoliciesRoute")
    b.add_self_call("6", "Check policy (Admin)", ":AdminPoliciesRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":AdminPoliciesRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":AdminPoliciesRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("7.3", "Display error message", "PolicyVersioningPage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetPolicyHistory(category)", ":AdminPoliciesRoute", ":SystemPoliciesController")
    b.add_message("9", "Query system policy version history records", ":SystemPoliciesController", "AdminDB")
    b.add_message("10", "Return policy version rows", "AdminDB", ":SystemPoliciesController", is_return=True)
    b.add_message("11", "Return 200 OK (List<PolicyVersionDTO>)", ":SystemPoliciesController", ":AdminPoliciesRoute", is_return=True)
    b.add_message("12", "Forward 200 OK", ":AdminPoliciesRoute", "GatewayAPI", is_return=True)
    b.add_message("13", "Return 200 OK", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("14", "Render policy timeline & changelog", "PolicyVersioningPage", "Admin", is_return=True)
    return b.build_xml()

def generate_uc_11_2():
    # UC_11.2 Create New Policy Version (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "PolicyVersioningPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":AdminPoliciesRoute", "type": "standard", "cx": 1000},
        {"name": ":SystemPoliciesController", "type": "standard", "cx": 1300},
        {"name": "AdminDB", "type": "db", "cx": 1600}
    ]
    b = SequenceDiagramBuilder("UC_11.2_CreateNewPolicyVersion", lifelines, 1900, 2400)
    b.add_message("1", "Fill policy form & submit", "Admin", "PolicyVersioningPage")
    b.add_message("2", "POST /api/admin/policies (dto)", "PolicyVersioningPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("4.2", "Display error message", "PolicyVersioningPage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/admin/policies", "GatewayAPI", ":AdminPoliciesRoute")
    b.add_self_call("6", "Check policy (Admin)", ":AdminPoliciesRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":AdminPoliciesRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":AdminPoliciesRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("7.3", "Display error message", "PolicyVersioningPage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "CreatePolicyVersion(request)", ":AdminPoliciesRoute", ":SystemPoliciesController")
    b.add_self_call("9", "Validate policy fields", ":SystemPoliciesController")
    f3 = b.start_opt_frame("validation failed", "Admin", ":SystemPoliciesController")
    b.add_message("10.1", "Return 400 Bad Request", ":SystemPoliciesController", ":AdminPoliciesRoute", is_return=True)
    b.add_message("10.2", "Return 400 Bad Request", ":AdminPoliciesRoute", "GatewayAPI", is_return=True)
    b.add_message("10.3", "Return 400 Bad Request", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("10.4", "Display error message", "PolicyVersioningPage", "Admin", is_return=True)
    b.end_opt_frame(f3)
    
    b.add_message("11", "Query latest policy version number", ":SystemPoliciesController", "AdminDB")
    b.add_message("12", "Return latest version_number", "AdminDB", ":SystemPoliciesController", is_return=True)
    b.add_message("13", "Archive previous policy version", ":SystemPoliciesController", "AdminDB")
    b.add_message("14", "Return archive result", "AdminDB", ":SystemPoliciesController", is_return=True)
    b.add_message("15", "Insert new policy version", ":SystemPoliciesController", "AdminDB")
    b.add_message("16", "Return inserted policy row", "AdminDB", ":SystemPoliciesController", is_return=True)
    
    b.add_self_call("17", "Check save result", ":SystemPoliciesController")
    fa = b.start_alt_frame("save failed", "save success", "Admin", ":SystemPoliciesController")
    # Branch 1: save failed
    b.add_message("18.1", "Return 500 Internal Server Error", ":SystemPoliciesController", ":AdminPoliciesRoute", is_return=True)
    b.add_message("18.2", "Return 500 Internal Server Error", ":AdminPoliciesRoute", "GatewayAPI", is_return=True)
    b.add_message("18.3", "Return 500 Internal Server Error", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("18.4", "Display error message", "PolicyVersioningPage", "Admin", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("19", "Return 200 OK (PolicyVersionDTO)", ":SystemPoliciesController", ":AdminPoliciesRoute", is_return=True)
    b.add_message("20", "Forward 200 OK", ":AdminPoliciesRoute", "GatewayAPI", is_return=True)
    b.add_message("21", "Return 200 OK", "GatewayAPI", "PolicyVersioningPage", is_return=True)
    b.add_message("22", "Display new policy version created alert", "PolicyVersioningPage", "Admin", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

def generate_uc_11_3():
    # UC_11.3 Submit Organizer Verification Documents (Organizer)
    lifelines = [
        {"name": "Organizer", "type": "actor", "cx": 100},
        {"name": "RequestOrganizerPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":OrganizerVerificationRoute", "type": "standard", "cx": 1000},
        {"name": ":OrganizerVerificationController", "type": "standard", "cx": 1300},
        {"name": "AuthenticationDB", "type": "db", "cx": 1600}
    ]
    b = SequenceDiagramBuilder("UC_11.3_SubmitOrganizerVerificationDocuments", lifelines, 1900, 2400)
    b.add_message("1", "Upload documents & submit", "Organizer", "RequestOrganizerPage")
    b.add_message("2", "POST /api/organizer-verification/submit", "RequestOrganizerPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Organizer", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "RequestOrganizerPage", is_return=True)
    b.add_message("4.2", "Display error message", "RequestOrganizerPage", "Organizer", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/organizer-verification/submit", "GatewayAPI", ":OrganizerVerificationRoute")
    b.add_message("6", "SubmitVerification(form)", ":OrganizerVerificationRoute", ":OrganizerVerificationController")
    
    b.add_self_call("7", "Validate company & tax code", ":OrganizerVerificationController")
    f2 = b.start_opt_frame("validation failed", "Organizer", ":OrganizerVerificationController")
    b.add_message("8.1", "Return 400 Bad Request", ":OrganizerVerificationController", ":OrganizerVerificationRoute", is_return=True)
    b.add_message("8.2", "Return 400 Bad Request", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("8.3", "Return 400 Bad Request", "GatewayAPI", "RequestOrganizerPage", is_return=True)
    b.add_message("8.4", "Display error message", "RequestOrganizerPage", "Organizer", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("9", "Insert verification request", ":OrganizerVerificationController", "AuthenticationDB")
    b.add_message("10", "Return request_id", "AuthenticationDB", ":OrganizerVerificationController", is_return=True)
    b.add_message("11", "Insert verification documents", ":OrganizerVerificationController", "AuthenticationDB")
    b.add_message("12", "Return document insert result", "AuthenticationDB", ":OrganizerVerificationController", is_return=True)
    
    b.add_self_call("13", "Check save result", ":OrganizerVerificationController")
    fa = b.start_alt_frame("save failed", "save success", "Organizer", ":OrganizerVerificationController")
    # Branch 1: save failed
    b.add_message("14.1", "Return 500 Internal Server Error", ":OrganizerVerificationController", ":OrganizerVerificationRoute", is_return=True)
    b.add_message("14.2", "Return 500 Internal Server Error", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("14.3", "Return 500 Internal Server Error", "GatewayAPI", "RequestOrganizerPage", is_return=True)
    b.add_message("14.4", "Display error message", "RequestOrganizerPage", "Organizer", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("15", "Return 200 OK (OrganizerVerificationDetailDTO)", ":OrganizerVerificationController", ":OrganizerVerificationRoute", is_return=True)
    b.add_message("16", "Forward 200 OK", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("17", "Return 200 OK", "GatewayAPI", "RequestOrganizerPage", is_return=True)
    b.add_message("18", "Display 'Documents submitted, pending review' banner", "RequestOrganizerPage", "Organizer", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

def generate_uc_11_4():
    # UC_11.4 Review Organizer Verification Documents (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "OrganizerRequestsAdminPage", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":OrganizerVerificationRoute", "type": "standard", "cx": 1000},
        {"name": ":OrganizerVerificationController", "type": "standard", "cx": 1300},
        {"name": "AuthenticationDB", "type": "db", "cx": 1600}
    ]
    b = SequenceDiagramBuilder("UC_11.4_ReviewOrganizerVerificationDocuments", lifelines, 1900, 1900)
    b.add_message("1", "Click on verification request row", "Admin", "OrganizerRequestsAdminPage")
    b.add_message("2", "GET /api/organizer-verification/{id}", "OrganizerRequestsAdminPage", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "OrganizerRequestsAdminPage", is_return=True)
    b.add_message("4.2", "Display error message", "OrganizerRequestsAdminPage", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward GET /api/organizer-verification/{id}", "GatewayAPI", ":OrganizerVerificationRoute")
    b.add_self_call("6", "Check policy (Admin)", ":OrganizerVerificationRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":OrganizerVerificationRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "OrganizerRequestsAdminPage", is_return=True)
    b.add_message("7.3", "Display error message", "OrganizerRequestsAdminPage", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "GetVerificationDetail(id)", ":OrganizerVerificationRoute", ":OrganizerVerificationController")
    b.add_message("9", "Query request & user profile", ":OrganizerVerificationController", "AuthenticationDB")
    b.add_message("10", "Return request & user row", "AuthenticationDB", ":OrganizerVerificationController", is_return=True)
    b.add_message("11", "Query documents list", ":OrganizerVerificationController", "AuthenticationDB")
    b.add_message("12", "Return documents list", "AuthenticationDB", ":OrganizerVerificationController", is_return=True)
    b.add_message("13", "Return 200 OK (data)", ":OrganizerVerificationController", ":OrganizerVerificationRoute", is_return=True)
    b.add_message("14", "Forward 200 OK", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("15", "Return 200 OK", "GatewayAPI", "OrganizerRequestsAdminPage", is_return=True)
    b.add_message("16", "Open review modal & render document previews", "OrganizerRequestsAdminPage", "Admin", is_return=True)
    return b.build_xml()

def generate_uc_11_5():
    # UC_11.5 Approve/Reject Organizer Verification (Admin)
    lifelines = [
        {"name": "Admin", "type": "actor", "cx": 100},
        {"name": "OrganizerVerificationReviewModal", "type": "standard", "cx": 400},
        {"name": "GatewayAPI", "type": "standard", "cx": 700},
        {"name": ":OrganizerVerificationRoute", "type": "standard", "cx": 1000},
        {"name": ":OrganizerVerificationController", "type": "standard", "cx": 1300},
        {"name": "AuthenticationDB", "type": "db", "cx": 1600}
    ]
    b = SequenceDiagramBuilder("UC_11.5_ApproveRejectOrganizerVerification", lifelines, 1900, 2500)
    b.add_message("1", "Click 'Approve' (or 'Reject' with reason)", "Admin", "OrganizerVerificationReviewModal")
    b.add_message("2", "POST /api/organizer-verification/{id}/decision", "OrganizerVerificationReviewModal", "GatewayAPI")
    
    b.add_self_call("3", "Validate JWT Token", "GatewayAPI")
    f1 = b.start_opt_frame("token invalid", "Admin", "GatewayAPI")
    b.add_message("4.1", "Return 401 Unauthorized", "GatewayAPI", "OrganizerVerificationReviewModal", is_return=True)
    b.add_message("4.2", "Display error message", "OrganizerVerificationReviewModal", "Admin", is_return=True)
    b.end_opt_frame(f1)
    
    b.add_message("5", "Forward POST /api/organizer-verification/{id}/decision", "GatewayAPI", ":OrganizerVerificationRoute")
    b.add_self_call("6", "Check policy (Admin)", ":OrganizerVerificationRoute")
    f2 = b.start_opt_frame("role is not Admin", "Admin", ":OrganizerVerificationRoute")
    b.add_message("7.1", "Return 403 Forbidden", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("7.2", "Return 403 Forbidden", "GatewayAPI", "OrganizerVerificationReviewModal", is_return=True)
    b.add_message("7.3", "Display error message", "OrganizerVerificationReviewModal", "Admin", is_return=True)
    b.end_opt_frame(f2)
    
    b.add_message("8", "ApproveVerification(id)", ":OrganizerVerificationRoute", ":OrganizerVerificationController")
    b.add_message("9", "Update request status ('Approved')", ":OrganizerVerificationController", "AuthenticationDB")
    b.add_message("10", "Return request update result", "AuthenticationDB", ":OrganizerVerificationController", is_return=True)
    b.add_message("11", "Update role to 'Organizer'", ":OrganizerVerificationController", "AuthenticationDB")
    b.add_message("12", "Return user role update result", "AuthenticationDB", ":OrganizerVerificationController", is_return=True)
    
    b.add_self_call("13", "Check save result", ":OrganizerVerificationController")
    fa = b.start_alt_frame("save failed", "save success", "Admin", ":OrganizerVerificationController")
    # Branch 1: save failed
    b.add_message("14.1", "Rollback transaction & Return 500 Internal Server Error", ":OrganizerVerificationController", ":OrganizerVerificationRoute", is_return=True)
    b.add_message("14.2", "Return 500 Internal Server Error", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("14.3", "Return 500 Internal Server Error", "GatewayAPI", "OrganizerVerificationReviewModal", is_return=True)
    b.add_message("14.4", "Display error message", "OrganizerVerificationReviewModal", "Admin", is_return=True)
    
    b.split_alt_frame(fa)
    
    # Branch 2: save success
    b.add_message("15", "Commit transaction & Return 200 OK", ":OrganizerVerificationController", ":OrganizerVerificationRoute", is_return=True)
    b.add_message("16", "Forward 200 OK", ":OrganizerVerificationRoute", "GatewayAPI", is_return=True)
    b.add_message("17", "Return 200 OK", "GatewayAPI", "OrganizerVerificationReviewModal", is_return=True)
    b.add_message("18", "Close modal & update request status in table", "OrganizerVerificationReviewModal", "Admin", is_return=True)
    b.end_alt_frame(fa)
    
    return b.build_xml()

print("gen_group3.py updated.")
