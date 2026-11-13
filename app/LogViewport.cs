using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CodexKit {
// Keep text/caret layout changes separate from the reader's scrolling intent.
internal sealed class LogViewport {
    readonly TextBox view;
    DispatcherOperation pending;
    bool attached,restoring;
    double vertical,horizontal;
    internal bool Following {get;private set;}

    internal LogViewport(TextBox textBox) {
        view=textBox;Following=true;
        view.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(Scrolled));
        view.AddHandler(UIElement.PreviewMouseWheelEvent,new System.Windows.Input.MouseWheelEventHandler((s,e)=>BeforeInput()),true);
        view.PreviewMouseDown+=(s,e)=>BeforeInput();
        view.PreviewKeyDown+=(s,e)=>BeforeInput();
        view.Loaded+=(s,e)=>{if(attached)Schedule();};
    }
    bool AtBottom(){return view.ExtentHeight-view.ViewportHeight-view.VerticalOffset<=2;}
    void Scrolled(object sender,ScrollChangedEventArgs e) {
        if(!attached||restoring||pending!=null||e.ExtentHeightChange!=0||e.ExtentWidthChange!=0||e.ViewportHeightChange!=0||e.ViewportWidthChange!=0)return;
        if(e.VerticalChange==0&&e.HorizontalChange==0)return;
        vertical=view.VerticalOffset;horizontal=view.HorizontalOffset;Following=AtBottom();
    }
    void Cancel(){if(pending!=null){pending.Abort();pending=null;}}
    void BeforeInput(){if(pending!=null){Cancel();Restore();}}
    internal void Detach(){Cancel();attached=false;}
    internal void Attach(){attached=true;Schedule();}
    internal void Reset(){Cancel();Following=true;vertical=horizontal=0;restoring=true;try{view.Clear();view.Select(0,0);}finally{restoring=false;}Schedule();}
    internal void SetText(string text) {
        if(view.Text==text)return;
        int start=view.SelectionStart,length=view.SelectionLength;
        // Schedule first: TextBox can move its caret/viewport while processing text.
        Schedule();restoring=true;
        try {
            if(text.StartsWith(view.Text,StringComparison.Ordinal))view.AppendText(text.Substring(view.Text.Length));
            else view.Text=text;
            start=Math.Min(start,text.Length);view.Select(start,Math.Min(length,text.Length-start));
        }finally{restoring=false;}
    }
    internal void Latest(){Following=true;Schedule();}
    void Schedule(){if(attached&&pending==null)pending=view.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{pending=null;Restore();}));}
    void Restore() {
        if(!attached)return;
        restoring=true;
        try {
            view.UpdateLayout();
            view.ScrollToHorizontalOffset(horizontal);
            view.ScrollToVerticalOffset(Following?view.ExtentHeight:vertical);
            view.UpdateLayout();
            vertical=view.VerticalOffset;horizontal=view.HorizontalOffset;
        }finally{restoring=false;}
    }
}
}
