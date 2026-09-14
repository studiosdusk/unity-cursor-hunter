"""Read-only validation of module boundaries, Unity metadata and asset references."""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]

def main():
    errors=[]
    def require(ok, message):
        if not ok: errors.append(message)
    version=(ROOT/'ProjectSettings/ProjectVersion.txt').read_text()
    require('m_EditorVersion: 6000.3.13f1\n' in version, 'Unity version mismatch')
    require('m_SerializationMode: 2' in (ROOT/'ProjectSettings/EditorSettings.asset').read_text(), 'Force Text required')
    require('Visible Meta Files' in (ROOT/'ProjectSettings/VersionControlSettings.asset').read_text(), 'Visible Meta Files required')
    base=ROOT/'Assets/CursorHunter'
    expected={'Contracts':[], 'Data':['Contracts'], 'Progression':['Contracts','Data'], 'Combat':['Contracts'], 'App':['Contracts','Data','Progression','Combat']}
    for module, refs in expected.items():
        path=base/module/'Runtime'/f'CursorHunter.{module}.asmdef'
        data=json.loads(path.read_text())
        project_refs=[ref for ref in data['references'] if ref.startswith('CursorHunter.')]
        require(project_refs==['CursorHunter.'+r for r in refs], f'Forbidden reference in {module}')
        require(data['name']=='CursorHunter.'+module, f'Assembly name mismatch: {module}')
        if module=='Contracts': require(data.get('noEngineReferences'), 'Contracts must be engine independent')
    all_guids={}
    for meta in (ROOT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid: ([0-9a-f]{32})$',meta.read_text(errors='replace'),re.M)
        if match: all_guids.setdefault(match[1],[]).append(meta)
    for path in [base]+list(base.rglob('*')):
        if path.suffix=='.meta':
            require(Path(str(path)[:-5]).exists(), f'Orphan meta: {path}')
            continue
        meta=Path(str(path)+'.meta')
        require(meta.exists(),f'Missing meta: {path}')
        if meta.exists():
            match=re.search(r'^guid: ([0-9a-f]{32})$',meta.read_text(),re.M)
            require(bool(match),f'Invalid GUID: {meta}')
            if match: require(len(all_guids[match[1]])==1,f'Duplicate new GUID: {meta}')
    rows=json.loads((ROOT/'docs/collaboration/asset-map.json').read_text())
    require(len({r['design_id'] for r in rows})==len(rows),'Duplicate design mapping ID')
    for row in rows:
        path=ROOT/row['path']
        require(path.exists(),f'Missing asset: {path}')
        require(all_guids.get(row['guid'])==[Path(str(path)+'.meta')],f'GUID does not resolve uniquely: {path}')
    for role in ['combat','progression']:
        p=ROOT/f'docs/collaboration/skills/cursor-hunter-{role}/SKILL.md'
        require(p.exists(),f'Missing role skill: {role}')
        if p.exists():
            text=p.read_text()
            require(text.startswith('---\nname: cursor-hunter-'+role+'\ndescription: '),f'Invalid skill frontmatter: {role}')
            require(text.count('\n---\n')==1,f'Invalid skill frontmatter boundary: {role}')
            installed=ROOT/f'.agents/skills/cursor-hunter-{role}/SKILL.md'
            require(installed.exists() and installed.read_text()==text,f'Skill copy differs: {role}')
    if errors: raise SystemExit('\n'.join(errors))
    print(f'PASS: Unity pin, 5 assembly boundaries, new .meta GUIDs, {len(rows)} asset mappings, 2 role skills')
    print('Unity import/compile/Play Mode are not covered by this check.')

if __name__=='__main__': main()
