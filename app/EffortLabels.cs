using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace CodexKit {
public sealed class EffortLabels:IValueConverter {
    static readonly Dictionary<string,string> Labels=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"none","无"},{"minimal","最低"},{"low","低"},{"medium","中"},{"high","高"},{"xhigh","很高"},{"max","最高"},{"ultra","极高"}};
    public static string Display(string value){
        string label;
        if(value==null||!Labels.TryGetValue(value,out label))return value??"";
        return label+"（"+Char.ToUpperInvariant(value[0])+value.Substring(1).ToLowerInvariant()+"）";
    }
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return Display(System.Convert.ToString(value));}
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
}
}
