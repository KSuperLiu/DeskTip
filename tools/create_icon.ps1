Add-Type -AssemblyName System.Drawing
$target = Join-Path $PSScriptRoot '../src/DeskTip/Assets/desktip.ico'
$bitmap = New-Object System.Drawing.Bitmap 256,256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$green = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#282441'))
$mint = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#C6B9FF'))
$graphics.FillEllipse($green, 8,8,240,240)
$shape = New-Object System.Drawing.Drawing2D.GraphicsPath
$shape.AddBezier(128,60, 104,60, 82,79, 82,107)
$shape.AddLine(82,107,82,150)
$shape.AddBezier(82,150, 82,163, 73,170, 64,179)
$shape.AddLine(64,179,192,179)
$shape.AddBezier(192,179, 183,170, 174,163, 174,150)
$shape.AddLine(174,150,174,107)
$shape.AddBezier(174,107, 174,79, 152,60, 128,60)
$shape.CloseFigure()
$graphics.FillPath($mint,$shape)
$graphics.FillEllipse($mint,111,187,34,20)
$smile = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#C6B9FF')),8
$graphics.DrawLine($smile,128,51,128,60)
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
