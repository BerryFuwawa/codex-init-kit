using System;using System.Runtime.InteropServices;using System.Threading.Tasks;
namespace CodexKit {
internal sealed class ClipboardCopyResult {internal bool Success;internal string Message;}
internal static class ClipboardCopy {
    internal static async Task<ClipboardCopyResult> Copy(string text,Action<string> write,Func<int,Task> delay){
        for(int attempt=0;attempt<8;attempt++){try{write(text??"");return new ClipboardCopyResult{Success=true,Message="已复制"};}
        catch(ExternalException ex){if(unchecked((uint)ex.ErrorCode)!=0x800401D0)return new ClipboardCopyResult{Message="复制未完成（错误码 0x"+unchecked((uint)ex.ErrorCode).ToString("X8")+"），请稍后重试。"};if(attempt==7)return new ClipboardCopyResult{Message="剪贴板暂时被其他程序占用，请稍后再次点击复制。"};}
        await delay(120); }
        return new ClipboardCopyResult{Message="复制未完成，请稍后重试。"};
    }
}
}
