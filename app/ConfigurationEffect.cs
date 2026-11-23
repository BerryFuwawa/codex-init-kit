using System;
using System.Collections.Generic;
namespace CodexKit {
// 只在实际写入后记录；保存与当前运行实例采用配置是两个不同的结果。
internal sealed class ConfigurationEffect {
    public string id,name,saved,activation,impact,evidence;
    internal static ConfigurationEffect Create(string id,string saved){
        var e=new ConfigurationEffect{id=id,saved=saved,evidence="文件检查；未验证当前运行实例采用此变更。"};
        switch(id){
        case "config":e.name="Codex 配置（config.toml）";e.activation="新会话创建时重新读取会话默认值；已有会话和各功能开关是否热加载待验证。项目配置、配置档及会话参数可覆盖全局默认值。";e.impact="不关闭现有对话；只改文件不能改变当前回合已选的模型、权限或运行参数。";break;
        case "agents":e.name="全局规则（AGENTS.md）";e.activation="新会话／新运行重新装载规则；已有会话是否重读待验证。AGENTS.override.md、项目规则及指令优先级仍适用。";e.impact="不要求关闭桌面；已有对话保留原上下文，不能宣称已采用新规则。";break;
        case "env":e.name="持久代理（.env 与当前用户）";e.activation="官方原生 CLI 启动时读取 .env；桌面 Node 连接在下次普通图标启动时继承当前用户代理及 NODE_USE_ENV_PROXY。现有进程环境不会随保存自动更新，需要重新打开后采用。";e.impact="当前 Windows 用户下其他遵循 HTTP_PROXY、HTTPS_PROXY 的程序也会读取这些设置。原有直连排除项保留；实际 HTTP、Work 与桌面 WebSocket 分别验收。";e.evidence="文件与持久用户设置检查；未验证当前运行实例采用此变更。";break;
        case "cli":e.name="CLI 路径选择";e.activation="下一次桌面启动并创建 CLI 时核对进程实际路径；保存的用户路径还可能受机器级或启动环境覆盖。";e.impact="现有 CLI 进程不会换成另一个程序。需要重新打开桌面时先保存全部对话，并另行确认。";break;
        case "guard":e.name="启动保护";e.activation="登录任务在下次符合条件的用户登录时执行；仅保存程序不代表任务注册成功，具体看阶段结果。";e.impact="不立即启动维护或关闭当前对话；不会令正在运行的 CLI 自动切换。";break;
        case "desktop":e.name="桌面应用组件";e.activation="下一次打开桌面后使用新组件；是否等待系统重启，以商店和组件检查结果为准。";e.impact="更新替换前需确认退出桌面，会中断所有正在进行的对话。";break;
        case "official":e.name="官方独立 CLI 组件";e.activation="下一次以该入口创建 CLI 进程时采用；单独更新组件不改变所选 CLI 模式。";e.impact="已有进程不热替换；已保留旧入口，当前实例版本未验证。";break;
        case "cua":e.name="电脑操作组件（CUA）";e.activation="新建电脑操作组件进程时读取；是否复用旧组件待验证，真实操作另行验收。";e.impact="替换使用中的组件前需要确认退出，不能仅凭文件完整性宣称当前连接恢复。";break;
        case "playwright":e.name="Playwright CLI";e.activation="新会话可通过 Playwright CLI 技能使用该工具。";e.impact="独立浏览器可直接使用。使用已有 Edge 或 Chrome 标签页需要在浏览器内确认连接。";break;
        case "chrome-devtools":e.name="Chrome DevTools MCP";e.activation="新会话加载 Chrome DevTools MCP。";e.impact="首次调用浏览器工具时按需启动独立 Chrome。";break;
        case "rtk":e.name="RTK";e.activation="下一次 RTK 命令读取组件及统计设置；新增 PATH 由取得新环境的进程采用。";e.impact="现有终端环境不会自动更新；不重写现有钩子。";break;
        case "folders":e.name="工作目录";e.activation="新建目录立即可用；工作区默认路径随 config.toml 的读取范围生效。";e.impact="只补齐缺失目录，不移动现有文件或会话。";break;
        default:throw new ArgumentException("未知配置变更："+id);
        }
        return e;
    }
    internal static void Record(List<ConfigurationEffect> list,string id,string saved){var e=list.Find(x=>x.id==id);if(e==null)list.Add(Create(id,saved));else e.saved+="；"+saved;}
    internal static ConfigurationEffect Read(Dictionary<string,object> d){return new ConfigurationEffect{id=Core.Value(d,"id",""),name=Core.Value(d,"name",""),saved=Core.Value(d,"saved",""),activation=Core.Value(d,"activation","待验证"),impact=Core.Value(d,"impact","待验证"),evidence=Core.Value(d,"evidence","未提供核验记录")};}
    internal string Detail{get{return "已保存："+saved+"\n生效方式："+activation+"\n影响范围："+impact+"\n核验程度："+evidence;}}
}
}
