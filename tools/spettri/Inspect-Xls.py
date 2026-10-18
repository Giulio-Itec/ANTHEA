"""Read-only BIFF/VBA extraction for the Spettri-NTC equivalence bench. No macros run."""
import argparse
import json
import struct
import sys
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('source', type=Path)
p.add_argument('output', type=Path)
p.add_argument('--dependencies', type=Path, required=True)
a = p.parse_args()
sys.path.insert(0, str(a.dependencies.resolve()))
import xlrd
from xlrd.formula import decompile_formula, FMLA_TYPE_CELL
from oletools.olevba import VBA_Parser

a.output.mkdir(parents=True, exist_ok=True)
w = xlrd.open_workbook(a.source, on_demand=True, formatting_info=True)
for i, s in enumerate(w.sheets()):
    values = [[s.cell_value(r, c) for c in range(s.ncols)] for r in range(s.nrows)]
    formulas = {}
    w._position = w._sh_abs_posn[i]
    while True:
        code, size, data = w.get_record_parts()
        if code == 0x000A: break
        if code not in (0x0006, 0x0206, 0x0406): continue
        r, c = struct.unpack('<HH', data[:4])
        length = struct.unpack('<H', data[20:22])[0]
        try:
            formula = decompile_formula(w, data[22:], length, FMLA_TYPE_CELL, r, c)
        except Exception as e:
            formula = 'UNPARSED: ' + str(e)
        formulas[xlrd.formula.colname(c) + str(r + 1)] = formula
    name = s.name.strip()
    (a.output / (name + '.json')).write_text(json.dumps({'name': s.name, 'values': values, 'formulas': formulas}, ensure_ascii=False), encoding='utf-8')
    with (a.output / (name + '.txt')).open('w', encoding='utf-8') as f:
        for r, row in enumerate(values):
            for c, v in enumerate(row):
                if v == '': continue
                address = xlrd.formula.colname(c) + str(r+1)
                f.write(f'{address}: {v}' + (f' = {formulas[address]}' if address in formulas else '') + '\n')
    print(s.name, s.nrows, s.ncols, 'formulas', len(formulas))
vba = VBA_Parser(str(a.source))
for _, _, name, content in vba.extract_macros():
    (a.output / Path(name).name).write_text(content, encoding='utf-8')
vba.close()
