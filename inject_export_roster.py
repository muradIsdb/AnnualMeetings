#!/usr/bin/env python3
"""
inject_export_roster.py
Injects the ExportRosterPage component into the working bundle.
Adds:
  1. The three helper functions + ExportRosterPage component (before the T6 nav array)
  2. A new nav item in T6 (Administration section): Export
  3. A new route: path:"export" -> ExportRosterPage
Produces /tmp/bundle_export_roster.js
"""

import re
import sys

WORKING_BUNDLE = "/home/ubuntu/AnnualMeetingsRepo/src/IsDB.Hospitality.API/wwwroot/assets/index-carclass-hist-v16.js"
NEW_COMPONENT_JS = "/home/ubuntu/new_export_roster.js"
OUTPUT_BUNDLE = "/tmp/bundle_export_roster.js"

print("Reading source files...")
content = open(WORKING_BUNDLE, "r", encoding="utf-8").read()
new_component = open(NEW_COMPONENT_JS, "r", encoding="utf-8").read()

# ── Step 1: Inject component code before T6 nav array ────────────────────────
T6_MARKER = 'T6=[{to:"/staff"'
t6_idx = content.find(T6_MARKER)
if t6_idx == -1:
    print("ERROR: Could not find T6 nav array marker.")
    sys.exit(1)
print(f"  T6 nav array found at index {t6_idx}")

# Insert component code just before T6
content = content[:t6_idx] + new_component.strip() + "\n" + content[t6_idx:]
print(f"  Component injected ({len(new_component):,} chars)")

# ── Step 2: Add Export nav item to T6 (Administration section) ───────────────
# T6 currently ends with: ...roles:[U.Admin]}]
# We append: ,{to:"/export",label:"Export",icon:fa,roles:[U.Admin]}
OLD_T6_END = '{to:"/integrations/field-mappings",label:"Field Mappings",icon:Qb,roles:[U.Admin]}]'
NEW_T6_END = '{to:"/integrations/field-mappings",label:"Field Mappings",icon:Qb,roles:[U.Admin]},{to:"/export",label:"Export",icon:fa,roles:[U.Admin]}]'

if OLD_T6_END not in content:
    print("ERROR: Could not find T6 end marker for nav item injection.")
    sys.exit(1)

content = content.replace(OLD_T6_END, NEW_T6_END, 1)
print("  Export nav item added to T6")

# ── Step 3: Add route for /export ─────────────────────────────────────────────
# Insert after the staff route:
# s.jsx(he,{path:"staff",element:s.jsx(we,{allowedRoles:[U.Admin],children:s.jsx(FC,{})})})
OLD_STAFF_ROUTE = 's.jsx(he,{path:"staff",element:s.jsx(we,{allowedRoles:[U.Admin],children:s.jsx(FC,{})})})'
NEW_STAFF_ROUTE = (
    's.jsx(he,{path:"staff",element:s.jsx(we,{allowedRoles:[U.Admin],children:s.jsx(FC,{})})})'
    ',s.jsx(he,{path:"export",element:s.jsx(we,{allowedRoles:[U.Admin],children:s.jsx(ExportRosterPage,{})})})'
)

if OLD_STAFF_ROUTE not in content:
    print("ERROR: Could not find staff route for export route injection.")
    sys.exit(1)

content = content.replace(OLD_STAFF_ROUTE, NEW_STAFF_ROUTE, 1)
print("  Export route added")

# ── Validation ────────────────────────────────────────────────────────────────
print("\nValidating...")
errors = []

if "function ExportRosterPage()" not in content:
    errors.append("ExportRosterPage() function not found in output")

if 'to:"/export",label:"Export"' not in content:
    errors.append("Export nav item not found in T6")

if 'path:"export"' not in content:
    errors.append("Export route not found")

if "function _ExportCheckboxGroup(" not in content:
    errors.append("_ExportCheckboxGroup helper not found")

if "function _ExportColumnSelector(" not in content:
    errors.append("_ExportColumnSelector helper not found")

if "_EXPORT_ALL_COLUMNS" not in content:
    errors.append("_EXPORT_ALL_COLUMNS constant not found")

# Ensure existing features are intact
for func in ["function HC()", "function kC(", "function uC()", "function SC()", "function V4("]:
    if func not in content:
        errors.append(f"{func} missing — existing feature may be broken")

# Bundle size sanity check
original_size = len(open(WORKING_BUNDLE, encoding="utf-8").read())
size_diff = len(content) - original_size
if size_diff < 0 or size_diff > 100000:
    errors.append(f"Bundle size change unexpected: {size_diff:+,} chars")

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print(f"  ✗ {e}")
    sys.exit(1)

print("  ✓ ExportRosterPage() function present")
print("  ✓ _ExportCheckboxGroup helper present")
print("  ✓ _ExportColumnSelector helper present")
print("  ✓ _EXPORT_ALL_COLUMNS constant present")
print("  ✓ Export nav item in T6")
print("  ✓ Export route registered")
print("  ✓ All existing features intact (HC, kC, uC, SC, V4)")
print(f"  ✓ Bundle size change: +{size_diff:,} chars")

# ── Write output ──────────────────────────────────────────────────────────────
open(OUTPUT_BUNDLE, "w", encoding="utf-8").write(content)
print(f"\nOutput written to: {OUTPUT_BUNDLE}")
print("SUCCESS — bundle ready for deployment.")
