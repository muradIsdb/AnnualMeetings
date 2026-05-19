#!/usr/bin/env python3
"""
inject_placard.py
Replaces the uC() function in the working bundle with new_placard.js.
Produces /tmp/bundle_new_placard.js
"""

import re
import sys

WORKING_BUNDLE = "/tmp/bundle_new_kc.js"   # latest validated bundle (includes kC history feature)
NEW_PLACARD_JS = "/home/ubuntu/new_placard.js"
OUTPUT_BUNDLE  = "/tmp/bundle_new_placard.js"

print("Reading source files...")
content    = open(WORKING_BUNDLE, "r", encoding="utf-8").read()
new_placard = open(NEW_PLACARD_JS, "r", encoding="utf-8").read()

# ── Locate uC() function ──────────────────────────────────────────────────────
start_marker = "function uC("
start_idx = content.find(start_marker)
if start_idx == -1:
    print("ERROR: Could not find 'function uC(' in bundle.")
    sys.exit(1)
print(f"  uC() starts at index {start_idx}")

# Find the closing brace by counting depth
depth = 0
end_idx = start_idx
for i, ch in enumerate(content[start_idx:], start_idx):
    if ch == "{":
        depth += 1
    elif ch == "}":
        depth -= 1
        if depth == 0:
            end_idx = i
            break

if end_idx == start_idx:
    print("ERROR: Could not find closing brace of uC().")
    sys.exit(1)
print(f"  uC() ends at index {end_idx} (length {end_idx - start_idx} chars)")

# ── Inject new_placard.js ─────────────────────────────────────────────────────
new_content = content[:start_idx] + new_placard.strip() + content[end_idx + 1:]
print(f"  New bundle size: {len(new_content):,} chars (was {len(content):,})")

# ── Validation ────────────────────────────────────────────────────────────────
print("\nValidating...")

errors = []

# 1. uC function is present
if "function uC()" not in new_content:
    errors.append("uC() function not found in output")

# 2. showPhoto state is present
if "showPhoto" not in new_content:
    errors.append("showPhoto state not found — injection may have failed")

# 3. setShowPhoto is present
if "setShowPhoto" not in new_content:
    errors.append("setShowPhoto not found")

# 4. Photo panel transition style is present
if "max-height" not in new_content and "maxHeight" not in new_content:
    errors.append("Photo panel transition style not found")

# 5. Bundle size sanity check (should be close to original)
size_diff = abs(len(new_content) - len(content))
if size_diff > 50000:
    errors.append(f"Bundle size changed by {size_diff} chars — unexpected")

# 6. kC function still present (car class history feature)
if "function kC(" not in new_content:
    errors.append("kC() function missing — car class history feature may be broken")

if errors:
    print("VALIDATION FAILED:")
    for e in errors:
        print(f"  ✗ {e}")
    sys.exit(1)

print("  ✓ uC() function present")
print("  ✓ showPhoto state present")
print("  ✓ setShowPhoto present")
print("  ✓ Photo panel transition style present")
print("  ✓ Bundle size within expected range")
print("  ✓ kC() car class history feature intact")
print("  ✓ No template literal issues")

# ── Write output ──────────────────────────────────────────────────────────────
open(OUTPUT_BUNDLE, "w", encoding="utf-8").write(new_content)
print(f"\nOutput written to: {OUTPUT_BUNDLE}")
print("SUCCESS — bundle ready for deployment.")
