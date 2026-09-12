# Security-relevant behavior

Channel Flip loads a user-mode audio processing DLL into the Windows audio host after an explicit first-run confirmation and administrator approval. The current core is not WHQL certified. Its unsigned setup uses `DisableProtectedAudioDG=1`, affecting the system-wide protected audio path until the setting is restored.

The installed DLL and recovery metadata are stored in administrator-controlled locations. The shared state file allows local users to toggle processing; it is not a place for executable code or arbitrary DSP configuration. The callback validates its fixed layout and uses bounded audio buffers.

The application does not record or upload audio. Diagnostic reports contain local device names and endpoint IDs, so review them before posting publicly.

For reports involving code execution, privilege boundaries, unsafe registry permissions, or failed restoration, use GitHub's private vulnerability reporting when available on the [repository security page](https://github.com/henry3218/ChannelFlip/security). If that option is unavailable, [open an issue](https://github.com/henry3218/ChannelFlip/issues) requesting a private contact without including exploit details or private logs.
