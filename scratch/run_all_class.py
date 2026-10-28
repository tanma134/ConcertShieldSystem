# -*- coding: utf-8 -*-
import os
import sys

sys.path.append(os.path.dirname(__file__))

from gen_class_group1 import (
    generate_uc_7_5_class, generate_uc_7_6_class, generate_uc_7_7_class,
    generate_uc_7_8_class, generate_uc_7_9_class, generate_uc_7_10_class, generate_uc_7_11_class
)
from gen_class_group2 import (
    generate_uc_8_7_class, generate_uc_8_8_class, generate_uc_8_10_class
)
from gen_class_group3 import (
    generate_uc_11_1_class, generate_uc_11_2_class, generate_uc_11_3_class,
    generate_uc_11_4_class, generate_uc_11_5_class
)
from gen_class_group4 import (
    generate_uc_14_1_class, generate_uc_14_5_class, generate_uc_14_6_class, generate_uc_14_7_class
)
from gen_class_group5 import (
    generate_uc_15_1_class, generate_uc_15_2_class, generate_uc_15_3_class,
    generate_uc_15_4_class, generate_uc_15_5_class
)
from gen_class_group6 import (
    generate_uc_20_1_class, generate_uc_20_2_class, generate_uc_20_3_class
)

CLASS_DIAGRAM_DIR = r"d:\BEN9\3\ConcertShieldSystem\diagrams_class"
os.makedirs(CLASS_DIAGRAM_DIR, exist_ok=True)

generators = [
    ("49_UC_7.5_ViewOrganizerVouchers_Class.xml", generate_uc_7_5_class),
    ("50_UC_7.6_CreateOrganizerVoucher_Class.xml", generate_uc_7_6_class),
    ("51_UC_7.7_ViewVoucherUsageDetails_Class.xml", generate_uc_7_7_class),
    ("52_UC_7.8_ViewSystemVouchers_Class.xml", generate_uc_7_8_class),
    ("53_UC_7.9_CreateSystemVoucher_Class.xml", generate_uc_7_9_class),
    ("54_UC_7.10_UpdateSystemVoucher_Class.xml", generate_uc_7_10_class),
    ("55_UC_7.11_DeleteSystemVoucher_Class.xml", generate_uc_7_11_class),
    ("66_UC_8.7_BulkRevokeTickets_Class.xml", generate_uc_8_7_class),
    ("67_UC_8.8_ViewTicketRevocationHistory_Class.xml", generate_uc_8_8_class),
    ("69_UC_8.10_ViewReissueHistory_Class.xml", generate_uc_8_10_class),
    ("85_UC_11.1_ViewPolicyVersionHistory_Class.xml", generate_uc_11_1_class),
    ("86_UC_11.2_CreateNewPolicyVersion_Class.xml", generate_uc_11_2_class),
    ("87_UC_11.3_SubmitOrganizerVerificationDocuments_Class.xml", generate_uc_11_3_class),
    ("88_UC_11.4_ReviewOrganizerVerificationDocuments_Class.xml", generate_uc_11_4_class),
    ("89_UC_11.5_ApproveRejectOrganizerVerification_Class.xml", generate_uc_11_5_class),
    ("105_UC_14.1_MonitorRealtimeCheckinStatus_Class.xml", generate_uc_14_1_class),
    ("109_UC_14.5_EnableOfflineCheckinMode_Class.xml", generate_uc_14_5_class),
    ("110_UC_14.6_SyncOfflineCheckinRecords_Class.xml", generate_uc_14_6_class),
    ("111_UC_14.7_ResolveOfflineSyncConflicts_Class.xml", generate_uc_14_7_class),
    ("112_UC_15.1_ViewGateAssignment_Class.xml", generate_uc_15_1_class),
    ("113_UC_15.2_ScanDynamicQRTicket_Class.xml", generate_uc_15_2_class),
    ("114_UC_15.3_ManualCheckin_Class.xml", generate_uc_15_3_class),
    ("115_UC_15.4_LookupTicket_Class.xml", generate_uc_15_4_class),
    ("116_UC_15.5_ViewCheckinScanHistory_Class.xml", generate_uc_15_5_class),
    ("155_UC_20.1_ViewNotifications_Class.xml", generate_uc_20_1_class),
    ("156_UC_20.2_MarkNotificationAsRead_Class.xml", generate_uc_20_2_class),
    ("157_UC_20.3_DeleteNotification_Class.xml", generate_uc_20_3_class),
]

for filename, func in generators:
    filepath = os.path.join(CLASS_DIAGRAM_DIR, filename)
    xml_content = func()
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(xml_content)
    print(f"Generated {filename} ({len(xml_content)} bytes)")

print("\nSUCCESS: All 27 Class Diagram XML files generated successfully in diagrams_class/!")
