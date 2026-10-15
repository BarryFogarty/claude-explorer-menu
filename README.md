# Claude Explorer Menu

Right-click any folder in Windows File Explorer and launch Claude Code there, with the model you want, or resume a recent chat.

```text
Claude  >  Opus
           Sonnet
           Resume…
           ------------------------------
           Bugfix: File not found error  (2h)
           Add unit tests for the auth module  (1d)
           Refactor the settings page  (3d)
           Explain this stack trace  (1w)
           ...
```

Two variants share one config file:

| | Basic (`basic/`) | Native (`native/`) |
| --- | --- | --- |
| Menu location | Windows 11 "Show more options" (classic menu) | Top-level Windows 11 menu |
| Recent chats list | No (Continue + Resume picker) | Yes, per folder, with real chat titles |
| Needs | PowerShell 7, Windows Terminal | Basic's requirements plus .NET SDK, VS C++ build tools, one admin step |
| Install effort | One script | Build, sign, trust a certificate, register |

If you only want a launcher, use Basic. It is a few registry keys.

## Repository layout

```text
basic/             registry-only variant (install.ps1, uninstall.ps1)
native/            Native AOT shell extension + sparse MSIX package
  src/ClaudeMenu/    the handler (C#)
  package/           AppxManifest.xml
  tests/Harness/     loads the DLL like Explorer and prints the menu
  build.ps1 sign.ps1 register.ps1 unregister.ps1
assets/            icon shared by both variants
config.json        settings shared by both variants (edit in place)
```

## Requirements

- Windows 10 or 11 (Native requires Windows 11)
- [Claude Code](https://code.claude.com) on PATH
- [Windows Terminal](https://aka.ms/terminal) (`wt`) and PowerShell 7 (`pwsh`)

## Basic variant

```powershell
.\basic\install.ps1
```

Adds a **Claude** submenu to folders and folder backgrounds (HKCU only, no admin). Entries: one per configured model, **Continue last chat**, **Resume…**. On Windows 11 it appears under **Show more options**.

Remove with `.\basic\uninstall.ps1`. Re-run `install.ps1` after editing the config.

## Native variant

A small native shell extension (C#, Native AOT) registered through a sparse MSIX package. It is the only way to get an entry on the top-level Windows 11 menu, and it builds the chat list at right-click time.

Extra requirements:

- .NET 10 SDK
- Visual Studio (or Build Tools) with the **Desktop development with C++** workload, which provides the linker Native AOT needs and the Windows SDK (`makeappx`, `signtool`)

Build and sign:

```powershell
.\native\build.ps1   # publishes native\dist\ClaudeMenu.dll and packs native\dist\ClaudeMenu.msix
.\native\sign.ps1    # signs with a self-signed certificate, exports native\dist\ClaudeMenu.cer
```

Trust the certificate once, from an **elevated** PowerShell. This only lets Windows install packages signed by that certificate:

```powershell
Import-Certificate -FilePath .\native\dist\ClaudeMenu.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Then register for your user (this restarts Explorer):

```powershell
.\native\register.ps1
```

Remove with `.\native\unregister.ps1`. If you also ran the Basic variant, run `.\basic\uninstall.ps1` so the classic entry is not duplicated.

The registered package points at `native\dist`, so re-run `build.ps1`, `sign.ps1` and `register.ps1` if you move the repository.

The handler runs in a separate `dllhost.exe` process, not inside Explorer, so a fault cannot take Explorer down.

### Chat list

- Chats come from `~\.claude\projects\<folder>\*.jsonl` (or `CLAUDE_CONFIG_DIR`), newest first.
- The label is the last `/rename` title, else Claude's own generated title, else the first prompt.
- Non-interactive runs (`claude --print`, SDK, scheduled scripts) are skipped, matching the CLI's own resume list.
- Only the last 64 KB of each of the newest files is read, and labels are cached by file path, modified time and size, so a rename or new message refreshes that chat only.
- Right-click latency is a few milliseconds once the cache is warm.

### Hiding and deleting chats

A flyout menu cannot nest a second level, so per-chat actions are modifier clicks on the chat itself:

| Gesture | Effect |
| --- | --- |
| Click | Resume the chat |
| Shift+click | Hide it from this menu (the transcript is untouched and the CLI still lists it) |
| Ctrl+click | Move the transcript to the Recycle Bin, no prompt (restore it from the Bin and it reappears) |

When a folder has hidden chats, a **Show N hidden chats** entry at the bottom un-hides them. Hidden ids are stored in `%LOCALAPPDATA%\ClaudeMenu\hidden.txt`. The menu is rebuilt on each right-click, so the change shows next time you open it.

## Configuration

`config.json` in the repository root is the only config file for both variants. Edit it in place; nothing is copied. Basic bakes it into the registry, so re-run `basic\install.ps1` after changes. Native finds it by walking up from `native\dist`, reads it on every right-click, and needs no re-registration. Missing or malformed keys fall back to the defaults.

```json
{
  "models": [
    { "label": "Opus", "flag": "opus" },
    { "label": "Sonnet", "flag": "sonnet" }
  ],
  "recentCount": 8,
  "shell": "pwsh",
  "terminal": "wt",
  "extraArgs": [],
  "separatorStyle": "native"
}
```

| Key | Meaning |
| --- | --- |
| `models` | Entries that start a new chat, launched as `claude --model <flag>` |
| `recentCount` | Chats to list (Native only, 0 to 30) |
| `shell` | Shell the terminal runs |
| `terminal` | Terminal executable (name or full path) |
| `extraArgs` | Extra arguments appended to every `claude` launch |
| `separatorStyle` | Native only. `native` uses a real separator item; `line` draws a disabled line of box characters, for Windows builds that render the native separator as a blank gap |

## Troubleshooting

- **Menu missing after register:** run `native\register.ps1` again (it restarts Explorer). Check `Get-AppxPackage BarryFogarty.ClaudeContextMenu` reports `Status: Ok`.
- **Install fails with a trust error:** the certificate step above was not run elevated.
- **Diagnostics:** create an empty file `%LOCALAPPDATA%\ClaudeMenu\debug`; the handler then writes `%LOCALAPPDATA%\ClaudeMenu\log.txt`. Delete the file to stop logging.
- **Stale chat labels:** delete `%LOCALAPPDATA%\ClaudeMenu\titles.v2.tsv`.
- **Hidden chats by mistake:** use **Show N hidden chats**, or delete `%LOCALAPPDATA%\ClaudeMenu\hidden.txt`.
- **Test without Explorer:** `dotnet run -c Release --project native\tests\Harness -- native\dist\ClaudeMenu.dll "D:\some\folder"` loads the DLL the way Explorer does and prints the menu for a folder.

## Limitations

- Windows controls where a packaged entry sits in the top-level menu; it cannot be pinned above or below a specific built-in item.
- The submenu cannot draw the full-width rule the built-in menu uses between sections; see `separatorStyle`.
- The transcript fields used for titles and the interactive/non-interactive check are not a documented Claude Code format. If they change, labels fall back to the first prompt, and the Native menu still works.
- The package is self-signed, so it is for personal use, not distribution.

## Roadmap

See [ROADMAP.md](ROADMAP.md).

## Disclaimer

This is an unofficial community tool. Claude and the Claude logo are trademarks of Anthropic; this project is not affiliated with or endorsed by Anthropic.
