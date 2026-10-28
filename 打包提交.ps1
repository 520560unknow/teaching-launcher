# 课堂智答 · 一键打包提交脚本
# 用法：把录制好的视频命名为 演示视频.mp4 放到 d:\app\start_mode\
#       然后运行： powershell -ExecutionPolicy Bypass -File "d:\app\start_mode\打包提交.ps1"
#
# 每次运行都会重新同步：最新源码 / 最新 jar / 最新两份 docx，再压缩视频并打包。

$ErrorActionPreference = "Stop"

$Root     = "d:\app\start_mode"
$Name     = "李儒健202410011831+课堂智答"
$OutDir   = Join-Path $Root $Name
$ZipPath  = Join-Path $Root "$Name.zip"
$Demo     = "D:\java\jdk8\demo"
$SrcVideo = Join-Path $Root "演示视频.mp4"
$DstVideo = Join-Path $OutDir "演示视频.mp4"
$Ffmpeg   = "D:\app\oopz\ffmpeg.exe"

function MB($bytes) { "{0:N1} MB" -f ($bytes / 1MB) }

Write-Host "== 1/5 处理视频 ==" -ForegroundColor Cyan
if (-not (Test-Path -LiteralPath $SrcVideo)) {
    throw "找不到视频：$SrcVideo`n请先把录制文件命名为 演示视频.mp4 放到 $Root"
}
$srcSize = (Get-Item -LiteralPath $SrcVideo).Length
Write-Host ("原始视频：{0}" -f (MB $srcSize))
if ($srcSize -le 35MB) {
    Write-Host "原始视频已 ≤35MB，直接使用（不压缩）。"
    Copy-Item -LiteralPath $SrcVideo -Destination $DstVideo -Force
} else {
    if (-not (Test-Path -LiteralPath $Ffmpeg)) { throw "找不到 ffmpeg：$Ffmpeg" }
    Write-Host "开始压缩（720P / CRF 27）..."
    & $Ffmpeg -y -i "$SrcVideo" -vf "scale=-2:720" -c:v libx264 -preset medium -crf 27 -c:a aac -b:a 96k "$DstVideo"
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg 压缩失败，退出码 $LASTEXITCODE" }
}
Write-Host ("处理后视频：{0}" -f (MB (Get-Item -LiteralPath $DstVideo).Length))

Write-Host "`n== 2/5 同步源码（src + pom.xml + ai-corpus）==" -ForegroundColor Cyan
$srcDir = Join-Path $OutDir "源码"
if (Test-Path -LiteralPath $srcDir) { Remove-Item -LiteralPath $srcDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $srcDir | Out-Null
Copy-Item -LiteralPath (Join-Path $Demo "src")     -Destination (Join-Path $srcDir "src")      -Recurse -Force
Copy-Item -LiteralPath (Join-Path $Demo "pom.xml") -Destination (Join-Path $srcDir "pom.xml")  -Force
Copy-Item -LiteralPath (Join-Path $Demo "ai-corpus") -Destination (Join-Path $srcDir "ai-corpus") -Recurse -Force
Write-Host "  已同步 src / pom.xml / ai-corpus"

Write-Host "`n== 3/5 同步文档与可运行包 ==" -ForegroundColor Cyan
foreach ($f in @("项目设计文档.docx", "测试设计文档.docx")) {
    $p = Join-Path $Root $f
    if (-not (Test-Path -LiteralPath $p)) { throw "缺少文档：$p" }
    Copy-Item -LiteralPath $p -Destination $OutDir -Force
    Write-Host "  已同步 $f"
}
$jarSrc = Join-Path $Demo "target\firestDemo-0.0.1-SNAPSHOT.jar"
if (-not (Test-Path -LiteralPath $jarSrc)) { throw "找不到可运行 jar：$jarSrc" }
Copy-Item -LiteralPath $jarSrc -Destination $OutDir -Force
Write-Host ("  已同步 jar（{0}），构建时间 {1}" -f (MB (Get-Item -LiteralPath $jarSrc).Length), (Get-Item -LiteralPath $jarSrc).LastWriteTime)

Write-Host "`n== 4/5 打包 ==" -ForegroundColor Cyan
if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
Compress-Archive -Path $OutDir -DestinationPath $ZipPath -CompressionLevel Optimal
$zipSize = (Get-Item -LiteralPath $ZipPath).Length
Write-Host ("压缩包：$ZipPath  {0}" -f (MB $zipSize))

Write-Host "`n== 5/5 体积校验 ==" -ForegroundColor Cyan
if ($zipSize -gt 100MB) {
    Write-Host ("[超限] 压缩包 {0} 超过 100MB。请把脚本里的 -crf 27 改成 29~30 后重跑。" -f (MB $zipSize)) -ForegroundColor Red
} else {
    Write-Host ("[通过] 压缩包 {0} ≤ 100MB（视频 {1}）。" -f (MB $zipSize), (MB (Get-Item -LiteralPath $DstVideo).Length)) -ForegroundColor Green
}
Write-Host "`n提交目录内容："
Get-ChildItem -LiteralPath $OutDir | Select-Object Name, @{n='大小';e={if ($_.PSIsContainer) { '—' } else { MB $_.Length }}} | Format-Table -AutoSize