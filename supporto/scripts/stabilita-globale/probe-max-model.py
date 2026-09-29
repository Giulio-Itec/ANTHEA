"""Create an isolated input-format probe, to be verified and resaved in MAX.

This is not a MAX result generator. The source is left untouched. The output is
explicitly a probe and cannot be included as a validated comparison case.
"""
from pathlib import Path
import struct
import shutil

root = Path(__file__).resolve().parents[2] / 'artefatti' / 'stabilita-globale' / 'confronti-max'
source = root / '00-base-originale'
target = root / 'prova-formato-non-validata'
if target.exists(): raise RuntimeError('La copia di prova esiste già; non sovrascrivere.')
shutil.copytree(source, target)
data = bytearray((target / 'dati.dat').read_bytes())
for offset in [16624, 16980]:
    assert struct.unpack_from('<d', data, offset)[0] == .3
    struct.pack_into('<d', data, offset, .25)
(target / 'dati.dat').write_bytes(data)
native_path = str(target).encode('cp1252')
(root / (target.name + '.mrt')).write_bytes(struct.pack('<i', len(native_path)) + native_path + b'\0\0\0\0')
print(root / (target.name + '.mrt'))
