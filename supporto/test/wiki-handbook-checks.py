"""Run structural negative cases and independent numeric checks; no calculation engine calls."""
from pathlib import Path
import sys,json,copy,math,unittest
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'supporto/scripts/wiki'))
from validate_wiki import validate
W=ROOT/'X.Desktop/Wiki'
def read(n):return json.loads((W/n).read_text(encoding='utf-8'))
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
        for invalid in ['[link](wiki:missing)','[link](wiki:beam#missing)','![](../../X.Desktop/Assets/Wiki/footing.svg)','![figure](missing.svg)']:
            with self.subTest(content=invalid):
                self.bodies[key]=original+'\n'+invalid
                with self.assertRaises(ValueError):self.run_validation()
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
