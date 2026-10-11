"""Archive Rev32 at its relative path; preserve previous Markdown/PDF sources from Git."""
from pathlib import Path
import subprocess,json
from hashlib import sha256
ROOT=Path(__file__).resolve().parents[2]
archive=ROOT/'supporto/SUPERATI/fondazioni-superficiali-rev33'
entries=[]
for kind in ['pratica','teorica']:
    for suffix in ['docx','pdf']:
        relative=Path(f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev32.{suffix}')
        source=(ROOT/relative).resolve();target=(archive/relative).resolve()
        assert source.is_relative_to(ROOT.resolve()) and target.is_relative_to(archive.resolve())
        if source.exists():
            assert not target.exists();target.parent.mkdir(parents=True,exist_ok=True);source.rename(target)
        entries.append({'origin':relative.as_posix(),'sha256':sha256(target.read_bytes()).hexdigest(),'replacement':relative.as_posix().replace('Rev32','Rev33')})
    for suffix in ['md','pdf']:
        relative=Path(f'supporto/docs/guida-{kind}-anthea.{suffix}')
        target=(archive/relative).resolve();assert target.is_relative_to(archive.resolve())
        previous=subprocess.check_output(['git','show','HEAD:'+relative.as_posix()],cwd=ROOT)
        target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists():assert target.read_bytes()==previous
        else:target.write_bytes(previous)
        entries.append({'origin':relative.as_posix(),'sha256':sha256(previous).hexdigest(),'replacement':relative.as_posix()+' (revisione 33)'})
(archive/'archivio.json').write_text(json.dumps({'date':'2026-10-12','reason':'Edizione superata dalla revisione 33 con Fondazioni superficiali','files':entries},indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print('Archiviate le revisioni superate; struttura relativa e impronte conservate.')
