"""Compile the complete production DXItemGrid with explicit headless substitutes."""
import argparse
import os
from pathlib import Path
import subprocess
from xml.sax.saxutils import escape

checks = Path(__file__).resolve().parent
source = checks.parents[1] / "GodotClient/Controls/DXItemGrid.cs"
build = Path(os.environ["AGENT_BUILD_DIR"]) / "footprint-checks"
build.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--revision", help="Compare a committed DXItemGrid from this repository")
args = parser.parse_args()
if args.revision:
    revision = subprocess.check_output(
        ["git", "rev-parse", "--verify", "--end-of-options", args.revision + "^{commit}"],
        cwd=checks, text=True).strip()
    content = subprocess.check_output(
        ["git", "show", revision + ":GodotClient/Controls/DXItemGrid.cs"], cwd=checks)
    source = build / "ComparedDXItemGrid.cs"
    source.write_bytes(content)
    print(f"Comparing committed source {revision}", flush=True)
feed = build / "empty-feed"
feed.mkdir(exist_ok=True)
files = [source, checks / "Stubs.cs", checks / "Program.cs"]
includes = "\n".join('<Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />'
                     for p in files)
project = build / "FootprintChecks.csproj"
project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup>{includes}</ItemGroup></Project>
''', encoding="utf-8")
env = dict(os.environ, DOTNET_CLI_HOME=str(build / "cli"),
           DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
           DOTNET_GENERATE_ASPNET_CERTIFICATE="false", MSBUILDDISABLENODEREUSE="1",
           DOTNET_CLI_USE_MSBUILD_SERVER="0",
           NUGET_PACKAGES=str(build.parent.parent / "cache/nuget"))
dotnet = os.environ.get("DOTNET", "dotnet")
subprocess.run([dotnet, "build", str(project), "--disable-build-servers", "-v:q",
                "-p:UseSharedCompilation=false", "-nodeReuse:false", "-m:1",
                f"-p:RestoreSources={feed}", "-p:NuGetAudit=false"],
               env=env, check=True, timeout=90)
result = subprocess.run([dotnet, str(build / "bin/Debug/net10.0/FootprintChecks.dll")],
                        env=env, timeout=30)
raise SystemExit(result.returncode)
