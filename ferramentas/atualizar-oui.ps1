<#
.SYNOPSIS
    Atualiza a tabela OUI embutida no MT Mapa de Rede.

.DESCRIPTION
    Baixa o oui.txt oficial do IEEE (ou le um arquivo local), guarda so o prefixo e o nome
    da organizacao e grava compactado em src\mapa-rede-mt.nucleo\dados\oui.txt.gz.
    Depois de rodar, compile de novo e rode os testes antes do commit.

.PARAMETER Origem
    Endereco do oui.txt do IEEE ou caminho de um arquivo ja baixado.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File ferramentas\atualizar-oui.ps1
#>
param(
    [string]$Origem = 'https://standards-oui.ieee.org/oui/oui.txt'
)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$destino = [IO.Path]::Combine($raiz, 'src', 'mapa-rede-mt.nucleo', 'dados', 'oui.txt.gz')
$temporario = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'oui-ieee.txt')

if ($Origem -match '^https?://') {
    Write-Host "Baixando $Origem"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $Origem -OutFile $temporario -UseBasicParsing -UserAgent 'MT-Mapa-de-Rede'
} else {
    Copy-Item -LiteralPath $Origem -Destination $temporario -Force
}

$tabela = New-Object 'System.Collections.Generic.SortedDictionary[string,string]' ([StringComparer]::Ordinal)
foreach ($linha in [IO.File]::ReadLines($temporario, [Text.Encoding]::UTF8)) {
    if ($linha -match '^([0-9A-Fa-f]{6})\s+\(base 16\)\s*(.*?)\s*$') {
        $prefixo = $Matches[1].ToUpperInvariant()
        $nome = $Matches[2]
        if ($nome -and -not $tabela.ContainsKey($prefixo)) {
            $tabela[$prefixo] = $nome
        }
    }
}

if ($tabela.Count -lt 30000) {
    throw "O arquivo trouxe so $($tabela.Count) prefixos. Esperado: mais de 30000. Nada foi gravado."
}

$data = Get-Date -Format 'yyyy-MM-dd'
$arquivo = [IO.File]::Create($destino)
$gzip = New-Object IO.Compression.GZipStream($arquivo, [IO.Compression.CompressionMode]::Compress)
$escritor = New-Object IO.StreamWriter($gzip, (New-Object Text.UTF8Encoding($false)))
try {
    $escritor.NewLine = "`n"
    $escritor.WriteLine('# Tabela OUI (MA-L) do IEEE, formato: prefixo<TAB>organizacao')
    $escritor.WriteLine("# Origem: $Origem, lido em $data")
    foreach ($par in $tabela.GetEnumerator()) {
        $escritor.WriteLine("$($par.Key)`t$($par.Value)")
    }
} finally {
    $escritor.Dispose()
}

Remove-Item -LiteralPath $temporario -Force
Write-Host "Tabela OUI atualizada: $($tabela.Count) prefixos em $destino"
