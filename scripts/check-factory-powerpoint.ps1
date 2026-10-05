[CmdletBinding()]
param([string]$OutDir = 'artifacts/local/factory-scene-x0')
$ErrorActionPreference = 'Stop'
# Native PowerPoint automation over this task's files only. It closes only the
# presentations opened here and preserves any unrelated existing PowerPoint work.
# API: https://learn.microsoft.com/en-us/office/vba/api/powerpoint.model3dformat
$out = [IO.Path]::GetFullPath($OutDir)
$app = New-Object -ComObject PowerPoint.Application
$presentation = $null
try {
    $presentation = $app.Presentations.Open("$out/factory.pptx",0,0,0)
    if ($presentation.Slides.Count -ne 2) { throw 'Expected two native slides.' }
    $rotations = @()
    for ($i=1; $i -le 2; $i++) {
        $slide = $presentation.Slides.Item($i)
        $models = @($slide.Shapes | Where-Object { $_.Type -eq 30 })
        if ($models.Count -ne 1) { throw "Slide $i did not load exactly one native 3D model." }
        $model = $models[0].Model3D
        $slide.Export("$out/powerpoint-slide-$i.png",'PNG',1600,900)
        $before = (Get-FileHash -LiteralPath "$out/powerpoint-slide-$i.png").Hash
        $model.IncrementRotationX(15)
        $model.IncrementRotationY(25)
        $slide.Export("$out/powerpoint-rotated-$i.png",'PNG',1600,900)
        $after = (Get-FileHash -LiteralPath "$out/powerpoint-rotated-$i.png").Hash
        if ($before -eq $after) { throw "Slide $i did not visibly change after native model rotation." }
        $rotations += [pscustomobject]@{slide=$i; x=$model.RotationX; y=$model.RotationY; z=$model.RotationZ; changedRender=$true}
    }
    $presentation.SaveAs("$out/factory-office-roundtrip.pptx",24)
    $presentation.Close()
    $presentation = $app.Presentations.Open("$out/factory-office-roundtrip.pptx",0,0,0)
    foreach ($r in $rotations) {
        $slide = $presentation.Slides.Item($r.slide)
        $models = @($slide.Shapes | Where-Object { $_.Type -eq 30 })
        if ($models.Count -ne 1) { throw 'Native model was lost on reopen.' }
        $model = $models[0].Model3D
        if ([Math]::Abs($model.RotationX-$r.x) -gt .01 -or [Math]::Abs($model.RotationY-$r.y) -gt .01) {
            throw 'Native rotation was lost on save/reopen.'
        }
        $model.IncrementRotationX(-10)
        $slide.Export("$out/powerpoint-reopened-$($r.slide).png",'PNG',1600,900)
        if ((Get-FileHash -LiteralPath "$out/powerpoint-reopened-$($r.slide).png").Hash -eq
            (Get-FileHash -LiteralPath "$out/powerpoint-rotated-$($r.slide).png").Hash) {
            throw 'Repeat rotation after reopen did not change native rendering.'
        }
    }
    [pscustomobject]@{ application='Microsoft PowerPoint'; version=$app.Version; slides=2; nativeModels=2;
        rotationChangedRendering=$true; savedClosedReopened=$true; rotationsPreserved=$true; rotatedAgainAfterReopen=$true;
        interaction='COM Model3D rotation and native slide rendering, not a mouse/UI test'; rotations=$rotations } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$out/powerpoint-native-validation.json" -Encoding utf8
    Write-Output 'Native PowerPoint model load, rotation, save/reopen and repeat rotation passed.'
}
finally {
    if ($null -ne $presentation) { $presentation.Close() }
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($app) | Out-Null
}
