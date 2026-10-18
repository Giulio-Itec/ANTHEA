"""Create an analysis copy with editable inputs; never modify the reference or its formulas."""
import sys, zipfile, re
from pathlib import Path
source, target = map(Path, sys.argv[1:])
if target.exists(): raise SystemExit('Refusing to overwrite an existing capture workbook')
with zipfile.ZipFile(source) as src, zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as dst:
    for item in src.infolist():
        data=src.read(item.filename)
        if item.filename.startswith('xl/worksheets/sheet') and item.filename.endswith('.xml'):
            data=re.sub(rb'<sheetProtection\b[^>]*/>', b'', data)
        dst.writestr(item,data)
