import plistlib
from pathlib import Path
import tempfile
import unittest
import zipfile
from package_unsigned_ipa import package

class UnsignedIPATests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); self.app = self.root / 'Homecoming.app'; self.app.mkdir()
        self.info = {'CFBundleExecutable': 'Homecoming', 'CFBundleSupportedPlatforms': ['iPhoneOS']}
        (self.app / 'Homecoming').write_bytes(b'device-executable'); self.save_info()
    def save_info(self):
        (self.app / 'Info.plist').write_bytes(plistlib.dumps(self.info))
    def test_payload_contains_app_and_framework(self):
        framework = self.app / 'Frameworks' / 'UnityFramework.framework'; framework.mkdir(parents=True)
        (framework / 'UnityFramework').write_bytes(b'framework')
        output = self.root / 'unsigned.ipa'; package(self.app, output)
        with zipfile.ZipFile(output) as archive:
            self.assertIsNone(archive.testzip())
            self.assertIn('Payload/Homecoming.app/Homecoming', archive.namelist())
            self.assertIn('Payload/Homecoming.app/Frameworks/UnityFramework.framework/UnityFramework', archive.namelist())
    def test_reject_simulator_missing_executable_and_signed_product(self):
        self.info['CFBundleSupportedPlatforms'] = ['iPhoneSimulator']; self.save_info()
        with self.assertRaises(ValueError): package(self.app, self.root / 'invalid.ipa')
        self.info['CFBundleSupportedPlatforms'] = ['iPhoneOS']; self.save_info()
        (self.app / 'Homecoming').unlink()
        with self.assertRaises(ValueError): package(self.app, self.root / 'invalid.ipa')
        (self.app / 'Homecoming').write_bytes(b'app'); (self.app / 'embedded.mobileprovision').write_bytes(b'profile')
        with self.assertRaises(ValueError): package(self.app, self.root / 'invalid.ipa')

if __name__ == '__main__': unittest.main()
