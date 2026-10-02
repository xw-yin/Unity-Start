"""Install ephemeral Apple signing assets; never print credential contents."""
import base64
import datetime
import fnmatch
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import sys
import uuid


def run(*args):
    result = subprocess.run(args, capture_output=True)
    if result.returncode:
        raise ValueError(f"{args[0]} {args[1]} failed; check the certificate/password and signing configuration.")
    return result.stdout


def validate_profile(profile, bundle, team, distribution):
    if not re.fullmatch(r"[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+", bundle):
        raise ValueError("IOS_BUNDLE_ID must be a valid bundle identifier.")
    if not re.fullmatch(r"[A-Z0-9]{10}", team):
        raise ValueError("IOS_TEAM_ID must be the 10-character Apple Team ID.")
    if team not in profile.get("TeamIdentifier", []):
        raise ValueError("Provisioning profile belongs to a different Apple Team.")
    identifier = profile.get("Entitlements", {}).get("application-identifier", "")
    pattern = identifier.partition(".")[2]
    if not pattern or not fnmatch.fnmatchcase(bundle, pattern):
        raise ValueError("Provisioning profile does not match IOS_BUNDLE_ID.")
    expires = profile.get("ExpirationDate")
    if not expires or expires.replace(tzinfo=datetime.timezone.utc) <= datetime.datetime.now(datetime.timezone.utc):
        raise ValueError("Provisioning profile is expired or lacks an expiration date.")
    debug = profile.get("Entitlements", {}).get("get-task-allow", False)
    devices = bool(profile.get("ProvisionedDevices"))
    if profile.get("ProvisionsAllDevices"):
        raise ValueError("Enterprise profiles are not supported by this workflow.")
    if distribution == "development" and not (debug and devices):
        raise ValueError("Choose a development profile for development export.")
    if distribution == "ad-hoc" and not (devices and not debug):
        raise ValueError("Ad Hoc requires a distribution profile containing registered device UDIDs.")
    if distribution == "app-store" and (devices or debug):
        raise ValueError("App Store export requires an App Store distribution profile.")
    if distribution not in ("development", "ad-hoc", "app-store"):
        raise ValueError("Unsupported distribution method.")
    try:
        return str(uuid.UUID(profile["UUID"])).upper()
    except (KeyError, ValueError, TypeError):
        raise ValueError("Provisioning profile has an invalid UUID.") from None


def install(root):
    root.mkdir(parents=True, exist_ok=True)
    os.chmod(root, 0o700)
    bundle, team, distribution = [os.environ[k] for k in ("IOS_BUNDLE_ID", "IOS_TEAM_ID", "IOS_DISTRIBUTION")]
    p12, profile_path = root / "certificate.p12", root / "profile.mobileprovision"
    for target, key in ((p12, "IOS_CERTIFICATE_P12_BASE64"), (profile_path, "IOS_PROVISIONING_PROFILE_BASE64")):
        try:
            target.write_bytes(base64.b64decode("".join(os.environ[key].split()), validate=True))
            os.chmod(target, 0o600)
        except (ValueError, KeyError):
            raise ValueError(f"{key} is missing or is not valid base64.") from None
    profile = plistlib.loads(run("security", "cms", "-D", "-i", str(profile_path)))
    profile_uuid = validate_profile(profile, bundle, team, distribution)
    keychain = root / "build.keychain-db"
    original = re.findall(r'"([^\"]+)"', run("security", "list-keychains", "-d", "user").decode())
    destinations = [Path.home() / folder / f"{profile_uuid}.mobileprovision" for folder in (
        "Library/MobileDevice/Provisioning Profiles", "Library/Developer/Xcode/UserData/Provisioning Profiles")]
    # Record paths before mutation so the always() cleanup step can recover from partial failure.
    state = {"keychain": str(keychain), "keychains": original, "profiles": []}
    (root / "state.json").write_text(json.dumps(state))
    password = uuid.uuid4().hex
    run("security", "create-keychain", "-p", password, str(keychain))
    run("security", "set-keychain-settings", "-lut", "7200", str(keychain))
    run("security", "unlock-keychain", "-p", password, str(keychain))
    run("security", "import", str(p12), "-P", os.environ.get("IOS_CERTIFICATE_PASSWORD", ""), "-k", str(keychain), "-T", "/usr/bin/codesign", "-T", "/usr/bin/security")
    run("security", "set-key-partition-list", "-S", "apple-tool:,apple:,codesign:", "-s", "-k", password, str(keychain))
    run("security", "list-keychains", "-d", "user", "-s", str(keychain), *original)
    identities = run("security", "find-identity", "-v", "-p", "codesigning", str(keychain)).decode()
    category = r"(?:Apple Development|iPhone Developer)" if distribution == "development" else r"(?:Apple Distribution|iPhone Distribution)"
    matches = re.findall(r'\b([A-F0-9]{40})\s+"' + category + r'[^\"]*"', identities)
    if len(matches) != 1:
        raise ValueError("P12 must contain exactly one valid signing identity of the selected type, with its private key.")
    # Verify this certificate is permitted by the supplied provisioning profile.
    import hashlib
    allowed = {hashlib.sha1(cert).hexdigest().upper() for cert in profile.get("DeveloperCertificates", [])}
    if matches[0] not in allowed:
        raise ValueError("The P12 certificate is not included in this provisioning profile.")
    for destination in destinations:
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists():
            raise ValueError("A matching profile already exists on the runner; use a clean hosted runner.")
        state["profiles"].append(str(destination))
        (root / "state.json").write_text(json.dumps(state))
        shutil.copyfile(profile_path, destination)
    methods = {"ad-hoc": "release-testing", "app-store": "app-store-connect", "development": "debugging"}
    options = {"method": methods[distribution], "teamID": team, "signingStyle": "manual",
               "signingCertificate": matches[0], "provisioningProfiles": {bundle: profile_uuid},
               "destination": "export", "manageAppVersionAndBuildNumber": False}
    (root / "ExportOptions.plist").write_bytes(plistlib.dumps(options))
    with open(os.environ["GITHUB_OUTPUT"], "a") as output:
        output.write(f"profile_uuid={profile_uuid}\nidentity_sha={matches[0]}\n")
    print("Signing assets validated and installed in an ephemeral keychain.")


def cleanup(root):
    state_path = root / "state.json"
    if state_path.exists():
        state = json.loads(state_path.read_text())
        if state["keychains"]:
            run("security", "list-keychains", "-d", "user", "-s", *state["keychains"])
        keychain = Path(state["keychain"])
        if keychain.exists():
            run("security", "delete-keychain", str(keychain))
        for path in state["profiles"]:
            Path(path).unlink(missing_ok=True)
    if root.exists():
        shutil.rmtree(root)


if __name__ == "__main__":
    try:
        signing_root = Path(os.environ["RUNNER_TEMP"]) / "ios-signing"
        cleanup(signing_root) if "--cleanup" in sys.argv else install(signing_root)
    except (ValueError, KeyError, OSError, plistlib.InvalidFileException) as error:
        print(f"::error::{error}", file=sys.stderr)
        sys.exit(1)
