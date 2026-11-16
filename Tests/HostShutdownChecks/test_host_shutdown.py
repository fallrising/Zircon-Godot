"""Run the actual ServerCore entry point against a controlled fake world.

Requires Linux, Python 3, .NET 10 and the common workspace launcher.
This checks host shutdown ordering, not real MirDB persistence or gameplay.
"""
import os
from pathlib import Path
import selectors
import signal
import subprocess
import tempfile
import time
import unittest
from xml.sax.saxutils import escape


class HostShutdownTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dotnet = os.environ.get("DOTNET", "dotnet")
        checks = Path(__file__).resolve().parent
        source = checks.parents[1] / "ServerCore" / "Program.cs"
        build = Path(os.environ["AGENT_BUILD_DIR"]) / "host-checks"
        build.mkdir(parents=True, exist_ok=True)
        offline_feed = build / "empty-feed"
        offline_feed.mkdir(exist_ok=True)
        project = build / "HostChecks.csproj"
        source_xml = escape(str(source), {'"': '&quot;'})
        fixture_xml = escape(str(checks / "HostFixture.cs"), {'"': '&quot;'})
        project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="{source_xml}" />
    <Compile Include="{fixture_xml}" />
  </ItemGroup>
</Project>
''', encoding="utf-8")
        cls.env = dict(
            os.environ,
            DOTNET_CLI_HOME=str(build / "cli"),
            DOTNET_CLI_TELEMETRY_OPTOUT="1",
            DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
            DOTNET_GENERATE_ASPNET_CERTIFICATE="false",
            MSBUILDDISABLENODEREUSE="1",
            DOTNET_CLI_USE_MSBUILD_SERVER="0",
            NUGET_PACKAGES=str(build.parent.parent / "cache" / "nuget"),
        )
        result = subprocess.run(
            [cls.dotnet, "build", str(project), "--disable-build-servers", "-v:q",
             "-p:UseSharedCompilation=false", "-nodeReuse:false", "-m:1",
             f"-p:RestoreSources={offline_feed}", "-p:NuGetAudit=false"],
            env=cls.env, capture_output=True, text=True, timeout=90,
        )
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        cls.dll = build / "bin" / "Debug" / "net10.0" / "HostChecks.dll"

    def run_host(self, self_stop=False):
        with tempfile.TemporaryDirectory(
            prefix="host-shutdown-", dir=os.environ["TMPDIR"]
        ) as directory:
            gate = Path(directory) / "save-complete"
            process = subprocess.Popen(
                [self.dotnet, str(self.dll)],
                env=dict(self.env, HOST_SELF_STOP=str(int(self_stop)),
                         HOST_SAVE_GATE=str(gate)),
                stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT, start_new_session=True,
            )
            output = bytearray()
            try:
                with selectors.DefaultSelector() as selector:
                    selector.register(process.stdout, selectors.EVENT_READ)

                    def wait_for(marker):
                        deadline = time.monotonic() + 5
                        while marker not in output:
                            remaining = deadline - time.monotonic()
                            self.assertGreater(remaining, 0, output.decode(errors="replace"))
                            self.assertTrue(selector.select(remaining),
                                            output.decode(errors="replace"))
                            chunk = os.read(process.stdout.fileno(), 4096)
                            self.assertTrue(chunk, "host exited early: "
                                            + output.decode(errors="replace"))
                            output.extend(chunk)

                    wait_for(b"READY\n")
                    if not self_stop:
                        process.send_signal(signal.SIGINT)
                    wait_for(b"WORLD_STOPPING\n")
                    self.assertIsNone(process.poll(), output.decode(errors="replace"))
                    self.assertNotIn(b"WORLD_SAVED", output)
                    self.assertNotIn(b"CONFIG_SAVED", output)
                    gate.write_text("release", encoding="utf-8")
                    wait_for(b"WORLD_SAVED\n")
                    # Keep stdin open: shutdown cannot depend on a newline or EOF.
                    try:
                        process.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        self.fail("host hung after world stop with stdin open: "
                                  + output.decode(errors="replace"))
                    output.extend(process.stdout.read())
                self.assertEqual(process.returncode, 0, output.decode(errors="replace"))
                self.assertIn(b"WORLD_SAVED\nCONFIG_SAVED\n", output)
                self.assertEqual(output.count(b"CONFIG_SAVED\n"), 1)
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait(timeout=5)
                process.stdin.close()
                process.stdout.close()

    def test_sigint_waits_for_world_save_then_saves_config(self):
        self.run_host()

    def test_internal_stop_exits_with_stdin_still_open(self):
        self.run_host(self_stop=True)


if __name__ == "__main__":
    unittest.main()
