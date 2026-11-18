import base64,re
from pathlib import Path
root=Path(__file__).resolve().parent.parent
text=next((root/'scripts').glob('第1步-*.cmd')).read_bytes().decode('gbk')
match=re.search(r'::PROCESS_STOP_PAYLOAD_BEGIN\r\n(.*?)::PROCESS_STOP_PAYLOAD_END',text,re.S)
assert match, 'Standalone termination payload is missing'
payload=base64.b64decode(''.join(re.findall(r'::PSB64:([A-Za-z0-9+/=]+)',match[1]))).decode('utf-8')
assert payload.startswith((root/'app/InitializationPreflight.ps1').read_text(encoding='utf-8-sig')), 'Standalone payload differs from tested preflight'
assert not re.search(r'taskkill\s+/f\s+/im',text,re.I), 'Global image-name termination remains'
assert len(re.findall(r'call :STOP_CODEX\r\nif errorlevel 1',text))==3, 'Caller ignores termination failure'
stop=text.split('\r\n:STOP_CODEX\r\n',1)[1].split('\r\n:CLEAR_USER_RUNTIME',1)[0]
assert max(map(len,stop.splitlines()))<8191, 'Bootstrap exceeds cmd command limit'
assert 'Stop-InitializationApps' in payload and '$process.Kill()' in payload and 'CloseMainWindow' not in payload
print('PASS standalone canonical force payload, scoped selection, failure gates and command length; no live processes touched.')
