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
    $draw.DrawRoundedRectangle((Brush '#151B27'),$null,[Windows.Rect]::new(0,0,256,256),52,52)
    $draw.DrawGeometry($null,(Stroke '#9EBCFF' 20),[Windows.Media.Geometry]::Parse('M177 66 C146 39 94 42 65 74 C35 108 39 160 72 188 C96 209 127 214 154 205'))
    $draw.DrawGeometry((Brush '#9EBCFF'),$null,[Windows.Media.Geometry]::Parse('M158 45 L199 69 L165 94 Z'))
    $draw.DrawGeometry($null,(Stroke '#EDF2FC' 13),[Windows.Media.Geometry]::Parse('M94 99 L118 123 L94 147 M135 148 L154 148'))
    $draw.DrawGeometry($null,(Stroke '#70DFC0' 16),[Windows.Media.Geometry]::Parse('M166 179 L184 197 L216 162'))
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
