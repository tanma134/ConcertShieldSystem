from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def text(path):
    return (ROOT / path).read_text(encoding="utf-8")

checks = {
    "coordinates removed from create form": "name=\"latitude\"" not in text("frontend/src/pages/organizer/steps/StepInfo.jsx") and "name=\"longitude\"" not in text("frontend/src/pages/organizer/steps/StepInfo.jsx"),
    "chart synchronizes ticket quantity": "SynchronizeTicketQuantitiesAsync" in text("backend/EventAPI/Services/SeatingService.cs"),
    "direct placed-ticket quantity edits blocked": "Quantity is managed by seating-zone capacity" in text("backend/EventAPI/Services/TicketTypeService.cs"),
    "template zone overrides supported": "SeatsPerRow = mapping.SeatsPerRow" in text("backend/EventAPI/Services/SeatingTemplateService.cs"),
    "zone edit supports grid resize": "ReplaceSeatsAsync" in text("backend/EventAPI/Services/SeatingService.cs"),
    "standalone organizer configuration routes": "/organizer/events/:id/${path}" in text("frontend/src/routes/AppRoutes.jsx"),
    "admin system template page": "AdminSeatingTemplatesPage" in text("frontend/src/routes/AppRoutes.jsx"),
    "admin approval contains no editor": "StepTicketsSeating" not in text("frontend/src/pages/admin/AdminEventDetailPage.jsx"),
    "dynamic pricing conditional fields": "adjustmentMode" in text("frontend/src/pages/organizer/steps/StepTicketsSeating.jsx"),
    "refund policies support editing": "Save policy" in text("frontend/src/pages/organizer/steps/StepTicketsSeating.jsx"),
}

failed = [name for name, passed in checks.items() if not passed]
for name, passed in checks.items():
    print(f"{'PASS' if passed else 'FAIL'}: {name}")
if failed:
    raise SystemExit(f"{len(failed)} contract test(s) failed")
print(f"PASS: {len(checks)} event/seating contract tests")
