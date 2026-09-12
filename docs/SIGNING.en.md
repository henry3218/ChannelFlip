# Signing roadmap for protected Windows audio

[繁體中文](SIGNING.md) | **English**

Reference review date: 2026-09-12. This is a future release plan. The project has not obtained Microsoft audio signing or hardware certification.

## Signing and the MIT license

MIT describes how others may use, modify, and distribute source code. Windows signatures identify particular binaries and verify their integrity; the protected audio environment has additional trust requirements. Publishing source code does not confer that trust.

If the APO DLL, its dependencies, and deployment meet protected-environment requirements and carry signatures accepted there, the project can aim to keep protection enabled without `DisableProtectedAudioDG=1`. The final signed package must be tested with protection enabled; a valid EXE signature alone is insufficient. See [Protected Media Path](https://learn.microsoft.com/en-us/windows/win32/medfound/protected-media-path).

## Typical certification work

1. Confirm Hardware Developer Program eligibility and prepare the organization information, Microsoft Entra ID global administrator, authorized signatory, and EV code-signing certificate. EV enrollment verifies the developer account; it does not by itself establish trust for an APO. See [registration requirements](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/hardware-program-register).
2. Prepare a submit-ready APO/driver package, including INF, deployment, and restoration for the target Windows versions and supported devices. Windows 11 uses the `AudioProcessingObject` setup class. See [APO deployment](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/deploying-audio-processing-objects).
3. Run the applicable HLK/WHCP tests and request the needed protected-environment signature attributes. Microsoft's `SignatureAttributes` examples include PETrust and DRM for audio DLLs; ordinary Authenticode or self-signing is not a substitute. See [signature attributes](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/inf-signatureattributes-section).
4. Submit the tests/package through [Partner Center](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/), then verify, package, and release the exact returned signed binaries.

Attestation signing is also available for certain testing scenarios. It is not Windows compatibility certification and does not prove that this project satisfies every requirement for a public production release. See [signing options](https://learn.microsoft.com/en-us/windows-hardware/drivers/dashboard/driver-signing-offerings).

## Remaining project work

- Review the custom core and attachment process against target certification APIs, packaging, and protected-environment rules. A WHQL signature does not compensate for an APO that violates loading requirements. See [APO implementation requirements](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/implementing-audio-processing-objects#disable-use-of-an-embedded-manifest).
- Add a signed-package build/release workflow. Keep signing keys and certificates out of Git.
- Make first-time setup of the certified version preserve the original protection setting, then test channel swapping, effect chaining, DRM, upgrades, and full restoration.
- Signatures apply only to the exact signed files. A modified or rebuilt MIT version needs its own appropriate signatures or a clearly documented development/test setup.

Even with accepted signatures, installation/registration may still require administrator approval and reloading audio services. Signing can remove the need to bypass protected audio; system audio processing still needs setup.
