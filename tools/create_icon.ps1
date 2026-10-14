Add-Type -AssemblyName System.Drawing
$target = Join-Path $PSScriptRoot '../src/DeskTip/Assets/desktip.ico'
$bitmap = New-Object System.Drawing.Bitmap 256,256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#235B47'))
$mint = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#DDF7EA'))
$graphics.FillEllipse($green, 8,8,240,240)
$shape = New-Object System.Drawing.Drawing2D.GraphicsPath
$shape.AddBezier(128,48, 119,77, 67,112, 67,152)
$shape.AddBezier(67,152, 67,222, 189,222, 189,152)
$shape.AddBezier(189,152, 189,112, 137,77, 128,48)
$shape.CloseFigure()
$graphics.FillPath($mint,$shape)
$graphics.FillEllipse($green,98,142,10,13)
$graphics.FillEllipse($green,148,142,10,13)
$smile = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#235B47')),5
$graphics.DrawArc($smile,113,149,30,26,15,150)
$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = $stream.ToArray()
$file = [System.IO.File]::Create($target)
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22)
$writer.Write($bytes)
$writer.Dispose(); $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $green.Dispose(); $mint.Dispose(); $shape.Dispose(); $smile.Dispose()
Write-Output 'DeskTip icon created.'
