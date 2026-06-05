#!/usr/bin/env python3
"""
Patch the bundle: replace ONLY the ExportRosterPage function with the updated v2
that includes (Not Set) options for Rank and Deserve Car Class filters.

Uses brace-counting to find the exact end of the function, avoiding accidental
deletion of subsequent functions.
"""
import sys

# Use the known-good v1 bundle as the base (has the working Export page without Not Set)
BUNDLE_IN  = '/tmp/bundle_export_roster.js'
BUNDLE_OUT = '/tmp/bundle_export_roster_v3.js'
NEW_COMP   = '/home/ubuntu/new_export_roster_v2.js'

content = open(BUNDLE_IN, encoding='utf-8').read()
new_comp = open(NEW_COMP, encoding='utf-8').read()

# Find the start of the ExportRosterPage function
func_marker = 'function ExportRosterPage()'
func_start = content.find(func_marker)
if func_start == -1:
    print('ERROR: ExportRosterPage function not found in bundle', file=sys.stderr)
    sys.exit(1)

print(f'ExportRosterPage starts at: {func_start}')

# Use brace counting to find the exact end of the function
depth = 0
in_string = False
string_char = None
i = func_start
func_end = -1

while i < len(content):
    ch = content[i]
    if in_string:
        if ch == '\\':
            i += 2
            continue
        if ch == string_char:
            in_string = False
    else:
        if ch in ('"', "'", '`'):
            in_string = True
            string_char = ch
        elif ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                func_end = i + 1  # include the closing brace
                break
    i += 1

if func_end == -1:
    print('ERROR: Could not find end of ExportRosterPage function', file=sys.stderr)
    sys.exit(1)

print(f'ExportRosterPage ends at: {func_end}')
print(f'Function length: {func_end - func_start} chars')
print(f'After function: {repr(content[func_end:func_end+60])}')

# Verify the end marker is correct
expected_after = '};function O6('
if not content[func_end:].startswith(expected_after):
    print(f'WARNING: After function is: {repr(content[func_end:func_end+30])}')
    print(f'Expected: {repr(expected_after)}')
    # Still proceed - brace counting is reliable

# Replace only the ExportRosterPage function
patched = content[:func_start] + new_comp + content[func_end:]

# Sanity checks
assert '__UNSET__' in patched, 'UNSET sentinel not found'
assert '(Not Set)' in patched, '(Not Set) label not found'
assert 'unsetRankCount' in patched, 'unsetRankCount not found'
assert 'function O6(' in patched, 'O6 function missing - accidental deletion!'
assert 'function qm(' in patched, 'qm function missing - accidental deletion!'
assert 'function SC(' in patched, 'SC function missing - accidental deletion!'
assert patched.count('function ExportRosterPage()') == 1, 'Duplicate ExportRosterPage'

open(BUNDLE_OUT, 'w', encoding='utf-8').write(patched)
print(f'\nPatched bundle written to {BUNDLE_OUT}')
print(f'Original size: {len(content):,}  Patched size: {len(patched):,}')
print(f'Size difference: {len(patched) - len(content):+,} chars')
