# -*- coding: utf-8 -*-
import os
import sys

sys.path.append(os.path.dirname(__file__))

from generate_all import generate_uc_7_5, generate_uc_7_6, generate_uc_7_7
from gen_group1 import generate_uc_7_8, generate_uc_7_9, generate_uc_7_10, generate_uc_7_11
from gen_group2 import generate_uc_8_7, generate_uc_8_8, generate_uc_8_10
from gen_group3 import generate_uc_11_1, generate_uc_11_2, generate_uc_11_3, generate_uc_11_4, generate_uc_11_5
from gen_group4 import generate_uc_14_1, generate_uc_14_5, generate_uc_14_6, generate_uc_14_7
from gen_group5 import generate_uc_15_1, generate_uc_15_2, generate_uc_15_3, generate_uc_15_4, generate_uc_15_5
from gen_group6 import generate_uc_20_1, generate_uc_20_2, generate_uc_20_3

DIAGRAM_DIR = r"d:\BEN9\3\ConcertShieldSystem\diagrams"
os.makedirs(DIAGRAM_DIR, exist_ok=True)

generators = [
    ("49_UC_7.5_ViewOrganizerVouchers.xml", generate_uc_7_5),
    ("50_UC_7.6_CreateOrganizerVoucher.xml", generate_uc_7_6),
    ("51_UC_7.7_ViewVoucherUsageDetails.xml", generate_uc_7_7),
    ("52_UC_7.8_ViewSystemVouchers.xml", generate_uc_7_8),
    ("53_UC_7.9_CreateSystemVoucher.xml", generate_uc_7_9),
    ("54_UC_7.10_UpdateSystemVoucher.xml", generate_uc_7_10),
    ("55_UC_7.11_DeleteSystemVoucher.xml", generate_uc_7_11),
    ("66_UC_8.7_BulkRevokeTickets.xml", generate_uc_8_7),
    ("67_UC_8.8_ViewTicketRevocationHistory.xml", generate_uc_8_8),
    ("69_UC_8.10_ViewReissueHistory.xml", generate_uc_8_10),
    ("85_UC_11.1_ViewPolicyVersionHistory.xml", generate_uc_11_1),
    ("86_UC_11.2_CreateNewPolicyVersion.xml", generate_uc_11_2),
    ("87_UC_11.3_SubmitOrganizerVerificationDocuments.xml", generate_uc_11_3),
    ("88_UC_11.4_ReviewOrganizerVerificationDocuments.xml", generate_uc_11_4),
    ("89_UC_11.5_ApproveRejectOrganizerVerification.xml", generate_uc_11_5),
    ("105_UC_14.1_MonitorRealtimeCheckinStatus.xml", generate_uc_14_1),
    ("109_UC_14.5_EnableOfflineCheckinMode.xml", generate_uc_14_5),
    ("110_UC_14.6_SyncOfflineCheckinRecords.xml", generate_uc_14_6),
    ("111_UC_14.7_ResolveOfflineSyncConflicts.xml", generate_uc_14_7),
    ("112_UC_15.1_ViewGateAssignment.xml", generate_uc_15_1),
    ("113_UC_15.2_ScanDynamicQRTicket.xml", generate_uc_15_2),
    ("114_UC_15.3_ManualCheckin.xml", generate_uc_15_3),
    ("115_UC_15.4_LookupTicket.xml", generate_uc_15_4),
    ("116_UC_15.5_ViewCheckinScanHistory.xml", generate_uc_15_5),
    ("155_UC_20.1_ViewNotifications.xml", generate_uc_20_1),
    ("156_UC_20.2_MarkNotificationAsRead.xml", generate_uc_20_2),
    ("157_UC_20.3_DeleteNotification.xml", generate_uc_20_3),
]

for filename, func in generators:
    filepath = os.path.join(DIAGRAM_DIR, filename)
    xml_content = func()
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(xml_content)
    print(f"Generated {filename} ({len(xml_content)} bytes)")

print("\nSUCCESS: All 27 sequence diagram XML files generated successfully!")
