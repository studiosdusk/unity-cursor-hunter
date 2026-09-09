"""macOS: launch this project only with the pinned Unity editor."""
from pathlib import Path
import argparse
import plistlib
import subprocess

ROOT=Path(__file__).resolve().parents[1]
VERSION='6000.3.13f1'

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--editor',type=Path,default=Path('/Applications/Unity/Hub/Editor')/VERSION/'Unity.app')
    parser.add_argument('--check',action='store_true',help='Only validate the installed editor')
    args=parser.parse_args()
    project_version=(ROOT/'ProjectSettings/ProjectVersion.txt').read_text()
    if f'm_EditorVersion: {VERSION}\n' not in project_version:
        raise SystemExit('Project version changed; review team version first.')
    info=args.editor/'Contents/Info.plist'
    binary=args.editor/'Contents/MacOS/Unity'
    if not info.exists() or not binary.exists():
        raise SystemExit(f'Install Unity {VERSION} first, or pass --editor /path/to/Unity.app')
    with info.open('rb') as stream: version=plistlib.load(stream).get('CFBundleVersion','')
    if version != VERSION:
        raise SystemExit(f'Editor mismatch: {version!r}; expected {VERSION}')
    print(f'Editor verified: {version} at {args.editor}')
    if not args.check:
        raise SystemExit(subprocess.call([str(binary),'-projectPath',str(ROOT)]))

if __name__=='__main__': main()
