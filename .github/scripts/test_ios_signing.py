import copy
import datetime
import unittest
from ios_signing import validate_profile


class SigningConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.profile = {
            "UUID": "12345678-1234-1234-1234-123456789abc",
            "TeamIdentifier": ["ABCDE12345"],
            "ExpirationDate": datetime.datetime.now() + datetime.timedelta(days=1),
            "Entitlements": {"application-identifier": "ABCDE12345.com.xwyin.homecoming", "get-task-allow": False},
            "ProvisionedDevices": ["test-device"],
        }

    def validate(self, profile=None, method="ad-hoc", bundle="com.xwyin.homecoming"):
        return validate_profile(profile or self.profile, bundle, "ABCDE12345", method)

    def test_valid_ad_hoc_and_wildcard(self):
        self.assertEqual(self.validate(), self.profile["UUID"].upper())
        self.profile["Entitlements"]["application-identifier"] = "ABCDE12345.com.xwyin.*"
        self.validate()

    def test_reject_wrong_team_bundle_expiry_and_uuid(self):
        for key, value in (("TeamIdentifier", ["OTHER12345"]),
                           ("ExpirationDate", datetime.datetime.now() - datetime.timedelta(days=1)),
                           ("UUID", "not-a-uuid")):
            profile = copy.deepcopy(self.profile); profile[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError): self.validate(profile)
        with self.assertRaises(ValueError): self.validate(bundle="com.other.app")
        with self.assertRaises(ValueError): self.validate(bundle="com.app;echo")

    def test_profile_must_match_export_method(self):
        with self.assertRaises(ValueError): self.validate(method="app-store")
        with self.assertRaises(ValueError): self.validate(method="development")
        self.profile["Entitlements"]["get-task-allow"] = True
        self.validate(method="development")
        with self.assertRaises(ValueError): self.validate()
        del self.profile["ProvisionedDevices"]
        self.profile["Entitlements"]["get-task-allow"] = False
        self.validate(method="app-store")
        with self.assertRaises(ValueError): self.validate()


if __name__ == "__main__":
    unittest.main()
