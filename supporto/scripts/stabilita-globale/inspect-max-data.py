"""Read-only inspection of a locally saved MAX model for round-trip checks."""
from pathlib import Path
import struct
import sys

b = Path(sys.argv[1]).read_bytes()
start, end = map(int, sys.argv[2:4])
for i in range(start, min(end, len(b)-7), 8):
    value = struct.unpack_from('<d', b, i)[0]
    print(i, f'{value:.12g}', b[i:i+8].hex())
