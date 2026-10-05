# Compile + bangun ulang konten (Keris Bali, Keris Sumatra, Candi Borobudur, Karambit, Komodo dari GLB Blender) + EditMode test + kartu QR & kartu penanda.
# Bila model diubah, ekspor ulang GLB dulu:
#   blender --background --factory-startup --python Tools\blender\keris_bali.py
#   blender --background --factory-startup --python Tools\blender\keris_sumatra.py
#   blender --background --factory-startup --python Tools\blender\candi_borobudur.py -- --export
#   blender --background --factory-startup --python Tools\blender\karambit.py
#   blender --background --factory-startup --python Tools\blender\komodo.py
# Bila naskah mode Kisah (Tools\narasi\kisah.json) diubah, buat ulang suaranya dulu (perlu internet):
#   python Tools\narasi\kisah_tts.py
# Bila musik latar (Tools\musik\musik.json) diganti, unduh treknya ke Tools\musik\asli\ lalu olah dulu:
#   python Tools\musik\siapkan_musik.py
# Tutup Unity Editor dulu (project tidak boleh terbuka di dua tempat).
# Jalankan: powershell -ExecutionPolicy Bypass -File Tools\compile_and_test.ps1
$ErrorActionPreference = 'Continue'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$proj = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $proj 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

Write-Host '1/3 Compile + Setup Everything (bisa beberapa menit)...'
$p = Start-Process $unity -Wait -PassThru -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$proj`"", '-buildTarget', 'Android',
    '-executeMethod', 'NusantaraAR.EditorTools.ProjectSetup.RunBatch', '-logFile', "`"$logs\setup.log`"")
Write-Host "   exit code: $($p.ExitCode)"
Select-String -Path "$logs\setup.log" -Pattern 'error CS|Exception|\[NusantaraAR\]' | Select-Object -First 40 | ForEach-Object { Write-Host "   $($_.Line)" }

Write-Host '2/3 EditMode test...'
$p = Start-Process $unity -Wait -PassThru -ArgumentList @('-batchmode', '-projectPath', "`"$proj`"", '-runTests', '-testPlatform', 'EditMode',
    '-testResults', "`"$logs\tests.xml`"", '-logFile', "`"$logs\tests.log`"")
Write-Host "   exit code: $($p.ExitCode) (0 = semua lulus, 2 = ada yang gagal)"
if (Test-Path "$logs\tests.xml") {
    [xml]$x = Get-Content "$logs\tests.xml"
    $r = $x.'test-run'
    Write-Host "   total $($r.total), lulus $($r.passed), gagal $($r.failed), dilewati $($r.skipped)"
    $x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { Write-Host "   GAGAL: $($_.fullname)"; Write-Host "     $($_.failure.message.InnerText)" }
}

Write-Host '3/3 Kartu QR + kartu penanda lama semua artefak...'
python (Join-Path $PSScriptRoot 'kartu_qr.py')
python (Join-Path $PSScriptRoot 'kartu_penanda.py')

Write-Host 'Selesai. Log lengkap: Logs\setup.log, Logs\tests.log, Logs\tests.xml'
