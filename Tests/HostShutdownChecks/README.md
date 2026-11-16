# Host shutdown checks

These Linux process checks compile the actual `ServerCore/Program.cs` with
substitutes for configuration and the world. They require Python 3 and the
.NET 10 SDK; no NuGet packages, game assets, network listeners or databases.

From the repository root, run through the common build workspace launcher,
setting `DOTNET` to the installed SDK executable when it is not on `PATH`:

```sh
DOTNET=/path/to/dotnet python3 /home/ckc/test/codex/agent-workspace/workspace.py run zircon-godot host-shutdown -- python3 Tests/HostShutdownChecks/test_host_shutdown.py -v
```

The launcher supplies `AGENT_BUILD_DIR` and `TMPDIR`. Generated project,
SDK state and binaries stay under its build/cache directories. Restore uses an
empty local package feed because this check has no package dependencies.

The checks send a real SIGINT or stop the substitute world internally. A
parent-controlled save gate verifies that the host remains alive until saving
finishes. Standard input stays open without a newline or EOF throughout:
the host must exit successfully after `WORLD_SAVED`, then `CONFIG_SAVED`.
Only processes created by the checks are stopped on timeout or failure.

This proves host sequencing only. It does not verify real MirDB/Users.db
persistence, world cleanup, gameplay, assets, graphical input, SIGTERM or
service-manager shutdown. Build `ServerCore/ServerCore.csproj` separately to
check integration with the real libraries.

Adapted from the bounded lab checks and shutdown patch at
[newclear 2aa21f10](https://github.com/fallrising/newclear/tree/2aa21f10b53ecaeb079279b6531bbb47a103e937/labs/zircon-godot).
The lab's historical tested pin remains unchanged.
