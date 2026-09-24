import json
D=json.load(open('tmp/validazione_ca/actual.json',encoding='utf-8'))
for v in D:
 print(v['id'], ('ERROR '+v['error'][:180]) if 'error' in v else ({k:v[k] for k in ['wk','actual','crack'] if k in v} if 'boundary' not in v else {k:v['boundary'][k] for k in ['Utilization','Resistance','Response']}))
