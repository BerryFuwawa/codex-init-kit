import base64,re
from pathlib import Path
root=Path(__file__).resolve().parent.parent
path=next((root/'scripts').glob('第1步-*.cmd'))
text=path.read_bytes().decode('gbk')
match=re.search(r'::PROCESS_STOP_PAYLOAD_BEGIN\r\n(.*?)::PROCESS_STOP_PAYLOAD_END',text,re.S)
old=base64.b64decode(''.join(re.findall(r'::PSB64:([A-Za-z0-9+/=]+)',match[1]))).decode('utf-8')
facade=old[old.index('function Get-ReadOnlyDesktopInfo {'):]
payload=(root/'app/InitializationPreflight.ps1').read_text(encoding='utf-8-sig')+'\n'+facade
encoded=base64.b64encode(payload.encode('utf-8')).decode('ascii')
chunks='\r\n'.join('::PSB64:'+encoded[i:i+160] for i in range(0,len(encoded),160))+'\r\n'
text=text[:match.start(1)]+chunks+text[match.end(1):]
path.write_bytes(text.encode('gbk'))
assert payload.startswith((root/'app/InitializationPreflight.ps1').read_text(encoding='utf-8-sig'))
assert not re.search(r'taskkill\s+/f\s+/im',text,re.I)
assert len(re.findall(r'call :STOP_CODEX\r\nif errorlevel 1',text))==3
assert text.count('::PROCESS_STOP_PAYLOAD_BEGIN\r\n')==1
stop=text.split('\r\n:STOP_CODEX\r\n',1)[1].split('\r\n:CLEAR_USER_RUNTIME',1)[0]
assert max(map(len,stop.splitlines()))<8191
print('PASS canonical payload, exact force rules, three failure gates, GBK roundtrip and command length.')
