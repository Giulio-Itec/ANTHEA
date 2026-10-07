"""Run structural negative cases and independent numeric checks; no calculation engine calls."""
from pathlib import Path
import sys,json,copy,math,unittest
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'supporto/scripts/wiki'))
from validate_wiki import validate
import re
W=ROOT/'X.Desktop/Wiki'
GUIDES=[ROOT/'supporto/docs/guida-pratica-anthea.md',ROOT/'supporto/docs/guida-teorica-anthea.md']
def read(n):return json.loads((W/n).read_text(encoding='utf-8'))
# W0.4 (7/10/2026): no development diary, AI tools, competing programs or sites. Normative citations
# (NTC, Circolare, Eurocodici, UNI, CNR, fib) and the bibliography of the implemented methods stay.
# Example models still kept in supporto/artefatti until W0.2 moves them into versioned examples.
ARTIFACT_EXAMPLES=('globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea','muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea','muri-materiali-distinta-20261005/interfaccia/distinta-due-zone.anthea')
FORBIDDEN_EVERYWHERE=[  # guides, Wiki metadata, review registry and Wiki code
    (r'(?i)\b(?:chatgpt|codex|openai|copilot|gemini|claude)\b','strumento di IA'),
    (r'(?i)\b(?:sofistik|madosoft|sim-cad|thebridgeeng|comsol|opensees|grasshopper|mcneel|geostru|midas|straus7|sap2000|etabs|csibridge|plaxis)\b','programma concorrente'),
    (r'\bSCIA\b|\bMAX 16\b','programma concorrente'),
    (r'(?i)thebridgeeng|\bwww\.','sito'),
]
FORBIDDEN_IN_GUIDES=[
    (r'\b[\w-]+\.(?:com|net|org|io)\b','sito'),
    (r'\bRev\.?\s?\d{2}\b|archivio Rev','revisione intermedia'),
    (r"(?i)resoconti (?:originali )?di (?:audit e )?sviluppo|appendici di sviluppo|in questa attività|questa revisione documentale|documentazione precedente|(?:controlli|test) dell['’]aggiornamento|futura integrazione|nuova verifica indipendente",'diario di sviluppo'),
    (r"(?i)\b(?:abbiamo|ho) (?:aggiunto|corretto|modificato|eliminato|introdotto)\b|\b(?:è stat[oa]|sono stat[ie]) (?:aggiunt|corrett|eliminat|introdott|rimoss)",'diario di sviluppo'),
    (r"(?i)preferenza (?:esplicita )?dell['’]utente|(?:richiest|autorizzat|allegat)[aoie] dall['’]utente|per scelta dell['’]utente|scansione locale|lett[ei] (?:anche )?visivamente",'diario di sviluppo'),
    (r'supporto/(?:test|scripts|SUPERATI)/|CONTROLLO\.md|\b\w+\.(?:cs|csproj)\b','percorso di sviluppo'),
    (r'supporto/artefatti/(?!'+'|'.join(map(re.escape,ARTIFACT_EXAMPLES))+')','percorso di sviluppo'),
]
def diary_violations(text,guide=True):
    """Forbidden expressions as (category, match); guide=False applies only the checks valid for metadata and code."""
    rules=FORBIDDEN_EVERYWHERE+(FORBIDDEN_IN_GUIDES if guide else [])
    return [(label,m.group(0)) for pattern,label in rules for m in re.finditer(pattern,text)]
def prose(text):
    """Paragraphs over 80 characters, excluding headings, tables, figures, formulas and link lists."""
    return {l.strip() for l in text.splitlines() if len(l.strip())>80 and not l.lstrip().startswith(('#','|','!','```','$$','- ','>','<!--'))}
class HandbookChecks(unittest.TestCase):
    def setUp(self):
        self.articles=read('index.json');self.chapters=read('chapters.json');self.references=read('references.json');self.glossary=read('glossary.json')
        self.aliases=read('aliases.json')
        self.bodies={}
        for a in self.articles:
            data=(ROOT/f'supporto/docs/guida-{a["source"].split(".")[0]}-anthea.md').read_bytes()
            self.bodies[a['id']]=data[a['offset']:a['offset']+a['length']].decode('utf-8')
    def run_validation(self):return validate(self.articles,self.chapters,self.references,self.glossary,self.bodies,ROOT,self.aliases)
    def test_alias_integrity(self):
        original=copy.deepcopy(self.aliases)
        for key,value in [('obsolete','missing'),('obsolete','beam#missing'),('loop','loop')]:
            self.aliases=dict(original);self.aliases[key]=value
            with self.assertRaises(ValueError):self.run_validation()
    def test_migration_coverage(self):
        archive=ROOT/'supporto/SUPERATI/wiki-integrazione-rev15-20261004'
        old=json.loads((archive/'X.Desktop/Wiki/index.json').read_text(encoding='utf-8'))
        current={a['id'] for a in self.articles}|{a['key'] for a in self.articles}
        for a in old:
            for path in [a['id'],a['key']]:
                self.assertTrue(path in current or path in self.aliases,path)
                for anchor in a['sections']:self.assertIn(path+'#'+anchor,self.aliases if path+'#'+anchor in self.aliases else {a['id']+'#'+x for x in next((n for n in self.articles if n['id']==a['id']),{}).get('sections',[])})
        self.assertFalse(any(a['status']=='historical' for a in self.articles))
    def test_catalog(self):self.assertEqual(self.run_validation()['chapters'],12)
    def test_review_scopes(self):
        reviews=json.loads((ROOT/'supporto/docs/wiki-riscontri.json').read_text(encoding='utf-8'))
        by_key={a['key']:a for a in self.articles}
        for key,review in reviews.items():
            self.assertEqual(by_key[key]['status'],review['status'])
            self.assertTrue(review['scope'] and review['evidence'])
            if review['openPoints']:self.assertNotEqual(review['status'],'reviewed')
    def test_invalid_metadata_rejected(self):
        original=copy.deepcopy(self.articles)
        for field,value in [('key',self.articles[1]['key']),('id',self.articles[1]['id']),('chapterId','missing'),('area',''),('title',''),('summary',''),('related',['missing']),('prerequisites',['missing']),('references',['missing'])]:
            with self.subTest(field=field):
                self.articles=copy.deepcopy(original);self.articles[0][field]=value
                with self.assertRaises(ValueError):self.run_validation()
    def test_invalid_content_rejected(self):
        key=self.articles[0]['id'];original=self.bodies[key]
        for invalid in ['[link](wiki:missing)','[link](wiki:beam#missing)','![](../../X.Desktop/Assets/Wiki/footing.svg)','![figure](missing.svg)','[sito](https://example.com/articolo)','Fonte: http://example.com']:
            with self.subTest(content=invalid):
                self.bodies[key]=original+'\n'+invalid
                with self.assertRaises(ValueError):self.run_validation()
    def test_reference_links_rejected(self):
        self.references=copy.deepcopy(self.references);self.references[0]['url']='https://example.com'
        with self.assertRaises(ValueError):self.run_validation()
    def test_only_own_content(self):
        # Decision of 6/10/2026: guides and Wiki keep only content written for ANTHEA; norms and bibliography stay as citations.
        terms=['Approfondimento ·','Letture tecniche','De Pisapia','CC BY-NC','marcodepisapia','geostru','simonecaffe','amazon.','Madosoft','MAX 16','TheBridgeEng']
        docs=[ROOT/'supporto/docs/guida-pratica-anthea.md',ROOT/'supporto/docs/guida-teorica-anthea.md',ROOT/'supporto/docs/wiki-riscontri.json',*sorted(W.glob('*.json'))]
        for path in docs+sorted((ROOT/'X.Desktop/Wpf').glob('Wiki*.cs')):
            text=path.read_text(encoding='utf-8').lower()
            for term in terms:self.assertNotIn(term.lower(),text,f'{path.name}: {term}')
        for path in docs:self.assertNotRegex(path.read_text(encoding='utf-8'),r'https?://',path.name)
        self.assertFalse([r for r in self.references if 'url' in r])
        keys={a['key'] for a in self.articles}|{a['id'] for a in self.articles}
        self.assertFalse([k for k in keys if k.rsplit('/',1)[-1].startswith(('mdp-','letture-'))])
        self.assertFalse([k for k,v in self.aliases.items() if v.split('#')[0].rsplit('/',1)[-1].startswith(('mdp-','letture-'))])
        hub=next(a for a in self.articles if a['key']=='biblioteca-tecnica')
        self.assertEqual(len(hub['related']),13)
        for key in hub['related']:self.assertIn(key,{a['key'] for a in self.articles})
    def test_no_development_diary(self):
        # W0.4: the guides and the Wiki texts derived from them keep no development diary, AI tools, competitors or sites.
        for path in GUIDES:self.assertEqual(diary_violations(path.read_text(encoding='utf-8')),[],path.name)
        for name in ['editorial.json','chapters.json','glossary.json','references.json']:
            self.assertEqual(diary_violations((W/name).read_text(encoding='utf-8')),[],name)
        for path in [ROOT/'supporto/docs/wiki-riscontri.json',*sorted(W.glob('*.json')),*sorted((ROOT/'X.Desktop/Wpf').glob('Wiki*.cs'))]:
            self.assertEqual(diary_violations(path.read_text(encoding='utf-8'),guide=False),[],path.name)
    def test_diary_lint_rejects_and_accepts(self):
        for text in ['Il collegamento condiviso ChatGPT non era recuperabile.','Generato con Codex.','Confronto con SOFiSTiK.','Fonte: thebridgeeng.com/design',
                     'Evidenze in supporto/artefatti/palo-armature.','I test sono in supporto/test/ElasticPile.Checks.','Vedi BarScheduleChecks.cs.',
                     "Le fonti restano nell'archivio Rev14.",'Valore iniziale per preferenza dell’utente.','Il comando è stato eliminato.']:
            with self.subTest(text=text):self.assertTrue(diary_violations(text),text)
        allowed=['NTC 2018 §7.2.5, G.U. 20 febbraio 2018, S.O. n. 8; Circolare 21 gennaio 2019 n. 7, C4.1.10.',
                 'EN 1998-5:2004 allegato F (F.7); EN 1992-1-1 §7.3.4(3); UNI 11104:2016; CNR-DT 200; fib Model Code 2010, Tab. 7.6-2.',
                 'Viggiani, Fondazioni; Bustamante e Doix; Reese e Matlock (1956); Broms; ANAS, Elenco prezzi 2026 Rev 1.',
                 'Fino al 6/10/2026 ANTHEA applicava γRD anche a F.','Aprire supporto/artefatti/'+ARTIFACT_EXAMPLES[0]+'.',
                 'Quote e numeri imposti dall’utente vengono rispettati; Rev. 0 diventa Rev. 1.']
        for text in allowed:
            with self.subTest(text=text):self.assertEqual(diary_violations(text),[])
    def test_no_paragraphs_duplicated_between_guides(self):
        practical,theory=(prose(p.read_text(encoding='utf-8')) for p in GUIDES)
        self.assertEqual(sorted(practical&theory),[])
        sample=next(iter(theory));self.assertTrue(prose(sample)&theory)
    def test_independent_examples(self):
        e,i,l,a=210000,8e6,4000,4000
        n=math.pi**2*e*i/l**2
        self.assertAlmostEqual(n/1000,1036.308462,places=5)
        self.assertAlmostEqual(n/a,259.0771155,places=5)
        self.assertAlmostEqual(math.pi**2*e*i/(2*l)**2,n/4)
        delta=max((200-.4*2.9/.02*(1+200000/33000*.02))/200000,.6*200/200000)
        spacing=3.4*30+.425*.8*.5*16/.02
        self.assertAlmostEqual(spacing*delta,.16061394,places=7)
        self.assertLess(150,5*(30+16/2))
        self.assertAlmostEqual((2+math.pi)*50+18,275.0796327,places=6)
        self.assertEqual(25*8**2/8,200)
        self.assertAlmostEqual(25*8.8**2/8,242)
        self.assertAlmostEqual(450/1.15,391.304347826,places=7)
        self.assertAlmostEqual(450/1.15/200000*1000,1.956521739,places=8)
        self.assertAlmostEqual(math.pi*1*10*50,1570.796326795,places=7)
        self.assertAlmostEqual(math.pi*.2*1.3*8*150,980.17690792,places=7)
        self.assertAlmostEqual(1.1/1.075737,1.022555,places=6)
        self.assertEqual(-100*20**2/8,-5000)
        self.assertEqual(9*100*20**2/128,2812.5)
        self.assertAlmostEqual(1.12*1.15,1.288)
        self.assertEqual(12*2.5,30)  # kNm/m times strip width in m
        self.assertAlmostEqual(2*math.pi*math.sqrt(1000/40000),.99345882658,places=10)
        self.assertAlmostEqual(2*math.pi*math.sqrt(2000/40000),1.4049629462,places=10)
        self.assertEqual(1855-25-30,1800)
        self.assertEqual(1850-25-25,1800)
if __name__=='__main__':unittest.main(verbosity=2)
