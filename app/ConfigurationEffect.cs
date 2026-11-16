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
        case "env":e.name="HTTP 代理（.env）";e.activation="由读取 .env 的新 CLI／组件进程采用；现有进程环境不会随文件自动改变。桌面主进程的 WebSocket 环境需单独准备与验证。";e.impact="不改变现有连接；新对话若复用旧组件，仍可能沿用旧环境。既有进程环境与启动方式的优先关系需核对。";break;
        case "cli":e.name="CLI 路径选择";e.activation="下一次桌面启动并创建 CLI 时核对进程实际路径；保存的用户路径还可能受机器级或启动环境覆盖。";e.impact="现有 CLI 进程不会换成另一个程序。需要重新打开桌面时先保存全部对话，并另行确认。";break;
        case "browser":e.name="浏览器入口";e.activation="新浏览器组件进程启动时读取入口。可先建立新连接；桌面是否复用旧组件待验证，需要退出时另行确认。";e.impact="已加载入口的组件不热替换；不关闭 Chrome 标签页。真实浏览器操作仍需单独验收。";break;
        case "websocket":e.name="WebSocket 启动设置";e.activation="在方便退出时，通过本工具的“修复并启动 Codex”创建带代理环境的新桌面进程，再验收 Dot 连接。";e.impact="只保存本工具设置，不向正在运行的桌面注入环境。重新打开桌面会影响全部对话，必须另行确认。";break;
        case "guard":e.name="启动保护";e.activation="登录任务在下次符合条件的用户登录时执行；仅保存程序不代表任务注册成功，具体看阶段结果。";e.impact="不立即启动维护或关闭当前对话；不会令正在运行的 CLI 自动切换。";break;
        case "desktop":e.name="桌面应用组件";e.activation="下一次打开桌面后使用新组件；是否等待系统重启，以商店和组件检查结果为准。";e.impact="更新替换前需确认退出桌面，会中断所有正在进行的对话。";break;
        case "official":e.name="官方独立 CLI 组件";e.activation="下一次以该入口创建 CLI 进程时采用；单独更新组件不改变所选 CLI 模式。";e.impact="已有进程不热替换；已保留旧入口，当前实例版本未验证。";break;
        case "cua":e.name="电脑操作组件（CUA）";e.activation="新建电脑操作组件进程时读取；是否复用旧组件待验证，真实操作另行验收。";e.impact="替换使用中的组件前需要确认退出，不能仅凭文件完整性宣称当前连接恢复。";break;
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
