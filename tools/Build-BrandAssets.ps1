[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root=Split-Path $PSScriptRoot -Parent
$assets=Join-Path $root 'assets'
[void][IO.Directory]::CreateDirectory($assets)
function Brush([string]$Color) { [Windows.Media.BrushConverter]::new().ConvertFromString($Color) }
function Stroke([string]$Color,[double]$Width) {
    $pen=[Windows.Media.Pen]::new((Brush $Color),$Width)
    $pen.StartLineCap=[Windows.Media.PenLineCap]::Round
    $pen.EndLineCap=[Windows.Media.PenLineCap]::Round
    $pen.LineJoin=[Windows.Media.PenLineJoin]::Round
    return $pen
}
function Render-Logo([int]$Size) {
    $visual=[Windows.Media.DrawingVisual]::new()
    $draw=$visual.RenderOpen()
    $draw.PushTransform([Windows.Media.ScaleTransform]::new($Size/256.0,$Size/256.0))
    $draw.DrawRoundedRectangle((Brush '#202B3D'),$null,[Windows.Rect]::new(0,0,256,256),62,62)
    $draw.DrawGeometry($null,(Stroke '#ADCBF1' 17),[Windows.Media.Geometry]::Parse('M143 202 C96 212 48 180 46 135 C43 91 76 54 120 51 C147 49 171 59 187 78 M187 52 L189 74 Q190 82 182 81 L160 78'))
    $draw.DrawGeometry($null,(Stroke '#F9F4E9' 14),[Windows.Media.Geometry]::Parse('M89 99 L106 113 Q111 117 106 121 L90 135 M147 114 L147 116'))
    $draw.DrawGeometry($null,(Stroke '#F9F4E9' 11),[Windows.Media.Geometry]::Parse('M115 147 Q131 160 147 145'))
    $draw.DrawEllipse((Brush '#A5DFC8'),$null,[Windows.Point]::new(185,184),36,36)
    $draw.DrawGeometry($null,(Stroke '#284A48' 11),[Windows.Media.Geometry]::Parse('M168 183 L178 193 Q181 196 184 192 L203 172'))
    $draw.Pop();$draw.Close()
    $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($Size,$Size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream=[IO.MemoryStream]::new()
    try{$encoder.Save($stream);return ,$stream.ToArray()}finally{$stream.Dispose()}
}
[IO.File]::WriteAllBytes((Join-Path $assets 'logo.png'),(Render-Logo 512))
$sizes=@(16,24,32,48,64,128,256)
$frames=@(foreach($size in $sizes){[pscustomobject]@{Size=$size;Bytes=(Render-Logo $size)}})
$stream=[IO.File]::Create((Join-Path $assets 'app.ico'))
$writer=[IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
    $offset=6+16*$frames.Count
    foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
    foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
} finally {$writer.Dispose();$stream.Dispose()}
Write-Output 'Generated logo.png and app.ico (16-256 px) from vector geometry.'
