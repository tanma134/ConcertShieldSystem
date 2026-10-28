# -*- coding: utf-8 -*-
import sys
import os
sys.path.append(os.path.dirname(__file__))
from class_builder import ClassDiagramBuilder

def generate_uc_11_1_class():
    b = ClassDiagramBuilder("UC_11.1_ViewPolicyVersionHistory", 1300, 1100)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("dto", "PolicyVersionDTO", attributes=[
        "+PolicyId: int",
        "+Category: string",
        "+VersionCode: string",
        "+VersionNumber: int",
        "+Title: string",
        "+Scope: string",
        "+ChangeSummary: string",
        "+Content: string",
        "+Status: string",
        "+EffectiveDate: DateTime",
        "+CreatedAt: DateTime",
        "+CreatedByAdminName: string"
    ], x=40, y=160, width=280)
    
    b.add_class("ctrl", "SystemPoliciesController", attributes=[
        "-IConfiguration _configuration",
        "-ILogger&lt;SystemPoliciesController&gt; _logger",
        "-string _connectionString"
    ], methods=[
        "+GetPolicyVersions(category, status, search, page, pageSize): Task&lt;IActionResult&gt;"
    ], x=440, y=160, width=520)
    
    b.add_class("model", "SystemPolicyVersion", attributes=[
        "+PolicyId: int",
        "+Category: string",
        "+VersionCode: string",
        "+VersionNumber: int",
        "+Title: string",
        "+Scope: string",
        "+ChangeSummary: string",
        "+Content: string",
        "+Status: string",
        "+EffectiveDate: DateTime",
        "+CreatedAt: DateTime",
        "+CreatedByAdminId: int"
    ], x=560, y=360, width=280)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "dto", "Dependency")
    b.add_dependency("ctrl", "model", "Dependency")
    
    return b.build_xml()

def generate_uc_11_2_class():
    b = ClassDiagramBuilder("UC_11.2_CreateNewPolicyVersion", 1300, 1100)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("req_dto", "CreatePolicyVersionRequestDTO", attributes=[
        "+Category: string",
        "+Title: string",
        "+Scope: string",
        "+ChangeSummary: string",
        "+Content: string",
        "+EffectiveDate: DateTime?"
    ], x=40, y=160, width=280)
    
    b.add_class("res_dto", "PolicyVersionDTO", attributes=[
        "+PolicyId: int",
        "+Category: string",
        "+VersionCode: string",
        "+VersionNumber: int",
        "+Title: string",
        "+Scope: string",
        "+Status: string",
        "+EffectiveDate: DateTime"
    ], x=40, y=360, width=280)
    
    b.add_class("ctrl", "SystemPoliciesController", attributes=[
        "-IConfiguration _configuration",
        "-ILogger&lt;SystemPoliciesController&gt; _logger",
        "-string _connectionString"
    ], methods=[
        "+CreatePolicyVersion(dto: CreatePolicyVersionRequestDTO): Task&lt;IActionResult&gt;"
    ], x=440, y=160, width=520)
    
    b.add_class("model", "SystemPolicyVersion", attributes=[
        "+PolicyId: int",
        "+Category: string",
        "+VersionCode: string",
        "+VersionNumber: int",
        "+Title: string",
        "+Scope: string",
        "+Content: string",
        "+Status: string",
        "+EffectiveDate: DateTime",
        "+CreatedAt: DateTime",
        "+CreatedByAdminId: int"
    ], x=560, y=360, width=280)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "req_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "model", "Dependency")
    
    return b.build_xml()

def generate_uc_11_3_class():
    b = ClassDiagramBuilder("UC_11.3_SubmitOrganizerVerificationDocuments", 1400, 1200)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("submit_dto", "SubmitVerificationDTO", attributes=[
        "+CompanyName: string",
        "+TaxCode: string",
        "+RepresentativeName: string",
        "+RepresentativeCccd: string",
        "+DocumentFiles: List&lt;IFormFile&gt;?",
        "+DocumentTypes: List&lt;string&gt;?"
    ], x=40, y=160, width=280)
    
    b.add_class("res_dto", "VerificationRequestResponseDTO", attributes=[
        "+RequestId: int",
        "+UserId: int",
        "+Status: string",
        "+SubmittedAt: DateTime",
        "+Message: string"
    ], x=40, y=360, width=280)
    
    b.add_class("ctrl", "OrganizerVerificationController", attributes=[
        "-AuthenticationDbContext _dbContext",
        "-ILogger&lt;OrganizerVerificationController&gt; _logger"
    ], methods=[
        "+SubmitVerification(dto: SubmitVerificationDTO): Task&lt;IActionResult&gt;"
    ], x=420, y=160, width=540)
    
    b.add_class("req_model", "OrganizerVerificationRequest", attributes=[
        "+RequestId: int",
        "+UserId: int",
        "+CompanyName: string",
        "+TaxCode: string",
        "+RepresentativeName: string",
        "+Status: string",
        "+SubmittedAt: DateTime"
    ], x=340, y=380, width=280)
    
    b.add_class("doc_model", "OrganizerVerificationDocument", attributes=[
        "+DocumentId: int",
        "+RequestId: int",
        "+DocumentType: string",
        "+FileName: string",
        "+FileUrl: string",
        "+UploadedAt: DateTime"
    ], x=740, y=380, width=280)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "submit_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "req_model", "Dependency")
    b.add_dependency("ctrl", "doc_model", "Dependency")
    b.add_association("req_model", "doc_model", "Association")
    
    return b.build_xml()

def generate_uc_11_4_class():
    b = ClassDiagramBuilder("UC_11.4_ReviewOrganizerVerificationDocuments", 1400, 1200)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("res_dto", "VerificationRequestResponseDTO", attributes=[
        "+RequestId: int",
        "+UserId: int",
        "+CompanyName: string",
        "+TaxCode: string",
        "+Status: string",
        "+SubmittedAt: DateTime",
        "+Documents: List&lt;VerificationDocumentItemDTO&gt;"
    ], x=40, y=160, width=280)
    
    b.add_class("doc_dto", "VerificationDocumentItemDTO", attributes=[
        "+DocumentId: int",
        "+RequestId: int",
        "+DocumentType: string",
        "+FileName: string",
        "+FileUrl: string",
        "+PreviewUrl: string",
        "+UploadedAt: DateTime"
    ], x=40, y=370, width=280)
    
    b.add_class("ctrl", "OrganizerVerificationController", attributes=[
        "-AuthenticationDbContext _dbContext"
    ], methods=[
        "+GetVerificationRequests(status, search, page, limit): Task&lt;IActionResult&gt;"
    ], x=440, y=160, width=520)
    
    b.add_class("req_model", "OrganizerVerificationRequest", attributes=[
        "+RequestId: int",
        "+UserId: int",
        "+CompanyName: string",
        "+TaxCode: string",
        "+Status: string",
        "+SubmittedAt: DateTime"
    ], x=340, y=380, width=280)
    
    b.add_class("doc_model", "OrganizerVerificationDocument", attributes=[
        "+DocumentId: int",
        "+RequestId: int",
        "+DocumentType: string",
        "+FileUrl: string",
        "+UploadedAt: DateTime"
    ], x=740, y=380, width=280)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "req_model", "Dependency")
    b.add_dependency("ctrl", "doc_model", "Dependency")
    b.add_association("res_dto", "doc_dto", "Association")
    b.add_association("req_model", "doc_model", "Association")
    
    return b.build_xml()

def generate_uc_11_5_class():
    b = ClassDiagramBuilder("UC_11.5_ApproveRejectOrganizerVerification", 1400, 1200)
    b.add_class("cb", "ControllerBase", x=580, y=30, width=180)
    
    b.add_class("review_dto", "ReviewVerificationDTO", attributes=[
        "+Decision: string",
        "+AdminNote: string?",
        "+RejectionReason: string?"
    ], x=40, y=160, width=260)
    
    b.add_class("res_dto", "VerificationRequestResponseDTO", attributes=[
        "+RequestId: int",
        "+UserId: int",
        "+Status: string",
        "+ReviewedAt: DateTime?",
        "+Message: string"
    ], x=40, y=320, width=260)
    
    b.add_class("ctrl", "OrganizerVerificationController", attributes=[
        "-AuthenticationDbContext _dbContext",
        "-ILogger&lt;OrganizerVerificationController&gt; _logger"
    ], methods=[
        "+ReviewVerification(requestId: int, dto: ReviewVerificationDTO): Task&lt;IActionResult&gt;"
    ], x=440, y=160, width=520)
    
    b.add_class("req_model", "OrganizerVerificationRequest", attributes=[
        "+RequestId: int",
        "+UserId: int",
        "+Status: string",
        "+ReviewedByAdminId: int?",
        "+ReviewedAt: DateTime?",
        "+AdminNote: string?",
        "+RejectionReason: string?"
    ], x=340, y=380, width=280)
    
    b.add_class("user_model", "User", attributes=[
        "+UserId: int",
        "+Email: string",
        "+IsVerified: bool",
        "+EkycStatus: string",
        "+UpdatedAt: DateTime"
    ], x=740, y=380, width=260)
    
    b.add_inheritance("ctrl", "cb", "Inheritance")
    b.add_dependency("ctrl", "review_dto", "Dependency")
    b.add_dependency("ctrl", "res_dto", "Dependency")
    b.add_dependency("ctrl", "req_model", "Dependency")
    b.add_dependency("ctrl", "user_model", "Dependency")
    b.add_association("req_model", "user_model", "Association")
    
    return b.build_xml()

print("Group 3 class diagram generators ready.")
