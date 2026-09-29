"""Extract text from native MAX RTF reports, leaving the source untouched."""
from pathlib import Path
import re
import sys

def extract(data):
    out, stack = [], []
    skip, uc, fallback = False, 1, 0
    destinations = {'fonttbl', 'colortbl', 'stylesheet', 'info', 'pict', 'object', 'header', 'footer', 'filetbl', 'listtable', 'listoverridetable', 'generator'}
    pattern = re.compile(r"\\([a-zA-Z]+)(-?\d+)? ?|\\'([0-9a-fA-F]{2})|\\([^a-zA-Z])|([{}])|([^\\{}]+)")
    for m in pattern.finditer(data):
        word, number, hx, symbol, brace, plain = m.groups()
        if brace == '{': stack.append((skip, uc))
        elif brace == '}':
            if stack: skip, uc = stack.pop()
            fallback = 0
        elif word:
            if word in destinations: skip = True
            elif word == 'uc': uc = int(number)
            elif not skip:
                if word == 'u':
                    out.append(chr(int(number) % 65536)); fallback = uc
                elif word in {'par', 'row', 'line', 'page'}: out.append('\n')
                elif word in {'tab', 'cell'}: out.append('\t')
                elif word == 'emdash': out.append('—')
                elif word == 'endash': out.append('–')
        elif symbol == '*': skip = True
        elif not skip:
            value = bytes.fromhex(hx).decode('cp1252', errors='replace') if hx else ({'~': ' ', '_': '-', '-': ''}.get(symbol, symbol) if symbol else plain.replace('\r', '').replace('\n', ''))
            if fallback:
                n = min(fallback, len(value)); value = value[n:]; fallback -= n
            out.append(value)
    return ''.join(out)

if __name__ == '__main__':
    source, target = map(Path, sys.argv[1:3])
    target.write_text(extract(source.read_text(encoding='cp1252')), encoding='utf-8')
    print(target)
