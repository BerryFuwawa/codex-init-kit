using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
// Presentation only: backend messages, control records and diagnostic inputs
// remain untouched. No logs are sent to a translation service.
public static class DesktopLocalization {
    static readonly string[,] Phrases={
        {"Expected exactly one installed official desktop package; no cross-channel installation attempted.","未能唯一确定已安装的官方桌面版；没有安装其他渠道的版本。"},
        {"Installed package is not the supported official Store channel.","当前桌面应用并非受支持的官方 Microsoft Store 版本。"},
        {"Desktop components are incomplete or the app changed during inspection.","桌面组件不完整，或检查期间桌面应用发生变化。"},
        {"Desktop package is pending or unhealthy:","桌面应用仍在更新或状态异常："},
        {"Codex is running. Save work and fully exit before continuing.","Codex 正在运行，请保存工作并彻底退出后重试。"},
        {"Codex is running. Close Codex before changing startup protection.","Codex 正在运行，请保存工作并退出后再更改启动保护。"},
        {"CODEX_STILL_RUNNING: Codex reopened or a runtime process is still running. Exit it and run the tool again.","Codex 或相关运行进程仍在运行，请保存工作并退出后重试。"},
        {"CODEX_STILL_RUNNING: Waited 180 seconds. Exit Codex and double-click this file again.","等待 180 秒后 Codex 仍未退出，请退出后重试。"},
        {"Updated native CLI failed its OpenAI signature check.","更新后的桌面原生 CLI 未通过 OpenAI 签名校验。"},
        {"Updated native CLI failed its version probe.","更新后的桌面原生 CLI 未通过版本检查。"},
        {"Unsafe process argument.","启动参数不安全，操作已停止。"},
        {"Process did not stop; completion is still pending.","进程尚未结束，操作结果仍待确认。"},
        {"Output pipe still open; installer completion pending.","安装程序的输出通道仍未关闭，完成状态待确认。"},
        {"Error pipe still open; installer completion pending.","安装程序的错误输出通道仍未关闭，完成状态待确认。"},
        {"winget is missing. Update in Microsoft Store:","未找到 winget，请在 Microsoft Store 更新桌面版："},
        {"Desktop update timed out; Store may still be completing the update. Recheck before repairing dependent components.","桌面版更新超时，商店可能仍在处理。请重新检查后再修复依赖组件。"},
        {"Desktop update requires restart or is still pending; dependent repairs were paused.","桌面版更新需要重启或尚未完成，依赖该版本的修复已暂停。"},
        {"winget completed but the installed version has not changed; completion is not verified. Recheck Store.","winget 已结束，但桌面版版本未变化，无法确认更新完成；请检查 Microsoft Store。"},
        {"Desktop update failed. Exit code ","桌面版更新失败，退出码："},
        {". Microsoft Store:","。Microsoft Store："},
        {"Official standalone CLI installer did not complete.","官方独立 CLI 安装程序未完成。"},
        {"Official standalone CLI is incomplete:","官方独立 CLI 组件不完整："},
        {"Bundled Node does not advertise environment proxy support.","内置 Node 未声明支持环境代理，已停止修复。"},
        {"Environment inheritance probe failed.","环境变量继承检查失败。"},
        {"Another CUA or browser check/repair is running. Wait for it to finish.","另一个 CUA 或浏览器检查、修复正在运行，请等待结束。"},
        {"Another CUA or browser operation is running.","另一个 CUA 或浏览器操作正在运行，请等待结束。"},
        {"Another desktop startup check or repair is running.","另一个桌面启动检查或修复正在运行，请等待结束。"},
        {"Managed patch app version differs; rollback refused.","补丁对应的桌面版版本不一致，已停止回滚。"},
        {"Launcher has unrecognized changes; no overwrite attempted. Inspect CUA components first.","浏览器入口存在无法识别的修改，未覆盖文件；请先检查 CUA 组件。"},
        {"Component integrity check failed:","组件完整性检查失败："},
        {"App version changed before rollback.","回滚前桌面版版本已变化，操作已停止。"},
        {"Existing original backup failed hash verification.","已有原始备份未通过哈希校验，操作已停止。"},
        {"Official app changed before patch; stopped.","应用补丁前官方应用已变化，操作已停止。"},
        {"Launcher changed before patch; stopped.","应用补丁前浏览器入口已变化，操作已停止。"},
        {"Desktop update pending; startup protection paused.","桌面版更新尚未完成，启动保护操作已暂停。"},
        {"Desktop update pending; browser repair paused.","桌面版更新尚未完成，浏览器修复已暂停。"},
        {"Unsupported desktop executable list; no files were changed.","不支持当前桌面启动组件列表，未修改文件。"},
        {"Unsupported desktop executable digest; no files were changed.","不支持当前桌面启动组件的哈希算法，未修改文件。"},
        {"Unsupported desktop executable cache layout; no files were changed.","不支持当前桌面启动组件目录结构，未修改文件。"},
        {"Desktop source hashing is not tied to the verified runtime implementation; no files were changed.","无法核实桌面启动组件的来源与哈希算法，未修改文件。"},
        {"The target already exists but its files are incomplete or differ from the official package. No overwrite was attempted.","目标目录已存在，但文件不完整或与官方安装包不一致；未覆盖已有文件。"},
        {"Desktop package changed during inspection; recheck the new version.","检查期间桌面应用已更新，请重新检查新版本。"},
        {"Desktop package changed during recovery; recheck the new version.","修复期间桌面应用已更新，请重新检查新版本。"},
        {"Copied file verification failed:","复制后的文件校验失败："},
        {"Final file verification failed.","最终文件校验失败。"},
        {"Cannot uniquely identify official app launch ID.","无法唯一确定官方应用启动标识，操作已停止。"},
        {"Unsupported official application identity.","官方桌面应用身份不受支持，操作已停止。"},
        {"Unknown launcher changes; no overwrite attempted.","启动入口存在无法识别的修改，未覆盖文件。"},
        {"Component inventory differs; migration stopped.","组件清单不一致，补丁迁移已停止。"},
        {"Component hash differs; migration stopped.","组件哈希不一致，补丁迁移已停止。"},
        {"Original backup hash mismatch.","原始备份的哈希不一致。"},
        {"Legacy backup hash mismatch.","旧补丁备份的哈希不一致。"},
        {"App or entry changed before migration.","迁移前应用或入口文件已变化，操作已停止。"},
        {"Startup repair requires an HTTP/mixed proxy; use browser rollback for direct mode.","启动修复需要 HTTP 或混合代理；直连模式请使用浏览器回滚。"},
        {"CheckOnly requires an HTTP/mixed proxy.","仅检查模式需要 HTTP 或混合代理。"},
        {"App updated before launch; run again.","启动前桌面应用已更新，请重新执行。"},
        {"Empty path is forbidden.","路径为空，操作已停止。"},
        {"Unsafe path outside the runtime root:","路径位于允许的运行时目录之外，操作已停止："},
        {"Refusing a link/junction/reparse point:","检测到链接或重解析点，操作已停止："},
        {"Runtime root does not match the expected cache path.","运行时目录与预期缓存路径不一致。"},
        {"Directory missing:","目录不存在："},
        {"Cannot uniquely identify cua-repl.mjs; this runtime is unsupported.","无法唯一确定 cua-repl.mjs，当前运行时不受支持。"},
        {"Unknown official launcher structure; no file was changed.","官方入口结构无法识别，未修改文件。"},
        {"Invalid managed proxy URL.","代理地址格式无效。"},
        {"Invalid managed proxy endpoint.","代理 IP 或端口无效。"},
        {"Invalid browser runtime ID.","浏览器运行时标识无效。"},
        {"Managed patch belongs to another app version or runtime; recheck required.","补丁属于其他应用版本或运行时，请重新检查。"},
        {"Original bytes or backup hash changed; refusing overwrite.","原始文件或备份哈希已变化，拒绝覆盖。"},
        {"Managed patch metadata hash mismatch.","补丁记录的哈希不一致。"},
        {"Runtime has third-party changes or a hash mismatch; refusing overwrite.","运行时存在第三方修改或哈希不一致，拒绝覆盖。"},
        {"Target directory is missing.","目标目录不存在。"},
        {"Directory set differs.","目录清单不一致。"},
        {"Missing file:","缺少文件："},
        {"File content differs:","文件内容不一致："},
        {"All file paths, directory paths, lengths and SHA256 hashes match.","文件、目录、长度和 SHA-256 均一致。"},
        {"System XCOPY is unavailable.","系统 XCOPY 不可用。"},
        {"Move destination already exists:","移动目标已存在："},
        {"Move postcondition failed. Please retain both paths for manual recovery.","移动结果校验失败，请保留两个路径以便手动恢复。"},
        {"Move destination has an unexpected item type.","移动目标的文件类型与预期不符。"},
        {"Invalid runtime ID.","运行时标识无效。"},
        {"Source and target must differ.","来源和目标不能相同。"},
        {"Source and target must not contain each other.","来源和目标不能互相包含。"},
        {"Backup path already exists.","备份路径已存在，操作已停止。"},
        {"Quarantine path already exists.","隔离目录已存在，操作已停止。"},
        {"Official source changed during repair. Please run the tool again after the app update finishes.","修复期间官方来源发生变化，请在应用更新完成后重新执行。"},
        {"Verification failed:","校验失败："},
        {"REPAIR_AND_ROLLBACK_FAILED:","修复及回滚均失败："},
        {"REPAIR_FAILED:","修复失败："},
        {"; rollback:","；回滚："},{"; backup:","；备份："},{"; incomplete target:","；未完成的目标："},
        {"Expected one registered OpenAI.Codex package; found ","预期找到一个已注册的 OpenAI.Codex 应用，实际数量："},
        {"The registered package has no installation directory.","已注册的桌面应用没有安装目录。"},
        {"Unsupported ASAR header.","应用资源包头不受支持。"},{"Invalid ASAR header size.","应用资源包头大小无效。"},
        {"Truncated ASAR header.","应用资源包头不完整。"},{"Unsupported runtime implementation size.","运行时实现文件大小不受支持。"},
        {"Invalid ASAR entry offset.","应用资源包条目偏移无效。"},{"ASAR entry is outside the archive.","应用资源包条目超出文件范围。"},
        {"Truncated runtime implementation.","运行时实现文件不完整。"},{"Cannot uniquely identify the current runtime implementation.","无法唯一识别当前运行时实现。"},
        {"This app version changed its runtime identity algorithm. The tool stopped without modifying the runtime.","当前应用的运行时标识算法已变化，工具已停止且未修改运行时。"},
        {"Unrecognized runtime hash calculation. The tool stopped without modifying the runtime.","无法识别运行时哈希算法，工具已停止且未修改运行时。"},
        {"The official cua_node source is missing. This app version is not supported by this tool.","缺少官方 cua_node 来源，当前应用版本不受支持。"},
        {"Cannot identify the current required runtime ID safely.","无法安全识别当前所需的运行时标识。"},
        {"Official source lacks required file:","官方来源缺少必需文件："},
        {"The app was updated during this run. Please run this file again.","操作期间应用已更新，请重新执行。"},
        {"Runtime identity changed during this run. Please run this file again.","操作期间运行时标识已变化，请重新执行。"},
        {"CUA library syntax is invalid.","CUA 组件脚本语法无效。"},
        {"Proxy IP must be a valid IPv4 or IPv6 address.","代理 IP 必须是有效的 IPv4 或 IPv6 地址。"},
        {"Proxy IP must be a string.","代理 IP 必须为文本。"},
        {"Proxy host must be a valid IPv4 or IPv6 address", "代理 IP 必须是有效的 IPv4 或 IPv6 地址"},
        {"Drive is required for initialize.","初始化需要选择工作盘。"},{"Invalid drive value:","工作盘无效："},
        {"JobFile was not found:","任务文件不存在："},{"JobFile is not valid JSON:","任务文件 JSON 格式无效："},
        {"JobFile ProxyPort must be an integer.","任务文件中的代理端口必须为整数。"},
        {"Operation is required.","未指定操作。"},{"ProxyPort must be between 0 and 65535.","代理端口必须在 0 到 65535 之间。"},
        {"Invalid PayloadRoot:","资源目录无效："},{"PayloadRoot is required for this operation.","此操作需要资源目录。"},
        {"PayloadRoot was not found:","资源目录不存在："},{"Payload artifact is missing:","缺少内置资源："},
        {"Executable was not found:","找不到可执行文件："},{"Unable to start process:","无法启动程序："},
        {"init.cmd is not valid GBK text.","初始化脚本的 GBK 编码无效。"},
        {"Base work-rules payload is missing.","缺少基础工作规则。"},{"init.cmd entry marker is missing.","初始化脚本缺少入口标记。"},
        {"Unable to patch init.cmd drive selector.","无法准备初始化脚本的工作盘选择。"},{"Unable to patch init.cmd confirmation block.","无法准备初始化脚本的确认步骤。"},
        {"Unrecognized process termination in init adapter; stopped.","初始化适配器包含未识别的结束进程操作，已停止。"},
        {"Unexpected initialization adapter cleanup path.","初始化临时文件路径不符合预期，已停止清理。"},
        {"init.cmd failed with exit code ","初始化脚本失败，退出码："},{"init.cmd reported a failed result.","初始化脚本报告失败。"},
        {".env was not written by proxy operation.","代理操作未写入 .env 文件。"},
        {"Proxy operation did not remove proxy environment entries.","代理操作未移除代理环境配置。"},{"Proxy operation did not write the requested proxy endpoint.","代理操作未写入所选代理地址。"},
        {"No initialization backup session is available for rollback.","没有可用于回滚的初始化备份。"},
        {"init rollback failed with exit code ","初始化回滚失败，退出码："},{"init rollback reported a failure.","初始化回滚报告失败。"},
        {"cua.cmd failed with exit code ","CUA 脚本失败，退出码："},
        {"guard.cmd PowerShell payload markers are missing.","启动保护脚本缺少 PowerShell 载荷标记。"},
        {"guard.cmd PowerShell payload is invalid base64.","启动保护脚本的载荷编码无效。"},
        {"guard.cmd embedded PowerShell payload has syntax errors.","启动保护脚本的 PowerShell 载荷存在语法错误。"},
        {"Startup protection is not installed.","尚未安装启动保护。"},{"Startup protection install did not complete.","启动保护安装未完成。"},
        {"Startup protection update did not complete.","启动保护更新未完成。"},{"Startup protection uninstall did not complete.","启动保护卸载未完成。"},
        {"Desktop native mode was not restored.","桌面原生 CLI 模式未恢复。"},{"Official Current was not selected.","官方独立 CLI 模式未选中。"},
        {"Protection rollback did not complete.","启动保护回滚未完成。"},{"Codex doctor did not produce a new diagnostic log.","Codex Doctor 未生成新的诊断日志。"},
        {"codex doctor failed with native exit code ","Codex Doctor 执行失败，退出码："},{"Codex doctor reported a failure.","Codex Doctor 报告了失败项。"},
        {"Unknown operation:","未知操作："},{"Invalid commit","更新提交标识无效"},
        {"Job directory already exists","任务目录已存在"},{"Missing resource:","缺少内置资源："},
        {"Unsupported operation","不支持此操作"},{"Worker options are empty","后台任务选项为空"},
        {"Invalid argument","参数无效"},{"Update stage checksum mismatch","更新文件校验失败"},
        {"OptionsBase64 is not valid UTF-8 JSON:","后台选项不是有效的 UTF-8 JSON："},
        {"Invalid component version.","组件版本号无效。"},{"Invalid GitHub commit ID.","GitHub 提交标识无效。"},
        {"Invalid GitHub repository.","GitHub 仓库地址无效。"},{"Update manifest repository/schema mismatch.","更新清单的仓库或格式不匹配。"},
        {"Component missing from update manifest.","更新清单缺少所需组件。"},{"Invalid script download path.","脚本下载路径无效。"},
        {"Invalid script checksum.","脚本校验值无效。"},{"Refusing to replace a directory or link.","目标为目录或链接，已停止替换。"},
        {"SHA-256 mismatch; original script was not replaced.","SHA-256 不一致，原脚本未被替换。"},
        {"Unsupported script encoding.","脚本编码不受支持。"},
        {"Downloaded file does not match the expected script component/version.","下载文件与预期脚本组件或版本不一致。"},
        {"Script changed during download; update cancelled.","下载期间脚本发生变化，更新已取消。"},
        {"Current script is unavailable.","当前脚本不可用。"},{"Script version metadata missing.","缺少脚本版本信息。"},
        {"Update payload markers missing.","缺少更新资源标记。"},{"Invalid update payload line.","更新资源内容无效。"},
        {"Invalid model ID.","模型 ID 无效。"},{"Invalid reasoning effort.","思考强度无效。"},
        {"Expected exactly one Codex folder management block.","工作目录规则应且只能存在一份，请检查规则内容。"},
        {"Expected exactly one managed V1.3 Luna prompt block.","子代理规则应且只能存在一份受管理的 V1.3 规则块。"},
        {"Folder selection output file is not configured.","未配置工作目录选择结果文件。"},
        {"No usable file-system drive was detected.","未检测到可用的文件系统磁盘。"},
        {"Requested Codex folder-management drive is unavailable.","所选工作目录磁盘不可用。"},
        {"The Luna max-effort advisory must not contain a selective-use restriction.","子代理思考强度提示中存在不兼容的使用限制。"},
        {"The Luna prompt must not retain the legacy first-confirmation flow.","子代理规则中仍保留旧版首次确认流程，请检查规则迁移结果。"},
        {"Unable to locate the single-file CMD source.","无法定位内置 CMD 脚本来源。"},
        {"installer download missing","安装程序未下载成功"},{"refusing to set invalid CODEX_CLI_PATH","CLI 路径无效，已停止设置 CODEX_CLI_PATH"},
        {"refusing to set non-runnable CODEX_CLI_PATH","CLI 无法运行，已停止设置 CODEX_CLI_PATH"},
        {"embedded PowerShell payload markers missing or invalid","内嵌 PowerShell 载荷标记缺失或无效"},
        {"installed CMD missing","已安装的 CMD 脚本缺失"},{"automatic maintenance script missing","自动维护脚本缺失"},
        {"automatic maintenance task verification failed","自动维护任务未通过校验"},
        {"package root missing","组件安装目录缺失"},{"codex.exe execution failed","codex.exe 执行失败"},
        {"Desktop bundled runtime healthy","桌面版内置运行时检查正常"},
        {"Codex root is an existing file, not a directory:","工作根目录路径已被同名文件占用："},
        {"Required Codex folder path is an existing file:","所需工作目录路径已被同名文件占用："},
        {"Codex config.toml is unavailable:","Codex 配置文件不可用："},
        {"Projectless task folder was not bound to the selected drive:","无项目任务目录未绑定到所选工作盘："},
        {"Responses WebSocket failed; HTTPS fallback may still work - Check proxy, VPN, firewall, DNS, custom CA, and WebSocket policy support.","响应流 WebSocket 连接失败，可尝试 HTTPS 回退；请检查代理、VPN、防火墙、DNS、证书和 WebSocket 策略。"},
        {"MCP configuration has optional issues - Set the missing MCP env vars or disable the affected server.","MCP 配置存在可选项问题；请补充缺失的环境变量或关闭对应服务。"},
        {"desktop update and runtime CDN is unreachable","桌面更新和运行时下载服务不可达"},
        {"update configuration is locally consistent","更新配置在本地一致"},
        {"runtime standalone","运行模式：官方独立 CLI"},{"install consistent","安装内容一致"},{"state databases healthy","状态数据库检查正常"},
        {"app-server not running (ephemeral mode)","临时模式下应用服务未运行"},
        {"Skip folder-management drive confirmation","已跳过文件夹管理工作盘确认"},
        {"Current executable changed before update","更新前当前程序已变化"},{"Updated executable checksum mismatch","更新后程序校验失败"},
        {"File not found:","找不到文件："},
        {"The process cannot access the file because it is being used by another process","文件正被其他程序占用，暂时无法访问"},
        {"Access to the path","无权访问该路径"},{"Access is denied","访问被拒绝，请检查权限"},
        {"Permission denied","访问被拒绝，请检查权限"},{"The system cannot find the file specified","系统找不到指定文件"},
        {"The system cannot find the path specified","系统找不到指定路径"},
        {"No connection could be made because the target machine actively refused it","连接被目标端口拒绝，请检查代理或服务是否运行"},
        {"The operation has timed out","操作超时，请检查网络后重试"},{"The request was canceled","请求已取消"},
        {"The remote name could not be resolved","无法解析服务器地址，请检查网络和 DNS"},
        {"The underlying connection was closed","网络连接已关闭，请检查网络或代理"},
        {"Unable to connect to the remote server","无法连接服务器，请检查网络或代理"},
        {"Connection refused","连接被拒绝，请检查代理或服务是否运行"},{"Network is unreachable","网络不可达，请检查网络连接"},
        {"Authentication failed","登录认证未通过，请检查登录状态"},{"Unauthorized","未通过身份验证，请检查登录状态和权限"},
        {"No applicable update found.","未找到可用更新。"},{"No package found matching input criteria.","未找到匹配的应用包。"},
        {"Runtime missing; repair required.","运行时缺失，需要修复。"},{"Known legacy patch; migration required.","检测到已知旧补丁，需要迁移。"},
        {"Luna 多线程提示词安装失败","子代理规则安装失败"},
        {"Proxy repair required.","需要修复代理设置。"},{"Managed patch ready; real Chrome connection not verified.","补丁已准备；真实 Chrome 连接尚未验证。"},
        {"Managed patch already ready; no entry write required.","补丁已准备，无需再次写入入口。"},
        {"Official app launch requested. Actual browser connection still needs verification.","已请求启动官方应用；真实浏览器连接仍需验证。"},
        {"CUA check found a repairable difference.","CUA 检查发现可修复的差异。"},
        {"Native CLI verified:","桌面原生 CLI 已验证："},{"Official standalone CLI ready:","官方独立 CLI 已准备："},
        {"Desktop updated:","桌面版已更新："},{"Desktop already up to date:","桌面版已是最新："},
        {"Codex version:","Codex 版本："},{"Runtime ID:","运行时标识："},{"Source files:","来源文件数："},{"Source bytes:","来源字节数："},
        {"Source:","来源："},{"Target:","目标："},{"XCOPY exit code:","XCOPY 退出码："},
        {"RESULT: PASS", "结果：通过"},{"RESULT: FAIL", "结果：失败"},{"CHECK ONLY", "仅检查"}
    };
    static readonly Regex EnglishSentence=new Regex(@"(?<![A-Za-z0-9_./\\-])[A-Za-z]{2,}(?:\s+[A-Za-z]{2,})+(?![A-Za-z0-9_./\\-])",RegexOptions.CultureInvariant);
    static readonly Regex ErrorHint=new Regex(@"\b(error|exception|fail(?:ed|ure)?|denied|cannot|unable|invalid|refused|missing|not found|mismatch|timeout|timed out|fatal|incomplete|unavailable)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    static readonly Regex PathPattern=new Regex(@"(?<![A-Za-z0-9])(?:[A-Za-z]:\\|\\\\)[^\r\n;，。\""']+",RegexOptions.CultureInvariant);
    static readonly Regex CodePattern=new Regex(@"(?i)\b(?:0x[0-9a-f]{4,8}|E(?:PERM|ACCES|CONNREFUSED|CONNRESET|TIMEDOUT)|HRESULT\s*[:=]?\s*-?\d+|(?:exit code|退出码)\s*[:=]?\s*-?\d+)\b",RegexOptions.CultureInvariant);
    static readonly Regex WebStatus=new Regex(@"(?i)(?:\bHTTP\s*|\()(\d{3})(?:\)|\b)",RegexOptions.CultureInvariant);
    static readonly string[] Technical={"Microsoft Store","Codex Doctor","Codex CLI","OpenAI Codex","Node.js","HTTP Proxy","SHA-256","SHA256","PowerShell","Windows","GitHub","OpenAI","Codex","Node","CUA","CLI","HTTP","HTTPS","JSON","GBK","UTF-8","ASAR","XCOPY","EXE","URL","IP","IPv4","IPv6","WebSocket","VPN","DNS","MCP","CDN","GB","MB","KB","winget",".env","parentModel","childModel","parentEffort","childEffort","proxyHost","projectPath","folderManagement","subagents","cuaRepair","browserRepair","officialCliUpdate","desktopUpdate","minimal","medium","xhigh","ultra","none","low","high","max"};
    static string ReplaceInsensitive(string input,string from,string to) {
        var at=input.IndexOf(from,StringComparison.OrdinalIgnoreCase);if(at<0)return input;
        var result=new StringBuilder();int start=0;
        while(at>=0){result.Append(input,start,at-start);result.Append(to);start=at+from.Length;at=input.IndexOf(from,start,StringComparison.OrdinalIgnoreCase);}
        result.Append(input,start,input.Length-start);return result.ToString();
    }
    static string MaskTechnical(string value) {
        value=PathPattern.Replace(value," [路径] ");
        value=CodePattern.Replace(value," [错误码] ");
        value=Regex.Replace(value,@"(?i)\b(?:https?|ms-windows-store)://\S+"," [链接] ");
        value=Regex.Replace(value,@"(?i)\b[a-z0-9_.-]+\.(?:exe|cmd|ps1|toml|json|mjs|log|txt)\b"," [文件] ");
        value=Regex.Replace(value,@"\b[A-Za-z0-9_]+(?:[-./:][A-Za-z0-9_]+)+\b"," [标识] ");
        value=Regex.Replace(value,@"\bCMD\b"," ",RegexOptions.IgnoreCase);
        foreach(var term in Technical)value=ReplaceInsensitive(value,term," ");
        return value;
    }
    static string Details(string original) {
        var details=new List<string>();var code=CodePattern.Match(original);
        if(code.Success)details.Add("错误码："+Regex.Replace(code.Value,@"(?i)^(?:exit code|退出码)\s*[:=]?\s*",""));
        else {var status=WebStatus.Match(original);if(status.Success)details.Add("HTTP 状态码："+status.Groups[1].Value);}
        var path=PathPattern.Match(original);if(path.Success)details.Add("路径："+path.Value.TrimEnd(' ','.',')'));
        return details.Count==0?"":" "+string.Join("；",details);
    }
    public static string TranslateLine(string input,bool errorContext=false) {
        if(String.IsNullOrEmpty(input))return input??"";
        var original=input.TrimEnd('\r');var text=original;
        if(Regex.IsMatch(text,@"^\s*(?:#< CLIXML|<Objs\b|CategoryInfo\s*:|FullyQualifiedErrorId\s*:|At .+ line:|at\s+\S+\(|\+\s|~{3,})",RegexOptions.IgnoreCase))return "异常详细输出已保留，请查看原始日志。";
        if(text.Trim()=="Notes")return "检查提示";if(text.Trim()=="Environment")return "运行环境";
        var preserved=new List<string>();
        text=PathPattern.Replace(text,m=>{var marker="〔保留"+preserved.Count+"〕";preserved.Add(m.Value);return marker;});
        text=Regex.Replace(text,@"(?i)\b(?:https?|ms-windows-store)://\S+",m=>{var marker="〔保留"+preserved.Count+"〕";preserved.Add(m.Value);return marker;});
        text=Regex.Replace(text,@"(?i)\[BROWSER INCOMPLETE\]","【浏览器未完成】");
        text=Regex.Replace(text,@"(?i)\[INCOMPLETE\]","【未完成】");
        text=Regex.Replace(text,@"(?i)\[FAIL\]","【失败】");
        text=Regex.Replace(text,@"(?i)\[ERROR\]","【错误】");
        text=Regex.Replace(text,@"(?i)\[WARN(?:ING)?\]","【警告】");
        text=Regex.Replace(text,@"(?i)\[ok\]","【正常】");text=text.Replace("[XX]","【失败】").Replace("[!!]","【提示】").Replace("[--]","【跳过】");
        text=Regex.Replace(text,@"(?i)\bOption\s+([A-Za-z][A-Za-z0-9_]*)\s+must be (?:a )?(boolean|non-empty string|string)\.?",m=>"选项 "+m.Groups[1].Value+(m.Groups[2].Value.Equals("boolean",StringComparison.OrdinalIgnoreCase)?" 必须为布尔值。":" 必须为有效文本。"));
        text=Regex.Replace(text,@"(?i)\bOption\s+([A-Za-z][A-Za-z0-9_]*)\s+contains an invalid (model id|effort)\.?",m=>"选项 "+m.Groups[1].Value+(m.Groups[2].Value.Equals("effort",StringComparison.OrdinalIgnoreCase)?" 的思考强度无效。":" 的模型 ID 无效。"));
        for(int i=0;i<Phrases.GetLength(0);i++)text=ReplaceInsensitive(text,Phrases[i,0],Phrases[i,1]);
        text=Regex.Replace(text,@"(?i)File count differs: expected (\d+), actual (\d+)\.","文件数不一致：应为 $1，实际为 $2。");
        text=Regex.Replace(text,@"(?i)Waiting for process completion \((\d+)s\)\.\.\.","等待进程完成（$1 秒）……");
        text=Regex.Replace(text,@"(?i)XCOPY failed\. Exit code: ([-\d]+|\{\d+\})\. Log:","XCOPY 失败，退出码：$1。日志：");
        text=Regex.Replace(text,@"(?i)The remote server returned an error:\s*\((\d{3})\)[^.]*\.?","服务器返回 HTTP 状态码 $1，请检查网络或访问权限。");
        text=Regex.Replace(text,@"(?i)official installer exit code\s+(-?\d+)","官方安装程序失败，退出码：$1");
        text=Regex.Replace(text,@"(?i)([A-Za-z0-9_.-]+\.exe) (missing|signature invalid)",m=>m.Groups[1].Value+(m.Groups[2].Value.Equals("missing",StringComparison.OrdinalIgnoreCase)?" 缺失":" 签名无效"));
        text=Regex.Replace(text,@"(?i)Nested or unclosed .+? block detected at version (\S+)\.?","检测到嵌套或未关闭的规则块，版本：$1");
        text=Regex.Replace(text,@"(?i).+? END marker has no matching BEGIN marker: (\S+)\.?","规则块结束标记缺少对应的开始标记，版本：$1");
        text=Regex.Replace(text,@"(?i).+? BEGIN marker has no matching END marker: (\S+)\.?","规则块开始标记缺少对应的结束标记，版本：$1");
        text=Regex.Replace(text,@"(?i).+? marker versions do not match: BEGIN=(\S+) END=(\S+)\.?","规则块的开始与结束版本不一致：$1 / $2");
        text=Regex.Replace(text,@"(?i).+? is missing required text:.*","规则缺少必需内容，请查看原始日志中的具体项目。");
        text=Regex.Replace(text,@"(?i)Required config value (\S+) was not written\.","必需的配置项 $1 未写入。");
        text=Regex.Replace(text,@"(?i)(\d+) active files [·.] ([0-9.]+ [A-Z]+) on disk","$1 个活动文件，占用磁盘 $2");
        text=Regex.Replace(text,@"(?i)(\d+) issues - rollout files are missing from the state DB; duplicate thread inventory entries found","$1 项问题：状态数据库缺少会话记录，且会话清单存在重复项");
        text=Regex.Replace(text,@"(?i)(\d+) ok\s*\|\s*(\d+) idle\s*\|\s*(\d+) notes\s*\|\s*(\d+) warn\s*\|\s*(\d+) fail(?: failed)?","$1 项正常 | $2 项空闲 | $3 项提示 | $4 项警告 | $5 项失败");
        text=Regex.Replace(text,@"(?i)(^|\s)(rollouts|updates|threads|reachability|websocket|mcp)\s+",m=>m.Groups[1].Value+new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"rollouts","会话记录："},{"updates","更新检查："},{"threads","会话清单："},{"reachability","网络可达性："},{"websocket","WebSocket："},{"mcp","MCP："}}[m.Groups[2].Value]);
        text=Regex.Replace(text,@"(?i)\bmodel\s+([A-Za-z0-9._/:-]+)\s+(?:is\s+)?(?:not available|not found|not supported|access denied)","模型 $1 当前不可用，请核对模型 ID 和账号权限");
        var check=MaskTechnical(text);
        if(EnglishSentence.IsMatch(check) || ((errorContext||ErrorHint.IsMatch(original)||original.Contains("[FAIL]")||original.Contains("[ERROR]")) && Regex.IsMatch(check,@"\b[A-Za-z]{3,}\b"))) {
            bool failure=errorContext||ErrorHint.IsMatch(original)||original.Contains("【失败】")||original.Contains("[FAIL]")||original.Contains("[ERROR]")||Regex.IsMatch(original,"未完成|失败|错误|异常|不可达|无法|未能");
            var context=Regex.Match(text,@"^(?:【[^】]+】\s*)?([\u4e00-\u9fff][^：:\r\n]{0,50})[:：]");
            return (context.Success?context.Groups[1].Value+"：":"")+(failure?"操作未完成，原因尚未识别；请查看原始日志。":"程序输出暂无中文说明；请查看原始日志。")+Details(original);
        }
        for(int i=0;i<preserved.Count;i++)text=text.Replace("〔保留"+i+"〕",preserved[i]);
        return text;
    }
    public static string TranslateText(string text,bool errorContext=false) {
        if(String.IsNullOrEmpty(text))return text??"";
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>TranslateLine(line,errorContext)));
    }
    public static string ExceptionText(Exception ex) {
        if(ex==null)return "操作未完成，原因尚未识别；请查看原始日志。";
        if(ex is UnauthorizedAccessException)return "无权访问文件或目录，请检查权限。"+Details(ex.Message);
        if(ex is PathTooLongException)return "文件路径过长，请缩短路径后重试。"+Details(ex.Message);
        var missing=ex as FileNotFoundException;if(missing!=null)return "找不到所需文件。"+(String.IsNullOrEmpty(missing.FileName)?Details(ex.Message):" 路径："+missing.FileName);
        if(ex is DirectoryNotFoundException)return "找不到所需目录。"+Details(ex.Message);
        var win=ex as Win32Exception;
        if(win!=null){switch(win.NativeErrorCode){case 2:case 3:return "系统找不到指定文件或路径。"+Details(ex.Message);case 5:return "访问被拒绝，请检查文件权限。"+Details(ex.Message);case 32:return "文件正被其他程序占用，请关闭相关程序后重试。"+Details(ex.Message);case 1223:return "已取消管理员授权，操作未开始。";case 1460:return "操作超时，请稍后重试。";}}
        var web=ex as WebException;
        if(web!=null){switch(web.Status){case WebExceptionStatus.NameResolutionFailure:return "无法解析服务器地址，请检查网络和 DNS。";case WebExceptionStatus.ConnectFailure:return "无法连接服务器，请检查网络或代理。";case WebExceptionStatus.Timeout:return "网络请求超时，请稍后重试。";case WebExceptionStatus.TrustFailure:return "无法验证服务器证书，请检查系统时间、网络或代理。";case WebExceptionStatus.ProtocolError:var response=web.Response as HttpWebResponse;if(response!=null)return "服务器返回 HTTP 状态码 "+(int)response.StatusCode+"，请检查网络或访问权限。";break;}}
        var argument=ex as ArgumentException;
        return TranslateText(argument!=null&&!String.IsNullOrEmpty(argument.ParamName)?ex.Message.Split('\n')[0].TrimEnd('\r'):ex.Message,true);
    }
    public static string ChineseLogPath(string rawPath) {return Path.Combine(Path.GetDirectoryName(rawPath),Path.GetFileNameWithoutExtension(rawPath)+".zh-CN.log");}
    public static void SaveChineseLog(string rawPath,string text) {File.WriteAllText(ChineseLogPath(rawPath),text??"",new UTF8Encoding(false));}
}
}
