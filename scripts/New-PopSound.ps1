$ErrorActionPreference = 'Stop'
$buddyTarget = Join-Path $PSScriptRoot '../src/Buddy/Assets/pop.wav'
$buddySoundWriter = [System.IO.BinaryWriter]::new([System.IO.File]::Create($buddyTarget))
try {
    $buddySamples = 8820
    $buddySoundWriter.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'))
    $buddySoundWriter.Write([int](36 + $buddySamples * 2))
    $buddySoundWriter.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $buddySoundWriter.Write([int]16)
    $buddySoundWriter.Write([Int16]1)
    $buddySoundWriter.Write([Int16]1)
    $buddySoundWriter.Write([int]44100)
    $buddySoundWriter.Write([int]88200)
    $buddySoundWriter.Write([Int16]2)
    $buddySoundWriter.Write([Int16]16)
    $buddySoundWriter.Write([System.Text.Encoding]::ASCII.GetBytes('data'))
    $buddySoundWriter.Write([int]($buddySamples * 2))
    for ($buddyI = 0; $buddyI -lt $buddySamples; $buddyI++) {
        $buddyT = $buddyI / 44100
        $buddyEnvelope = [Math]::Sin([Math]::PI * $buddyI / $buddySamples) * [Math]::Exp(-18 * $buddyT)
        $buddyValue = 6000 * $buddyEnvelope * ([Math]::Sin(2 * [Math]::PI * 660 * $buddyT) + 0.3 * [Math]::Sin(2 * [Math]::PI * 990 * $buddyT))
        $buddySoundWriter.Write([Int16]$buddyValue)
    }
} finally { $buddySoundWriter.Dispose() }
