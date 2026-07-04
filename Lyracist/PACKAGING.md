# Lyracist Packaging Guidelines (MSIX)

This document describes how to bundle and distribute Lyracist as a packaged Windows app (MSIX) targeting Windows 10 and 11.

## MSIX Packaging Tool Configuration

1. **Publisher Info**:
   - **Publisher Display Name**: `PAROLE Software`
   - **Common Name (CN)**: `CN=PAROLE Software` (or your code-signing certificate CN)

2. **Application Properties**:
   - **Package Name**: `Lyracist`
   - **Display Name**: `Lyracist`
   - **Description**: `A premium dual-display karaoke lyrics projector and tablet host queue manager.`

3. **Visual Assets**:
   - **Logo**: Use [LyracistLogo.png](file:///c:/VB26/Lyracist/Lyracist/Assets/LyracistLogo.png) for app list logos, taskbar badges, and tile screens.
   - **Icon**: Use [LyracistIcon.ico](file:///c:/VB26/Lyracist/Lyracist/Assets/LyracistIcon.ico) as the desktop assembly icon.

4. **Target Platform**:
   - **Minimum Version**: Windows 10, Version 1809 (Build 17763) or newer.
   - **Target Version**: Windows 11 (Build 22000) or newer.

5. **Signing**:
   - Package must be signed using a valid code-signing certificate (trusted by the target machine) before installation.
