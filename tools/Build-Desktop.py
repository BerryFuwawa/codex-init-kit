import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import uuid

sys.stdout.reconfigure(encoding="utf-8")
repo = Path(__file__).resolve().parent.parent
work = repo
arguments = [arg for arg in sys.argv[1:] if arg != "--wizard-preview"]
wizard_preview = "--wizard-preview" in sys.argv[1:]
output = Path(arguments[0]) if arguments else repo / "build" / "desktop-native"
framework = Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319"
build = (repo / "tools/Build-Desktop.ps1").read_text(encoding="utf-8-sig")
output.mkdir(parents=True, exist_ok=True)
exe = output / "CodexInitKit.exe"
references = ["System.ServiceProcess.dll", "System.Management.dll", "System.IO.Compression.dll", "System.IO.Compression.FileSystem.dll", "System.dll", "System.Core.dll", "System.Web.Extensions.dll", "WPF/WindowsBase.dll",
              "WPF/PresentationCore.dll", "WPF/PresentationFramework.dll", "System.Xaml.dll"]
resources = {
    "Brand.logo.png": repo / "assets/logo.png",
    "Brand.app.ico": repo / "assets/app.ico",
    "UI.Controls.xaml": repo / "app/Controls.xaml",
    "Payload.init.cmd": next((repo / "scripts").glob("*V4.7.0*.cmd")),
    "Payload.guard.cmd": next((repo / "scripts").glob("*V5.1.2*.cmd")),
    "Payload.cua.cmd": repo / "scripts/Codex_CUA_Repair.cmd",
}
for path, name in re.findall(r'/resource:\$\(Join-Path \$root \'([^\']+)\'\),([^"\r\n]+)', build):
    resources[name] = repo / path
for name in ("RtkRules.md", "FolderRules.md", "SubagentRules.md", "HERO-Anti-OverDefense.LICENSE.txt"):
    resources["Payload." + name] = repo / "app" / name
sources = list((repo / "app").glob("*.cs")) + list((repo / "native").glob("*.cs"))
for data in (repo / "native/Data").iterdir(): resources["Native." + data.name] = data
for name in ["RtkRules.md","AntiOverDefense.md","WorkJournal.md","CommandLineFirst.md","OpenSourceReference.md","FolderRules.md","CredentialManagementRules.md","SubagentRules.md","HERO-Anti-OverDefense.LICENSE.txt"]: resources["Native.Desktop." + name] = repo / "app" / name
sources += [repo / path for path in re.findall(r"\$argsList\+=\(Join-Path \$root '(tests\\[^']+\.cs)'\)", build)]
resources["Payload.Project.LICENSE"] = repo / "LICENSE"
args = ["/nologo", "/target:winexe", "/platform:x64", "/optimize+", "/utf8output",
        "/win32manifest:" + str(repo / "app/app.manifest"),
        "/win32icon:" + str(repo / "assets/app.ico")]
args += ["/reference:" + str(framework / path) for path in references]
if wizard_preview:
    args.append("/define:INITIALIZATION_DESIGN_PREVIEW")
args += ["/resource:" + str(path) + "," + name for name, path in resources.items()]
args += [str(path) for path in sources]
compiler = ["rtk", "proxy", str(framework / "csc.exe")]
if wizard_preview:
    with tempfile.TemporaryDirectory(dir=output) as staging:
        staged_exe = Path(staging) / exe.name
        result = subprocess.run([*compiler, "/out:" + str(staged_exe), *args])
        if result.returncode == 0:
            staged_exe.replace(output / "CodexInitKit-WizardPreview.exe")
else:
    result = subprocess.run([*compiler, "/out:" + str(exe), *args])
raise SystemExit(result.returncode)
