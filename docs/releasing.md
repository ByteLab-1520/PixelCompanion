# Release process

Windows releases are created only from version tags and only after SignPath returns a trusted Authenticode-signed installer. The checksum attached to a GitHub Release is generated from that signed file, not from the unsigned build artifact.

## One-time GitHub configuration

After the SignPath open-source application is approved, configure the GitHub repository with:

- Secret `SIGNPATH_API_TOKEN`
- Variable `SIGNPATH_ORGANIZATION_ID`
- Variable `SIGNPATH_PROJECT_SLUG`
- Variable `SIGNPATH_SIGNING_POLICY_SLUG`

Link the SignPath GitHub trusted build system and project to this repository. The workflow uses `signpath/github-action-submit-signing-request@v2` and the artifact ID produced by `actions/upload-artifact`.

## Creating a release

1. Ensure `Directory.Build.props`, the installer script, and the Inno Setup fallback all contain the intended semantic version.
2. Merge the tested release changes into `main`.
3. Create and push a matching tag such as `v0.2.0`.
4. The `release` workflow builds the installer, submits it to SignPath, verifies the returned signature, regenerates SHA-256, and creates the GitHub Release.

The workflow stops without publishing if the version does not match, SignPath configuration is missing, signing fails, the returned signature is untrusted, or the checksum cannot be verified.

## Local packaging

Build an unsigned installer from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-WindowsInstaller.ps1 -Version 0.2.0
```

The build checksum ends in `.unsigned.sha256` and must not be attached to a public Release. After obtaining a signed installer, finalize it with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Finalize-WindowsRelease.ps1 `
  -SignedInstallerPath C:\path\to\PixelCompanion-Installer.exe `
  -Version 0.2.0
```

The final signed installer and post-signing checksum are written to `artifacts/windows/release/`.

The regular Windows CI job also installs the unsigned build into an isolated directory, verifies the versions of all three executables, and uninstalls it with `scripts/Test-WindowsInstaller.ps1`.
