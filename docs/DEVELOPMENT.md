# Desenvolvimento do NEXUS

Aplicativo WPF para Windows 11 x64, com .NET Framework 4.8 e sintaxe C# 5. O build usa o compilador do .NET Framework disponível no Windows, sem restauração NuGet.

## Compilar e testar

Na raiz do repositório, em PowerShell:

```powershell
./build.ps1 -Test
```

A saída fica em `dist/Nexus/` e `dist/Nexus-Ports-PC-Windows-x64.zip`. O workflow GitHub Actions compila, executa os testes e disponibiliza o ZIP como artifact.

Os testes usam dados isolados e fixtures. Não iniciam jogos ou compiladores de terceiros.

## Interface

```powershell
./dist/Nexus/Nexus.exe --data-dir "C:\Temp\NexusTest" --smoke-test "C:\Temp\nexus-preview.png"
```

O smoke test renderiza a janela, salva a imagem e encerra sem detecção ou consultas online. Aceita `--preview-view grid` ou `list`, `--preview-size`, `--catalog-smoke`, `--updates-smoke`, `--package-smoke`, `--content-smoke` e `--self-update-smoke`.

## Exclusão de ports

```powershell
./tests/removal.ps1
```

Valida o bloqueio de junctions, preservando o destino externo e o port. Os testes internos também cobrem pastas compartilhadas/protegidas, arquivos bloqueados ou alterados após a confirmação, cópias manuais, backups com comprovante, cache de assistentes e falha parcial. Use `--removal-smoke` com `--smoke-test` para renderizar a confirmação sem excluir arquivos.

## Atualização do launcher

```powershell
./tests/self-update.ps1
```

Verifica o auxiliar em processo separado, espera pelo processo anterior, reinício com confirmação, rollback de início com falha e recuperação de uma troca interrompida. Usa executáveis de fixture produzidos pelo próprio teste.

O atualizador consulta a última Release estável de `teasergames/ports-pc`. O asset precisa seguir `Nexus-Ports-PC-v<versão>-Windows-x64.zip`, ter SHA-256 publicado e conter `Nexus.exe`, `Nexus.exe.config`, `README.md`, `CATALOG.md` e `LICENSE`, na raiz ou sob `Nexus/`. A versão do assembly deve corresponder à Release.

Os arquivos anteriores e o journal ficam em `%LOCALAPPDATA%\NexusPortsPC\launcher-updates`. A troca não inclui a biblioteca nem instalações dos jogos. Preserve esse contrato ao preparar futuras Releases.

## Integração e catálogo

`tests/integration.ps1` oferece modos locais e online. Consulte o parâmetro `Mode` no script para as opções disponíveis. Modos com pacotes reais exigem os arquivos de auditoria correspondentes; o modo `local` depende de instalações conhecidas em Downloads. A saída fica em `test-output/`, ignorada pelo Git.

Os perfis ficam em `assets/ports.json`, as referências de origem em `assets/catalog-sources.json` e o cache de Releases em `assets/releases-cache.json`. Veja [CATALOG.md](../CATALOG.md) para as integrações e limitações de cada fonte.

## Dados e privacidade

Use `--data-dir "C:\Minha Biblioteca"` para isolar uma biblioteca. Os dados padrão ficam em `%LOCALAPPDATA%\NexusPortsPC`: `library.json`, backup `.bak`, `artwork/` e log local. A chave opcional do SteamGridDB usa Windows DPAPI, vinculada ao usuário; não inclua bibliotecas, credenciais, dumps, saves ou executáveis de jogos no repositório.

## Referências

- [Microsoft: .NET Framework no Windows](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)
- [MediaWiki PageImages API](https://www.mediawiki.org/wiki/API:Pageimages)
- [SteamGridDB API](https://www.steamgriddb.com/api/v2)
