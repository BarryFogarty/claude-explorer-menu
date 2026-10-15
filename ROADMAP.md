# Roadmap

## Now

- Phase 1: static folder menu (Opus, Sonnet, Continue, Resume…), config-driven, HKCU. Done.
- Phase 2: native C# (Native AOT) `IExplorerCommand` handler in a sparse MSIX package for the top-level Windows 11 menu, with a live recent-chats list per folder.
  - Label per session: last `custom-title` record, else last `ai-title`, else first user prompt.
  - Read only the last ~64 KB of each of the newest N `.jsonl` files (`recentCount`, default 8).
  - Label cache keyed by file path + mtime + size (invalidate on any change; drop entries for deleted files).

## TODO: README and discoverability

- Animated GIF at the top of the README showing the real behaviour: right-click a folder, the **Claude** submenu opening (models, Resume…, chat list), a chat launching in Windows Terminal, then Shift+click hiding a chat and **Show N hidden chats** bringing it back. Keep it short (under about 15 s) and small enough to load fast; record at 100% scaling on a clean folder with non-sensitive chat titles.
- Static screenshot of the menu as a fallback for the GIF (and for the GitHub social preview image).
- Search discoverability:
  - Repo description and GitHub topics (for example `claude-code`, `anthropic`, `windows-11`, `context-menu`, `file-explorer`, `shell-extension`, `native-aot`, `iexplorercommand`).
  - README headings and first paragraph that use the phrases people search for: "Claude Code", "Windows Explorer right-click", "Open in terminal", "resume chat", "Windows 11 context menu".
  - Short "Why" section: what the built-in options lack (no folder launcher, no per-folder chat list).
  - Social preview image (1280x640).
- Comparison table against the existing launchers (claude-from-here and similar) so searchers can pick quickly.
- Trademark note: Claude and the Claude logo belong to Anthropic; confirm the icon usage is acceptable for a public repo or swap in a neutral glyph.
- Install section polish: a one-line install per variant, and a note on what each variant changes on the machine (registry keys, package, certificate).
- Release packaging: tagged GitHub release with a prebuilt Basic zip, so Basic users never need a toolchain.

## TODO: Claude chat indexing service

Consider splitting session discovery out of the DLL into a small, lightweight "Claude chat indexing" utility (in the spirit of LLMnesia / Mantra).

- Watches `~/.claude/projects/**/*.jsonl` (FileSystemWatcher, plus a periodic reconcile scan as a safety net) and keeps a small index (SQLite or a flat JSON per project): session id, project dir, title (custom > ai > first prompt), last-modified, message count.
- The DLL then does a single indexed read per right-click instead of touching transcript files, so the menu appears effectively instantly regardless of how many sessions a folder has.
- Why: if the popup is not fast, the context menu loses its point versus typing `claude --resume`. Treat menu latency as the acceptance test (target: no perceptible delay at the first right-click after a cold start).
- Decide after measuring Phase 2 on a cold cache: if the DLL's tail-read + cache is fast enough, skip this.
- Design notes:
  - The index must be safe to read while the service writes (SQLite WAL, or write-temp-then-rename).
  - The DLL must still work with no service running (fall back to direct tail-read), so the service is an accelerator, not a dependency.
  - Same index could back other tools (cross-project chat search, `claude --resume` shortcuts).
