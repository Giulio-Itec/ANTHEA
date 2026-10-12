"""Archive Rev33 at its relative path; preserve previous Markdown/PDF sources from Git."""
from pathlib import Path
import subprocess,json
from hashlib import sha256
ROOT=Path(__file__).resolve().parents[2]
PREVIOUS_REVISION='098f5b68197253b71e25eb37b5f45d2d7d3c51f2' # Published source of the Rev33 guides.
archive=ROOT/'supporto/SUPERATI/fondazioni-superficiali-rev34'
entries=[]
for kind in ['pratica','teorica']:
    for suffix in ['docx','pdf']:
        relative=Path(f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev33.{suffix}')
        source=(ROOT/relative).resolve();target=(archive/relative).resolve()
        assert source.is_relative_to(ROOT.resolve()) and target.is_relative_to(archive.resolve())
        if source.exists():
            assert not target.exists();target.parent.mkdir(parents=True,exist_ok=True);source.rename(target)
        entries.append({'origin':relative.as_posix(),'sha256':sha256(target.read_bytes()).hexdigest(),'replacement':relative.as_posix().replace('Rev33','Rev34')})
    for suffix in ['md','pdf']:
        relative=Path(f'supporto/docs/guida-{kind}-anthea.{suffix}')
        target=(archive/relative).resolve();assert target.is_relative_to(archive.resolve())
        previous=subprocess.check_output(['git','show',PREVIOUS_REVISION+':'+relative.as_posix()],cwd=ROOT)
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():assert target.read_bytes()==previous
        else:target.write_bytes(previous)
        entries.append({'origin':relative.as_posix(),'sha256':sha256(previous).hexdigest(),'replacement':relative.as_posix()+' (revisione 34)'})
(archive/'archivio.json').write_text(json.dumps({'date':'2026-10-12','reason':'Edizione superata dalla revisione 34 con materiali comuni e viste della fondazione','files':entries},indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print('Archiviate le revisioni superate; struttura relativa e impronte conservate.')
