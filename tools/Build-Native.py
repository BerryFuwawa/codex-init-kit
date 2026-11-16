import argparse,json,os,subprocess,sys
from pathlib import Path
parser=argparse.ArgumentParser(description='构建原生 C# 初始化 CLI，不运行 PowerShell。')
parser.add_argument('output',nargs='?',default=None)
parser.add_argument('--tests',action='store_true')
args=parser.parse_args()
root=Path(__file__).resolve().parent.parent
out=Path(args.output).resolve() if args.output else root/'build/native'
out.mkdir(parents=True,exist_ok=True)
framework=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319'
name='NativeInitializer.Tests.exe' if args.tests else 'CodexInitKit.Cli.exe'
params=['/nologo','/target:exe','/platform:x64','/optimize+','/utf8output','/out:'+str(out/name),'/win32manifest:'+str(root/'native/native.manifest')]
for reference in ['System.dll','System.Core.dll','System.Web.Extensions.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll']:
    params.append('/reference:'+str(framework/reference))
for resource in sorted((root/'native/Data').iterdir()):
    params.append('/resource:'+str(resource)+',Native.'+resource.name)
if args.tests:
    for resource in sorted((root/'tests/Data').glob('Cua*.js')):
        params.append('/resource:'+str(resource)+',NativeTest.'+resource.name)
params.extend(str(path) for path in sorted((root/'native').glob('*.cs')))
params.extend([str(root/'app/GitHubTransport.cs'),str(root/'app/ProxyPortSettings.cs')])
params.append(str(root/('tests/NativeInitializer.Tests.cs' if args.tests else 'tools/NativeCliMain.cs')))
rsp=out/('tests.rsp' if args.tests else 'build.rsp')
rsp.write_text('\n'.join('"'+p+'"' for p in params),encoding='utf-8-sig')
result=subprocess.run(['rtk','proxy',str(framework/'csc.exe'),'@'+str(rsp)],cwd=root,capture_output=True,timeout=90)
(out/('tests-build.log' if args.tests else 'build.log')).write_bytes(result.stdout+result.stderr)
if result.returncode==0:
    config='<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup><runtime><AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false;Switch.System.IO.BlockLongPaths=false" /></runtime></configuration>'
    (out/(name+'.config')).write_text(config,encoding='utf-8')
if not args.tests and result.returncode==0:
    helper_args=[p.replace('/out:'+str(out/name),'/out:'+str(out/'CodexInitKit.Updater.exe')) for p in params if p!=str(root/'tools/NativeCliMain.cs')]
    helper_args.append(str(root/'tools/NativeUpdaterMain.cs'))
    helper_rsp=out/'updater.rsp'
    helper_rsp.write_text('\n'.join('"'+p+'"' for p in helper_args),encoding='utf-8-sig')
    helper=subprocess.run(['rtk','proxy',str(framework/'csc.exe'),'@'+str(helper_rsp)],cwd=root,capture_output=True,timeout=90)
    (out/'updater-build.log').write_bytes(helper.stdout+helper.stderr)
    if helper.returncode:
        sys.stdout.buffer.write(helper.stdout+helper.stderr)
        raise SystemExit(helper.returncode)
    (out/'CodexInitKit.Updater.exe.config').write_text(config,encoding='utf-8')
if args.tests and result.returncode==0:
    for fixture,fixture_name in [('NativeRtk.Fixture.cs','rtk-fixture.exe'),('NativeUpdate.Fixture.cs','update-fixture.exe')]:
        command=['rtk','proxy',str(framework/'csc.exe'),'/nologo','/target:exe','/utf8output','/out:'+str(out/fixture_name),str(root/'tests'/fixture)]
        built=subprocess.run(command,cwd=root,capture_output=True,timeout=30)
        if built.returncode:
            sys.stdout.buffer.write(built.stdout+built.stderr)
            raise SystemExit(built.returncode)
if args.tests and result.returncode==0:
    fixture_source=(root/'tests/NativeUpdate.Fixture.cs').read_text(encoding='utf-8').replace('2.3.1.2','2.3.0.0')
    old_source=out/'old-update-fixture.cs'
    old_source.write_text(fixture_source,encoding='utf-8')
    old_build=subprocess.run(['rtk','proxy',str(framework/'csc.exe'),'/nologo','/target:exe','/utf8output','/out:'+str(out/'old-cli-fixture.exe'),str(old_source)],cwd=root,capture_output=True,timeout=30)
    if old_build.returncode:
        sys.stdout.buffer.write(old_build.stdout+old_build.stderr)
        raise SystemExit(old_build.returncode)
if result.returncode:
    sys.stdout.buffer.write(result.stdout+result.stderr)
    raise SystemExit(result.returncode)
print(json.dumps({'built':str(out/name),'bytes':(out/name).stat().st_size,'nativeProcessOnly':True}))
