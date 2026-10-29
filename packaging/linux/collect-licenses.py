"""Copy dependency notices from the exact packages selected by NuGet."""
import json
import pathlib
import shutil
import sys
import xml.etree.ElementTree as ET

assets = json.loads(pathlib.Path(sys.argv[1]).read_text())
output = pathlib.Path(sys.argv[2])
output.mkdir(parents=True, exist_ok=True)
manifest = []
for identity, entry in assets['libraries'].items():
    if entry['type'] != 'package':
        continue
    package = next((pathlib.Path(root) / entry['path'] for root in assets['packageFolders']
                    if (pathlib.Path(root) / entry['path']).is_dir()), None)
    if package is None:
        raise RuntimeError('NuGet package unavailable: ' + identity)
    target = output / identity.replace('/', '-')
    target.mkdir(exist_ok=True)
    for file in package.rglob('*'):
        if file.is_file() and (file.name.lower().startswith(('license', 'licence', 'third-party', 'notice')) or file.suffix == '.nuspec'):
            relative = file.relative_to(package)
            (target / relative).parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(file, target / relative)
    nuspec = next(package.glob('*.nuspec'))
    metadata = ET.parse(nuspec).getroot()
    fields = {node.tag.rsplit('}', 1)[-1]: node.text or '' for node in metadata.iter()}
    manifest.append(f"{identity}\n{fields.get('copyright', fields.get('authors', ''))}\nLicense: {fields.get('license', fields.get('licenseUrl', 'see package metadata'))}\n")
(output / 'PACKAGES.txt').write_text('\n'.join(manifest))
