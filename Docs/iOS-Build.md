# GitHub Actions 打包 iOS

仓库 Actions → **Build iOS IPA** → **Run workflow**。选择 `ad-hoc`、`app-store` 或 `development`。成功后从该运行的 Artifacts 下载 `Homecoming-iOS-…`，解压即可得到签名的 `.ipa`。

流程使用 `macos-15`、项目记录的 Unity 6000.6.3f1 和该运行器的 Xcode，只导出 Homecoming 场景。工作流为手动触发，避免每次提交都启动收费的 macOS 构建。第一次会下载 Unity 和 iOS 模块，耗时较长。仓库使用 Git LFS，检出时会下载模型与纹理。

## 一次性配置

在 Settings → Secrets and variables → Actions → Secrets 添加以下 Repository secrets；不要把密码或证书提交到 Git，也不要贴到聊天里。

| Secret | 内容 |
|---|---|
| `UNITY_EMAIL` | 用于 CI 激活的 Unity 账户邮箱 |
| `UNITY_PASSWORD` | 对应账户密码 |
| `UNITY_LICENSE` 或 `UNITY_SERIAL` | GameCI 支持的有效 Unity 授权文件全文，或可用于该构建机激活的授权序列号 |
| `IOS_TEAM_ID` | Apple 开发者账户的 10 位 Team ID |
| `IOS_CERTIFICATE_P12_BASE64` | 包含私钥的 Apple 签名证书 `.p12` 的 Base64 |
| `IOS_CERTIFICATE_PASSWORD` | 导出 `.p12` 时设置的密码；空密码的证书可不设 |
| `IOS_PROVISIONING_PROFILE_BASE64` | 与证书、包名及分发方式匹配的 `.mobileprovision` 的 Base64 |

在同一页面的 **Variables** 添加 `IOS_BUNDLE_ID`，默认是 `com.xwyin.homecoming`；必须与 Apple 创建的 App ID 和描述文件匹配。

Unity 的 Windows 本机激活不等于 GitHub macOS 构建机已经获得授权。授权配置按 [GameCI 激活文档](https://game.ci/docs/github/activation/) 设置；如果你使用的 Unity Personal 授权无法通过 CI 激活，需要换用受支持的 CI 授权或已激活的自托管 Mac 构建机，不能用绕过授权的方式打包。

PowerShell 将文件转为 Base64（只在自己的终端操作）：

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\Certificates\distribution.p12')) | Set-Clipboard
# 把剪贴板内容粘贴到 IOS_CERTIFICATE_P12_BASE64
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\Certificates\Homecoming.mobileprovision')) | Set-Clipboard
# 把剪贴板内容粘贴到 IOS_PROVISIONING_PROFILE_BASE64
```

## 分发方式

- **ad-hoc**：Apple Distribution 证书 + Ad Hoc 描述文件，文件内必须包含目标 iPhone 的 UDID；可以安装到登记的设备。
- **app-store**：Apple Distribution 证书 + App Store 描述文件，产物用于上传 App Store Connect / TestFlight。此工作流只导出 IPA，不自动上传或发布。
- **development**：Apple Development 证书 + Development 描述文件，用于已登记设备开发测试，手机需要开启开发者模式。

证书需要包含私钥，且必须属于描述文件允许的证书。签名脚本会检查过期日期、Team、包名、证书和分发方式，完成后清理临时钥匙串与描述文件。签名后的 IPA 才是可按所选方式安装或分发的产物；单独把 `.app` 压缩并改名 `.ipa` 不具备合法签名。

## 当前资产

人物为本机《巫师 3》转换的叶奈法 DLC 服装与适配长靴，版权属于原权利人，不是本项目原创或免费商用模型。当前私有仓库用于该项目测试，工作流不会自动公开发布游戏。需要对外发行时应先替换为具有相应授权的模型。原 VRoid 备份的条款保存在 `Assets/ThirdParty/VRoidSample/LICENSE-AND-SOURCE.md`。

## 排错

缺少 Secrets 时，流程会在 Ubuntu 配置检查阶段停止，不启动 Mac 构建机。Unity 激活失败查看 Unity 步骤；签名失败查看签名步骤；Xcode 错误查看 `iOS-build-logs` Artifact。尚未提供 Apple 证书与 Unity CI 授权时，添加工作流不代表已经生成 IPA。

参考：[Unity iOS 构建](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/iphone/ios-building-and-delivering/build-process)、[GameCI Builder](https://game.ci/docs/github/builder/)、[GitHub 安装 Apple 签名证书](https://docs.github.com/en/actions/how-tos/deploy/deploy-to-third-party-platforms/install-apple-certificates)。
