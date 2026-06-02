#!/usr/bin/env python3
"""
Patch the bundle: replace the ExportRosterPage function with the updated v2
that includes (Not Set) options for Rank and Deserve Car Class filters.
"""
import sys

BUNDLE_IN  = '/home/ubuntu/AnnualMeetingsRepo/src/IsDB.Hospitality.API/wwwroot/assets/index-carclass-hist-v16.js'
BUNDLE_OUT = '/tmp/bundle_export_roster_v2.js'
NEW_COMP   = '/home/ubuntu/new_export_roster_v2.js'

content = open(BUNDLE_IN, encoding='utf-8').read()
new_comp = open(NEW_COMP, encoding='utf-8').read()

# Find the start of the current ExportRosterPage function
func_marker = 'function ExportRosterPage()'
func_start = content.find(func_marker)
if func_start == -1:
    print('ERROR: ExportRosterPage function not found in bundle', file=sys.stderr)
    sys.exit(1)

# Find the end: the next top-level function after ExportRosterPage is SC(
end_marker = '\nfunction SC('
func_end = content.find(end_marker, func_start)
if func_end == -1:
    print('ERROR: Could not find end marker (function SC) in bundle', file=sys.stderr)
    sys.exit(1)

print(f'Replacing ExportRosterPage: chars {func_start}..{func_end} ({func_end - func_start} chars)')

# Replace the old function with the new one
patched = content[:func_start] + new_comp + content[func_end:]

# Sanity checks
assert 'UNSET_LABEL' in patched, 'UNSET_LABEL not found in patched bundle'
assert '__UNSET__' in patched, '__UNSET__ sentinel not found in patched bundle'
assert '(Not Set)' in patched, '(Not Set) label not found in patched bundle'
assert 'unsetRankCount' in patched, 'unsetRankCount not found in patched bundle'
assert 'unsetCarClassCount' in patched, 'unsetCarClassCount not found in patched bundle'
assert patched.count('function ExportRosterPage()') == 1, 'Duplicate ExportRosterPage found'

open(BUNDLE_OUT, 'w', encoding='utf-8').write(patched)
print(f'Patched bundle written to {BUNDLE_OUT}')
print(f'Original size: {len(content):,}  Patched size: {len(patched):,}')
