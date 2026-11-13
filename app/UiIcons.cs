using System;using System.Collections.Generic;using System.Windows.Media;
namespace CodexKit {
internal static class UiIcons {
    static readonly Dictionary<string,Geometry> cache=new Dictionary<string,Geometry>();
    internal static Geometry For(string text,bool fallback=false){
        var key=String.IsNullOrEmpty(text)?"":text;
        string data=null;
        if(key.Contains("取消")||key=="关闭")data="M5,5 L19,19 M19,5 L5,19";
        else if(key.Contains("上一步")||key.Contains("返回"))data="M14,5 L7,12 L14,19 M7,12 L21,12";
        else if(key.Contains("下一步")||key.Contains("查看变更"))data="M10,5 L17,12 L10,19 M3,12 L17,12";
        else if(key.Contains("切换"))data="M3,7 L20,7 L16,3 M20,17 L3,17 L7,21";
        else if(key.Contains("复制"))data="M8,8 L21,8 L21,21 L8,21 Z M16,8 L16,3 L3,3 L3,16 L8,16";
        else if(key.Contains("删除")||key.Contains("卸载"))data="M4,6 L20,6 M9,3 L15,3 M6,6 L7,21 L17,21 L18,6 M10,10 L10,17 M14,10 L14,17";
        else if(key.Contains("回滚")||key.Contains("恢复"))data="M3,4 L3,10 L9,10 M3,10 C9,0 22,7 19,16 C17,23 8,22 5,18";
        else if(key.Contains("刷新")||key.Contains("重试")||key.Contains("重新"))data="M4,9 C6,2 17,2 20,8 M20,3 L20,9 L14,9 M20,15 C18,22 7,22 4,16 M4,21 L4,15 L10,15";
        else if(key.Contains("检查")||key.Contains("诊断"))data="M16,10 A6,6 0 1 1 4,10 A6,6 0 1 1 16,10 M15,15 L21,21";
        else if(key.Contains("修复"))data="M14,4 C19,1 23,7 19,11 L16,11 L7,21 L3,17 L12,8 L12,5 M14,4 L15,8 L19,9";
        else if(!key.Contains("保存")&&(key.Contains("更新")||key.Contains("下载")||key.Contains("版本")))data="M12,3 L12,16 M7,11 L12,16 L17,11 M4,17 L4,21 L20,21 L20,17";
        else if(key.Contains("保存"))data="M4,3 L17,3 L21,7 L21,21 L4,21 Z M8,3 L8,10 L16,10 L16,3 M8,21 L8,15 L17,15 L17,21";
        else if(key.Contains("最新日志"))data="M12,3 L12,16 M7,11 L12,16 L17,11 M5,21 L19,21";
        else if(key.Contains("启动")||key.Contains("开始")||key.Contains("就绪")||key=="初始化")data="M7,3 L21,12 L7,21 Z";
        else if(key.Contains("保护")||key.Contains("规则"))data="M12,2 L21,6 L20,14 C19,18 15,21 12,22 C9,21 5,18 4,14 L3,6 Z M8,12 L11,15 L16,9";
        else if(key.Contains("记忆"))data="M5,5 L19,5 L19,19 L5,19 Z M9,9 L15,9 L15,15 L9,15 Z M2,8 L5,8 M2,16 L5,16 M19,8 L22,8 M19,16 L22,16";
        else if(key.Contains("模型"))data="M12,2 L21,7 L21,17 L12,22 L3,17 L3,7 Z M3,7 L12,12 L21,7 M12,12 L12,22";
        else if(key.Contains("功能")||key=="常用操作")data="M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z";
        else if(key.Contains("连接")||key.Contains("网络")||key.Contains("浏览器"))data="M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M3,12 L21,12 M12,3 C6,8 6,16 12,21 C18,16 18,8 12,3";
        else if(key.Contains("工作")||key.Contains("目录")||key.Contains("文件夹")||key.Contains("打开备份"))data="M3,6 L9,6 L11,9 L21,9 L21,21 L3,21 Z M3,6 L3,3 L10,3 L13,6 L21,6 L21,9";
        else if(key.Contains("环境")||key.Contains("概览")||key.Contains("电脑")||key.Contains("组件"))data="M3,4 L21,4 L21,17 L3,17 Z M8,22 L16,22 M12,17 L12,22";
        else if(key.Contains("代理"))data="M3,6 L21,6 M3,18 L21,18 M8,3 L8,9 M16,15 L16,21";
        else if(key.Contains("CLI"))data="M3,4 L21,4 L21,20 L3,20 Z M6,9 L10,12 L6,15 M13,16 L18,16";
        else if(key.Contains("确认")||key=="知道了")data="M4,12 L10,18 L21,6";
        else if(fallback||key.Contains("日志")||key.Contains("详情"))data="M5,3 L16,3 L20,7 L20,21 L5,21 Z M8,9 L16,9 M8,13 L16,13 M8,17 L14,17";
        if(data==null)return null;Geometry icon;if(!cache.TryGetValue(data,out icon)){icon=Geometry.Parse(data);icon.Freeze();cache[data]=icon;}return icon;
    }
}
}
