@AGENTS.md

## Claude Code notes

- Cloud sessions run `scripts/cloud/session_start.sh` on start (wired in `.claude/settings.json`),
  which installs the .NET 8 SDK if missing so `make test` and `make sim` work.
- Unity is not available in cloud sessions. Change Unity code carefully, keep it thin, and say in
  the PR that it was not compiled in the editor.
- The NAS (192.168.1.203) and Gitea are on Brennan's LAN and not reachable from the cloud.
