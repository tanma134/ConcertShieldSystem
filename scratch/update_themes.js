const fs = require('fs');
const path = require('path');

function replaceInFile(filePath, replacements) {
  const fullPath = path.resolve(filePath);
  if (!fs.existsSync(fullPath)) {
    console.warn(`File not found: ${fullPath}`);
    return;
  }
  let content = fs.readFileSync(fullPath, 'utf8');
  for (const [from, to] of replacements) {
    if (typeof from === 'string') {
      content = content.split(from).join(to);
    } else {
      content = content.replace(from, to);
    }
  }
  fs.writeFileSync(fullPath, content, 'utf8');
  console.log(`Updated theme for ${filePath}`);
}

// 1. TicketGovernancePage.css
replaceInFile('frontend/src/pages/admin/TicketGovernancePage.css', [
  ['#1e293b', '#15151c'],
  ['#0f172a', '#0c0c10'],
  ['#334155', '#232332'],
  ['#475569', '#2e2e40'],
  ['#2563eb', '#ff2d75'],
  ['#3b82f6', '#ff2d75'],
  ['rgba(37, 99, 235,', 'rgba(255, 45, 117,'],
  ['rgba(59, 130, 246,', 'rgba(255, 45, 117,'],
  ['border-color: #3b82f6', 'border-color: #ff2d75'],
  ['border-color: #2563eb', 'border-color: #ff2d75']
]);

// 2. SystemPoliciesPage.css
replaceInFile('frontend/src/pages/admin/SystemPoliciesPage.css', [
  ['background: white;', 'background: #15151c;'],
  ['background-color: white;', 'background-color: #15151c;'],
  ['color: #1e293b;', 'color: #f8fafc;'],
  ['color: #0f172a;', 'color: #ffffff;'],
  ['border: 1px solid #e2e8f0;', 'border: 1px solid #232332;'],
  ['border-bottom: 1px solid #e2e8f0;', 'border-bottom: 1px solid #232332;'],
  ['border-top: 1px solid #e2e8f0;', 'border-top: 1px solid #232332;'],
  ['background: #f8fafc;', 'background: #191924;'],
  ['background: #f1f5f9;', 'background: #1e1e2e;'],
  ['background: #4f46e5;', 'background: #ff2d75;'],
  ['background: #4338ca;', 'background: #e02467;'],
  ['color: #6d28d9;', 'color: #ff2d75;'],
  ['background: #ede9fe;', 'background: rgba(255, 45, 117, 0.15);'],
  ['#4f46e5', '#ff2d75'],
  ['#4338ca', '#e02467']
]);

// 3. OrganizerVerificationsPage.css
replaceInFile('frontend/src/pages/admin/OrganizerVerificationsPage.css', [
  ['background: white;', 'background: #15151c;'],
  ['background-color: white;', 'background-color: #15151c;'],
  ['color: #0f172a;', 'color: #ffffff;'],
  ['border: 1px solid #e2e8f0;', 'border: 1px solid #232332;'],
  ['border-bottom: 1px solid #e2e8f0;', 'border-bottom: 1px solid #232332;'],
  ['border: 1px solid #cbd5e1;', 'border: 1px solid #2e2e40;'],
  ['background: #f8fafc;', 'background: #191924;'],
  ['background: #f1f5f9;', 'background: #1e1e2e;'],
  ['background: #4f46e5;', 'background: #ff2d75;'],
  ['background: #4338ca;', 'background: #e02467;'],
  ['border-color: #4f46e5;', 'border-color: #ff2d75;'],
  ['#4f46e5', '#ff2d75'],
  ['#4338ca', '#e02467'],
  ['color: #475569;', 'color: #94a3b8;']
]);

// 4. SubmitOrganizerVerificationPage.css
replaceInFile('frontend/src/pages/organizer/SubmitOrganizerVerificationPage.css', [
  ['background: white;', 'background: #15151c;'],
  ['background-color: white;', 'background-color: #15151c;'],
  ['color: #0f172a;', 'color: #ffffff;'],
  ['border: 1px solid #e2e8f0;', 'border: 1px solid #232332;'],
  ['border-bottom: 1px solid #e2e8f0;', 'border-bottom: 1px solid #232332;'],
  ['border: 1px solid #cbd5e1;', 'border: 1px solid #2e2e40;'],
  ['background: #f8fafc;', 'background: #191924;'],
  ['background: #f1f5f9;', 'background: #1e1e2e;'],
  ['#0d9488', '#ff2d75'],
  ['#2563eb', '#ff2d75'],
  ['#1d4ed8', '#e02467'],
  ['#3b82f6', '#ff2d75']
]);

// 5. RealtimeCheckinMonitorPage.css
replaceInFile('frontend/src/pages/organizer/RealtimeCheckinMonitorPage.css', [
  ['#0f172a', '#0c0c10'],
  ['#1e293b', '#15151c'],
  ['#334155', '#232332'],
  ['#475569', '#2e2e42'],
  ['linear-gradient(135deg, #7c3aed, #ec4899)', 'linear-gradient(135deg, #ff2d75, #ec4899)']
]);

// 6. NotificationCenterPage.css
replaceInFile('frontend/src/pages/organizer/NotificationCenterPage.css', [
  ['var(--tb-border)', '#232332'],
  ['var(--tb-bg)', '#0c0c10'],
  ['var(--tb-surface)', '#15151c'],
  ['var(--tb-surface-2)', '#1a1a26'],
  ['var(--tb-primary-2)', '#ff2d75'],
  ['var(--tb-primary-light)', 'rgba(255, 45, 117, 0.15)']
]);

// 7. VoucherManagementPage.css
replaceInFile('frontend/src/pages/admin/VoucherManagementPage.css', [
  ['background: #ffffff;', 'background: #15151c;'],
  ['background: #f3f4f6;', 'background: #1a1a26;'],
  ['color: #111827;', 'color: #ffffff;'],
  ['color: #4b5563;', 'color: #94a3b8;'],
  ['border: 1px solid #e5e7eb;', 'border: 1px solid #232332;'],
  ['border: 1px dashed #d1d5db;', 'border: 1px dashed #35354a;'],
  ['#3b82f6', '#ff2d75'],
  ['#2563eb', '#e02467'],
  ['#6d28d9', '#ff2d75'],
  ['background: #ede9fe;', 'background: rgba(255, 45, 117, 0.15); color: #ff2d75;'],
  ['color: #6d28d9;', 'color: #ff2d75;'],
  ['background: #e0f2fe;', 'background: rgba(56, 189, 248, 0.15); color: #38bdf8;'],
  ['background: #fef3c7;', 'background: rgba(245, 158, 11, 0.15); color: #fbbf24;']
]);

console.log('All 7 module themes updated successfully to Hot Pink & Black!');
