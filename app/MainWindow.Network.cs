using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CodexKit {
public partial class MainWindow {
    readonly ActivityIndicator operationActivity=new ActivityIndicator{Width=15,Height=15,Margin=new Thickness(0,0,7,0)};
    Dictionary<string,object> modelNetworkReport;
    void ConnectionOverview(){
        subtitle.Text=preview?"设计预览 · 检查已保存配置与连接状态。":"检查已保存配置与连接状态。";
        var proxy=Card("代理配置","选择代理预设或输入自定义 IP 和端口。该地址同时供 Codex 和远控转发使用。");proxy.Children.Add(proxyEnabled);proxy.Children.Add(ProxyPortRow());proxy.Children.Add(proxyPortNotice);proxy.Children.Add(T("代理 IP（留空为 127.0.0.1）",12,MutedBrush));proxy.Children.Add(proxyHost);Row(proxy,Button("保存代理配置",async()=>await Run("proxy","保存代理配置",true),true));
        proxy.Children.Add(T("同时保存到 Codex 的 .env 和 Windows 当前用户代理。保存工作并重新打开 Codex 后采用；现有进程与连接保持原环境。",12,MutedBrush));
        var remote=Card("远控转发","开启时直接使用上方当前选中的代理 IP 和端口。");
        bool configured=false;string relayState="已关闭";
        if(!preview){
            try{relayState=RemoteRelayService.Describe(out configured);}
            catch(Exception ex){relayState="无法读取状态："+ex.Message;}
        }
        Pair(remote,"转发状态",relayState);
        remote.Children.Add(T("开启需要管理员授权。Windows 服务随系统启动，退出工具后继续运行。使用系统 hosts 解析 chatgpt.com 的程序都会连到本机 127.0.0.2:443。转发开启期间，保存新代理并完成授权后，新连接使用新地址。",12,MutedBrush));
        Row(remote,configured
            ? Button("关闭远控转发",async()=>await Run("relay-disable","关闭远控转发",true,null,"移除工具添加的 chatgpt.com 映射并卸载转发服务。正在使用转发的连接会断开。"),false)
            : Button("开启远控转发",async()=>await Run("relay-enable","开启远控转发",true),true));
        var model=Card("已保存配置的模型检查","检查官方 CLI 实际读取的配置，界面中尚未保存的地址不参与探针。");
        Pair(model,"检查结果",Core.Value(modelNetworkReport,"modelState","尚未检查"));if(modelNetworkReport!=null){Pair(model,"配置文件",Core.Value(modelNetworkReport,"configurationPath",""));Pair(model,"代理来源",Core.Value(modelNetworkReport,"savedConfiguration",""));Pair(model,"运行时策略",Core.Value(modelNetworkReport,"proxyPolicy",""));}
        Row(model,Button("检查已保存的模型线路",async()=>await CheckModelNetwork(),true),Button("查看检查日志",()=>Navigate("日志"),false));
        var scope=Card("浏览器与 Work","这些连接分别使用各自的网络路径。");
        Pair(scope,"Work 本机连接","原生 exec-server 的 WSS／Noise 可读取 CLI 的 .env；电脑在线状态仍需在 Work 中验收。");
        Pair(scope,"桌面应用与浏览器","桌面 HTTP 按系统网络设置；Node WebSocket 可使用已保存的用户代理。桌面与浏览器实际连接仍需分别验收。");
        Row(scope,Button("检查浏览器组件",()=>{repairTab=1;Navigate("连接与修复");},false));scope.Children.Add(T("电脑名称与允许控制在 Codex/Work 中直接设置。",12,MutedBrush));
        var diagnostic=Card("诊断助手","CLI 或其他组件异常时，可进一步查看诊断建议。");diagnostic.Children.Add(T(diagnosticAdvice.Length>0?diagnosticAdvice:"诊断只读取状态，不自动执行修复。",12,MutedBrush));Row(diagnostic,Button("开始诊断",async()=>await Run("doctor","诊断助手",false),false));
    }
    async Task CheckModelNetwork(){var cli=Core.Value(status,"currentCliPath","");if(String.IsNullOrWhiteSpace(cli)){var install=Core.Value(status,"desktopInstallLocation","");if(install.Length>0)cli=Path.Combine(install,"app","resources","codex.exe");}await Run("model-network-check","检查已保存的模型线路",false,new Dictionary<string,object>{{"modelCliPath",cli}});}
}
}
