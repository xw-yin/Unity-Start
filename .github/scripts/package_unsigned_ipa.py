"""Package an unsigned iPhoneOS application for later signing."""
import os
from pathlib import Path
import plistlib
import stat
import sys
import zipfile

def package(app, output):
    app, output = Path(app), Path(output)
    if app.suffix != '.app' or not app.is_dir():
        raise ValueError(f'Device .app product missing: {app}')
    with (app / 'Info.plist').open('rb') as source:
        info = plistlib.load(source)
    executable = info.get('CFBundleExecutable', '')
    if not executable or Path(executable).name != executable or not (app / executable).is_file():
        raise ValueError('Application executable is missing or invalid.')
    if 'iPhoneOS' not in info.get('CFBundleSupportedPlatforms', []):
        raise ValueError('Expected an iPhoneOS device product, not a simulator build.')
    if (app / 'embedded.mobileprovision').exists() or (app / '_CodeSignature').exists():
        raise ValueError('Expected an unsigned product without provisioning or a signature.')
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
        for root, directories, files in os.walk(app, followlinks=False):
            for directory in list(directories):
                if (Path(root) / directory).is_symlink():
                    files.append(directory); directories.remove(directory)
            for name in files:
                path = Path(root) / name
                member = 'Payload/' + app.name + '/' + path.relative_to(app).as_posix()
                if path.is_symlink():
                    entry = zipfile.ZipInfo(member); entry.create_system = 3
                    entry.external_attr = (stat.S_IFLNK | 0o777) << 16
                    archive.writestr(entry, os.readlink(path))
                else:
                    archive.write(path, member)
    print(f'Unsigned IPA created: {output}. Sign before installing.')

if __name__ == '__main__':
    try:
        if len(sys.argv) != 3: raise ValueError('Usage: package_unsigned_ipa.py <device.app> <output.ipa>')
        package(sys.argv[1], sys.argv[2])
    except (OSError, ValueError, plistlib.InvalidFileException) as error:
        print(f'::error::{error}', file=sys.stderr); sys.exit(1)
