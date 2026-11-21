using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace CodexKit {
internal static class UiIcons {
    static readonly Dictionary<string,Geometry> cache=new Dictionary<string,Geometry>();
    const string Play="M5,5.7 C5,3.8 7,2.7 8.6,3.7 L20.1,10.3 C21.4,11.1 21.4,12.9 20.1,13.7 L8.6,20.3 C7,21.3 5,20.2 5,18.3 Z";
    const string Home="M3.2,10 L10.2,4.1 Q12,2.6 13.8,4.1 L20.8,10 M5.2,9 V17.6 Q5.2,20.5 8.1,20.5 H10 V16 Q10,13.7 12.1,13.7 Q14.2,13.7 14.2,16 V20.5 H16 Q18.9,20.5 18.9,17.6 V9";
    const string Link="M8.4,10.4 L6.2,12.6 A4.1,4.1 0 0 0 12,18.4 L14.3,16.1 M15.6,13.6 L17.8,11.4 A4.1,4.1 0 0 0 12,5.6 L9.7,7.9 M9.4,14.6 L14.6,9.4";
    const string Terminal="M6.7,4.6 H17.3 Q21,4.6 21,8.4 V15.6 Q21,19.4 17.3,19.4 H6.7 Q3,19.4 3,15.6 V8.4 Q3,4.6 6.7,4.6 Z M7.1,8.9 L9.6,11.2 Q10.1,11.7 9.6,12.2 L7.1,14.5 M13,14.5 H16.7";
    const string History="M3.7,9.3 A8.4,8.4 0 1 1 4.2,16.2 M3,5.6 V9.4 Q3,10.5 4.1,10.5 H7.9 M12.1,7.7 V11.8 Q12.1,12.3 12.6,12.6 L15.4,14.5";
    const string Download="M12,3.1 V14.2 M7.6,10.8 L11.3,14.7 Q12,15.4 12.7,14.7 L16.4,10.8 M3.5,15.3 V17.4 Q3.5,20.6 6.7,20.6 H17.3 Q20.5,20.6 20.5,17.4 V15.3";
    const string Note="M8.3,3.1 H14.1 Q15.1,3.1 15.9,3.9 L18.9,6.9 Q19.8,7.8 19.8,9 V17.7 Q19.8,20.9 16.6,20.9 H8.3 Q4.8,20.9 4.8,17.5 V6.5 Q4.8,3.1 8.3,3.1 Z M14,3.8 V6.6 Q14,8.6 15.9,8.6 H18.9 M8.6,12 H15.7 M8.6,15.8 H13.9";
    const string Close="M5.6,5.6 L18.4,18.4 M18.4,5.6 L5.6,18.4";
    const string Back="M19.5,12 H5 M11.3,5.7 L5.6,11.3 Q4.9,12 5.6,12.7 L11.3,18.3";
    const string Forward="M4.5,12 H19 M12.7,5.7 L18.4,11.3 Q19.1,12 18.4,12.7 L12.7,18.3";
    const string Switch="M3.5,8.2 H18.8 Q20.1,8.2 19.1,7.2 L15.9,4.1 M20.5,15.8 H5.2 Q3.9,15.8 4.9,16.8 L8.1,19.9";
    const string Copy="M9.1,6.5 V6 Q9.1,3.2 12,3.2 H17.7 Q20.4,3.2 20.4,6 V13.2 Q20.4,16 17.6,16 H17 M6.4,8 H12.6 Q15.4,8 15.4,10.9 V18 Q15.4,20.9 12.6,20.9 H6.4 Q3.6,20.9 3.6,18 V10.9 Q3.6,8 6.4,8 Z";
    const string Bin="M4.2,7.5 H19.8 M9.1,5.4 Q9.1,3.6 11,3.6 H13 Q14.9,3.6 14.9,5.4 M6,8 L6.8,17.9 Q7,20.7 9.9,20.7 H14.1 Q17,20.7 17.2,17.9 L18,8 M10,11.1 L10.3,16.9 M14,11.1 L13.7,16.9";
    const string Refresh="M4.1,10.3 A8,8 0 0 1 17.8,6.4 L19,8 M19.5,4.7 V8.3 Q19.5,9.3 18.5,9.3 H15 M19.9,13.7 A8,8 0 0 1 6.2,17.6 L5,16 M4.5,19.3 V15.7 Q4.5,14.7 5.5,14.7 H9";
    const string Search="M16.9,10.5 A6.6,6.6 0 1 1 3.7,10.5 A6.6,6.6 0 1 1 16.9,10.5 Z M15,15.2 L20.6,20.8";
    const string Wrench="M16.1,3.5 A5.8,5.8 0 0 0 9.2,11 L4.1,16.1 A2.7,2.7 0 0 0 7.9,19.9 L13,14.8 A5.8,5.8 0 0 0 20.5,7.9 L17.4,11 Q16.8,11.6 16,11.3 L13.5,10.5 L12.7,8 Q12.4,7.2 13,6.6 Z";
    const string Save="M6.4,3.8 H15.3 Q16.3,3.8 17.1,4.6 L19.6,7.1 Q20.4,7.9 20.4,9 V17.6 Q20.4,20.3 17.6,20.3 H6.4 Q3.6,20.3 3.6,17.6 V6.5 Q3.6,3.8 6.4,3.8 Z M8,4.4 V7.5 Q8,9.3 9.8,9.3 H13.2 Q15,9.3 15,7.5 V4.4 M7.8,19.6 V15.6 Q7.8,13.6 9.8,13.6 H14.2 Q16.2,13.6 16.2,15.6 V19.6";
    const string Shield="M12,3 Q15.2,5.2 19.2,5.8 Q20.5,6 20.3,7.5 L19.8,12.1 Q19.1,17.7 13,20.9 Q12,21.4 11,20.9 Q4.9,17.7 4.2,12.1 L3.7,7.5 Q3.5,6 4.8,5.8 Q8.8,5.2 12,3 Z M8.4,11.7 L10.8,14.1 Q11.2,14.5 11.6,14.1 L16.1,9.6";
    const string Memory="M8.4,3.6 H15.6 Q19.7,3.6 19.7,7.8 V16.2 Q19.7,20.4 15.6,20.4 H8.4 Q4.3,20.4 4.3,16.2 V7.8 Q4.3,3.6 8.4,3.6 Z M8.2,8 H15.8 M8.2,11.8 H12.5 M8.2,15.6 H14";
    const string Cube="M10.8,3.5 Q12,2.8 13.2,3.5 L19.3,6.9 Q20.5,7.6 20.5,9 V15.2 Q20.5,16.6 19.3,17.3 L13.2,20.7 Q12,21.4 10.8,20.7 L4.7,17.3 Q3.5,16.6 3.5,15.2 V9 Q3.5,7.6 4.7,6.9 Z M4.6,8 L11.2,11.7 Q12,12.2 12.8,11.7 L19.4,8 M12,12 V20.6";
    const string Tiles="M5.3,3.4 H8.2 Q10.1,3.4 10.1,5.3 V8.2 Q10.1,10.1 8.2,10.1 H5.3 Q3.4,10.1 3.4,8.2 V5.3 Q3.4,3.4 5.3,3.4 Z M15.8,3.4 H18.7 Q20.6,3.4 20.6,5.3 V8.2 Q20.6,10.1 18.7,10.1 H15.8 Q13.9,10.1 13.9,8.2 V5.3 Q13.9,3.4 15.8,3.4 Z M5.3,13.9 H8.2 Q10.1,13.9 10.1,15.8 V18.7 Q10.1,20.6 8.2,20.6 H5.3 Q3.4,20.6 3.4,18.7 V15.8 Q3.4,13.9 5.3,13.9 Z M15.8,13.9 H18.7 Q20.6,13.9 20.6,15.8 V18.7 Q20.6,20.6 18.7,20.6 H15.8 Q13.9,20.6 13.9,18.7 V15.8 Q13.9,13.9 15.8,13.9 Z";
    const string Globe="M20.7,12 A8.7,8.7 0 1 1 3.3,12 A8.7,8.7 0 1 1 20.7,12 Z M3.8,12 H20.2 M12,3.5 C7.4,7.6 7.4,16.4 12,20.5 C16.6,16.4 16.6,7.6 12,3.5 Z";
    const string Browser="M6.7,4.5 H17.3 Q21,4.5 21,8.2 V15.8 Q21,19.5 17.3,19.5 H6.7 Q3,19.5 3,15.8 V8.2 Q3,4.5 6.7,4.5 Z M3.8,9.8 H20.2 M6.8,7.2 H7.2 M10,7.2 H10.4";
    const string Folder="M3.3,7.1 Q3.3,4.5 6,4.5 H9.2 Q10.3,4.5 11,5.3 L12.5,7 H17.7 Q20.7,7 20.7,10 V16.5 Q20.7,19.5 17.7,19.5 H6.3 Q3.3,19.5 3.3,16.5 Z M4,10.3 H19.9";
    const string Monitor="M6.6,4 H17.4 Q21,4 21,7.6 V13.6 Q21,17.1 17.4,17.1 H6.6 Q3,17.1 3,13.6 V7.6 Q3,4 6.6,4 Z M12,17.2 V20.4 M8.2,20.5 H15.8";
    const string Sliders="M3.4,7.6 H6.6 M13.1,7.6 H20.6 M3.4,16.4 H10.9 M17.4,16.4 H20.6 M12.8,7.6 A2.9,2.9 0 1 1 7,7.6 A2.9,2.9 0 1 1 12.8,7.6 Z M17,16.4 A2.9,2.9 0 1 1 11.2,16.4 A2.9,2.9 0 1 1 17,16.4 Z";
    const string Check="M3.8,12.5 L8.5,17.3 Q9.3,18.1 10.1,17.3 L20.2,6.8";
    const string Hint="M6.2,17.2 A8.3,8.3 0 1 1 10.4,20 L6.1,21.1 Q5.2,21.3 5.4,20.4 Z M12,10.9 V15.7 M12,7.5 V7.8";
    const string External="M12.5,4 H18.7 Q20,4 20,5.3 V11.5 M19.4,4.6 L10.1,13.9 M9.3,6 H7.1 Q3.8,6 3.8,9.3 V16.8 Q3.8,20.1 7.1,20.1 H14.6 Q17.9,20.1 17.9,16.8 V14.7";

    internal static Geometry For(string text,bool fallback=false){
        string data=PathData(text??"");
        if(data==null){
            if(!fallback)return null;
            data=Note;
        }
        Geometry icon;
        if(!cache.TryGetValue(data,out icon)){
            icon=Geometry.Parse(data);
            icon.Freeze();
            cache[data]=icon;
        }
        return icon;
    }

    static string PathData(string key){
        switch(key){
            case "概览":return Home;
            case "初始化":return Play;
            case "提示":return Hint;
            case "连接与修复":case "连接状态":return Link;
            case "CLI 管理":return Terminal;
            case "备份与回滚":return History;
            case "更新与下载":return Download;
            case "日志":return Note;
            case "远控转发":case "开启远控转发":return Switch;
        }
        if(key.Contains("取消")||key=="关闭")return Close;
        if(key.Contains("上一步")||key.Contains("返回"))return Back;
        if(key.Contains("下一步")||key.Contains("查看变更"))return Forward;
        if(key.Contains("切换"))return Switch;
        if(key.Contains("复制"))return Copy;
        if(key.Contains("删除")||key.Contains("卸载")||key.Contains("清理"))return Bin;
        if(key.Contains("打开")&&(key.Contains("目录")||key.Contains("文件夹")))return Folder;
        if(key.Contains("发布页"))return External;
        if(key.Contains("刷新")||key.Contains("重试")||key.Contains("重新"))return Refresh;
        if(key.Contains("回到最新"))return Download;
        if(key.Contains("回滚")||key.Contains("恢复"))return History;
        if(key.Contains("检查")||key.Contains("诊断")||key.Contains("检测"))return Search;
        if(key.Contains("修复")||key.Contains("维护"))return Wrench;
        if(key.StartsWith("保存",StringComparison.Ordinal))return Save;
        if(key.Contains("更新")||key.Contains("下载")||key.Contains("版本"))return Download;
        if(key.Contains("保护")||key.Contains("规则"))return Shield;
        if(key.Contains("启动")||key.Contains("开始")||key.Contains("就绪"))return Play;
        if(key.Contains("记忆"))return Memory;
        if(key.Contains("模型"))return Cube;
        if(key.Contains("功能")||key=="常用操作")return Tiles;
        if(key.Contains("浏览器"))return Browser;
        if(key.Contains("网络"))return Globe;
        if(key.Contains("连接"))return Link;
        if(key.Contains("工作")||key.Contains("目录")||key.Contains("文件夹")||key.Contains("备份"))return Folder;
        if(key.Contains("环境")||key.Contains("电脑")||key.Contains("组件"))return Monitor;
        if(key.Contains("代理"))return Sliders;
        if(key.Contains("CLI"))return Terminal;
        if(key.Contains("确认")||key=="知道了"||key.StartsWith("先处理",StringComparison.Ordinal))return Check;
        if(key.Contains("日志")||key.Contains("详情"))return Note;
        return null;
    }
}
}
