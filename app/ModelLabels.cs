using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace CodexKit {
public sealed class ModelLabels : IValueConverter {
    static readonly Regex GptModel = new Regex(@"^gpt-[0-9][a-z0-9.]*(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant);

    public static string Display(string value) {
        if (String.IsNullOrEmpty(value) || !GptModel.IsMatch(value)) return value ?? "";
        var parts = value.Split('-');
        parts[0] = "GPT";
        for (int i = 2; i < parts.Length; i++)
            parts[i] = Char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        return String.Join(" ", parts);
    }

    public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return Display(System.Convert.ToString(value));}
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
}
}
