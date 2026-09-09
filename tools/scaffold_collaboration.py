"""Create the initial module boundaries; never overwrite existing project files."""
from pathlib import Path
import json
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'Assets/CursorHunter'
MODULES = {
    'Contracts': [],
    'Data': ['Contracts'],
    'Progression': ['Contracts', 'Data'],
    'Combat': ['Contracts'],
    'App': ['Contracts', 'Data', 'Progression', 'Combat'],
}

def write_new(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        path.write_text(text, encoding='utf-8')

def main():
    for module, refs in MODULES.items():
        owner = '친구' if module == 'Combat' else '사용자 (Contracts는 공동 검토)'
        folders = ['Runtime']
        if module != 'Contracts':
            folders += ['Tests']
        if module in ['Progression', 'Combat', 'App']:
            folders += ['Scenes', 'Prefabs', 'Art']
        if module == 'Data':
            folders += ['Definitions/' + item for item in ['Traits','Monsters','Bosses','Skills','Economy']]
        for folder in folders:
            write_new(BASE / module / folder / 'README.md', f'# {module}/{folder}\n담당: {owner}.\n용도와 연결 규칙: 프로젝트 루트 docs/collaboration/architecture.md 및 contracts.md.\n씬·프리팹·데이터 인스턴스는 담당자가 Unity 6000.3.13f1에서 생성한다.\n')
        data = dict(name='CursorHunter.'+module, rootNamespace='CursorHunter.'+module,
                    references=['CursorHunter.'+r for r in refs], autoReferenced=False,
                    noEngineReferences=module == 'Contracts')
        write_new(BASE/module/'Runtime'/('CursorHunter.'+module+'.asmdef'), json.dumps(data, indent=2)+'\n')
        write_new(BASE/module/'AGENTS.md', f'# {module} 작업 지침\n담당: {owner}.\n프로젝트 루트 docs/collaboration/architecture.md와 contracts.md를 읽고 소유권과 참조 방향을 유지한다.\n공급사 원본 대신 이 모듈의 프리팹/아트를 편집한다. 기존 클래스 존재를 확인한 뒤 구현한다.\n')
    for path in [BASE] + sorted(BASE.rglob('*')):
        if path.suffix == '.meta':
            continue
        meta = Path(str(path)+'.meta')
        if meta.exists():
            continue
        text = 'fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'
        if path.is_dir():
            text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        else:
            importer = 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'TextScriptImporter'
            text += importer+':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        write_new(meta, text)

if __name__ == '__main__':
    main()
