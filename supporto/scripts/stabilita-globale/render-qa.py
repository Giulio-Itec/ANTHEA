from pathlib import Path
import sys
import pypdfium2 as pdfium
from PIL import Image, ImageDraw

root = Path(sys.argv[1]).resolve()
for path in sorted(root.glob('*.pdf')):
    doc = pdfium.PdfDocument(str(path))
    folder = root / path.stem
    folder.mkdir(exist_ok=True)
    tiles = []
    for i, page in enumerate(doc):
        image = page.render(scale=1.2).to_pil().convert('RGB')
        image.save(folder / f'page-{i+1}.png')
        preview = image.copy(); preview.thumbnail((360, 510))
        tile = Image.new('RGB', (380, 540), 'white')
        tile.paste(preview, ((380-preview.width)//2, 22))
        ImageDraw.Draw(tile).text((8, 3), f'{path.stem} | {i+1}', fill='black')
        tiles.append(tile)
    for start in range(0, len(tiles), 8):
        group = tiles[start:start+8]
        overview = Image.new('RGB', (380*4, 540*((len(group)+3)//4)), '#d4d9df')
        for i, tile in enumerate(group): overview.paste(tile, ((i%4)*380, (i//4)*540))
        overview.save(root / f'{path.stem}-overview-{start//8+1}.png')
    print(path.stem, len(doc), 'pages')
