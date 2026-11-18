import argparse, http.server, json, os, subprocess, sys, threading
sys.stdout.reconfigure(encoding="utf-8")
from pathlib import Path

parser=argparse.ArgumentParser(description='验证产品代理合并、诊断和官方 CLI 的持久配置读取；仅使用隔离目录与不转发流量的回环代理。')
parser.add_argument('kit');parser.add_argument('cli');parser.add_argument('output')
args=parser.parse_args();out=Path(args.output).resolve();out.mkdir(parents=True,exist_ok=True)
fixture=out/'PersistentNetwork.CliFixture.exe'
csc=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
subprocess.run(['rtk','proxy',str(csc),'/nologo','/target:exe','/reference:System.Web.Extensions.dll','/out:'+str(fixture),str(Path(__file__).with_name('PersistentNetwork.CliFixture.cs'))],check=True)
(fixture.with_suffix('.exe.config')).write_text('<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>',encoding='utf-8')
connections=[]
class Proxy(http.server.BaseHTTPRequestHandler):
    def do_CONNECT(self):
        connections.append({'target':self.path,'proxyPort':self.server.server_port})
        self.send_error(502,'isolated test proxy does not forward')
    def log_message(self,*args): pass
servers=[http.server.ThreadingHTTPServer(('127.0.0.1',0),Proxy) for _ in range(2)]
for server in servers:threading.Thread(target=server.serve_forever,daemon=True).start()
saved_port,inherited_port=[s.server_port for s in servers]
home=out/'isolated-home';home.mkdir(exist_ok=True)
(home/'config.toml').write_text('''cli_auth_credentials_store = "file"
model = "gpt-6.1-sol"
model_provider = "isolated_probe"
[features]
respect_system_proxy = false
system_proxy_fallback = false
[model_providers.isolated_probe]
name = "Isolated local network probe"
base_url = "https://codex-persistence.invalid/v1"
wire_api = "responses"
requires_openai_auth = false
''',encoding='utf-8')
env=dict(os.environ);env['CODEX_HOME']=str(home)
for key in list(env):
    if key.lower() in ('http_proxy','https_proxy','all_proxy','no_proxy') or key in ('NODE_USE_ENV_PROXY','OPENAI_API_KEY','OPENAI_BASE_URL'):env.pop(key)
env['HTTP_PROXY']=env['HTTPS_PROXY']=f'http://127.0.0.1:{inherited_port}'
try:
    subprocess.run(['rtk',str(fixture),str(Path(args.kit).resolve()),'prepare',str(saved_port)],env=env,check=True,timeout=15)
    original=(home/'.env').read_bytes();runs=[]
    for attempt in range(2):
        report_path=out/f'probe-{attempt+1}.json';previous=len(connections)
        subprocess.run(['rtk',str(fixture),str(Path(args.kit).resolve()),str(Path(args.cli).resolve()),str(report_path)],env=env,check=True,timeout=55)
        report=json.loads(report_path.read_text(encoding='utf-8'));seen=connections[previous:]
        assert seen and all(c['proxyPort']==saved_port and c['target']=='codex-persistence.invalid:443' for c in seen),seen
        assert f':{saved_port}' in report['savedConfiguration'] and f':{inherited_port}' not in report['savedConfiguration']
        assert report['doctor']['checks']['config.load']['details']['config.toml']==str(home/'config.toml')
        assert report['parsed']['passed'] is False and '未进行握手' in report['parsed']['modelState']
        runs.append({'attempt':attempt+1,'connections':seen,'modelState':report['parsed']['modelState'],'proxyPolicy':report['parsed']['proxyPolicy']})
    assert (home/'.env').read_bytes()==original and b'CUSTOM=preserve' in original
    result={'passed':True,'freshCliProcesses':2,'savedProxyPort':saved_port,'inheritedProxyPort':inherited_port,'dotenvFileUnchanged':True,'proxyForwardedTraffic':False,'actualCodexDesktopStarted':False,'actualUserConfigChanged':False,'runs':runs}
    (out/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
finally:
    for server in servers:server.shutdown();server.server_close()
