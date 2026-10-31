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

Cores e estilos de controles ficam em `src/Theme.xaml`, carregado como recurso do aplicativo; janelas montadas em código usam as mesmas chaves (`PrimaryButton`, `Chip`, `Segment`, `Badge`…). Altere a paleta ali, não em cada janela.

O smoke test renderiza a janela, salva a imagem e encerra sem detecção ou consultas online. A imagem cobre só a área interna da janela, sem a borda do Windows; use-o com `--data-dir` apontando para a sua biblioteca para gerar as capturas do README. Aceita `--preview-view grid` ou `list`, `--preview-size`, `--catalog-smoke`, `--updates-smoke`, `--package-smoke`, `--content-smoke` e `--self-update-smoke`.

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

O atualizador consulta a última Release estável de `teasergames/ports-pc`. Se a API do GitHub limitar as consultas (60 por hora por rede, sem login), ele lê `nexus-update.json` em `github.com/teasergames/ports-pc/releases/latest/download/`, fora desse limite. O `build.ps1` gera esse manifesto em `dist/` para versões estáveis: **anexe-o a toda Release estável**, junto com o ZIP. O asset precisa seguir `Nexus-Ports-PC-v<versão>-Windows-x64.zip`, ter SHA-256 publicado e conter `Nexus.exe`, `Nexus.exe.config`, `README.md`, `CATALOG.md` e `LICENSE`, na raiz ou sob `Nexus/`. A versão do assembly deve corresponder à Release.

Os arquivos anteriores e o journal ficam em `%LOCALAPPDATA%\NexusPortsPC\launcher-updates`. A troca não inclui a biblioteca nem instalações dos jogos. Preserve esse contrato ao preparar futuras Releases.

### Versões prévias

Pre-releases do GitHub não são oferecidas pelo atualizador automático, que consulta apenas `/releases/latest`. Para que quem testou uma prévia receba a versão estável seguinte, a prévia usa um `AssemblyVersion` numérico **menor** que o da estável planejada e mostra o nome da prévia por `AssemblyInformationalVersion`:

| Release | Tag | `AssemblyVersion` | `AssemblyInformationalVersion` |
|---|---|---|---|
| Prévia | `v1.7.0-beta.2` | `1.6.90.0` | `1.7.0-beta.2` |
| Estável | `v1.7.0` | `1.7.0.0` | `1.7.0` |

O rodapé e a janela de atualização exibem a versão informativa.

## Integração e catálogo

`tests/integration.ps1` oferece modos locais e online. Consulte o parâmetro `Mode` no script para as opções disponíveis. Modos com pacotes reais exigem os arquivos de auditoria correspondentes; o modo `local` depende de instalações conhecidas em Downloads. A saída fica em `test-output/`, ignorada pelo Git.

Os perfis ficam em `assets/ports.json`, as referências de origem em `assets/catalog-sources.json` e o cache de Releases em `assets/releases-cache.json`. Veja [CATALOG.md](../CATALOG.md) para as integrações e limitações de cada fonte.

### Campos do perfil

- `ProjectType` (obrigatório): `Port`, `Recompilação`, `Decompilação`, `Reimplementação` ou `Compatibilidade`. É o filtro **Tipo de projeto**.
- O filtro **Instalação** não é um campo: vem de `ProjectOnly`, `ManualPackage` e `PackageKind` (`PortEntry.InstallMode`), para nunca divergir do instalador. `Kind` continua como rótulo livre; `"Em desenvolvimento"` em um perfil `ProjectOnly` o separa dos manuais.
- `ManualPackageFile`: nome do ZIP pedido no diálogo de importação de perfis fora do GitHub (`ManualPackage`). Os sites aceitos ficam em `PortCatalog.TrustedManualSources`.
- `MinNexusVersion` (opcional): oculta o perfil em versões do NEXUS anteriores à indicada. Use quando o perfil depender de um recurso novo do launcher.

O CI valida `assets/ports.json` contra [`assets/ports.schema.json`](../assets/ports.schema.json) antes do build, e `PortCatalog.Parse` repete as regras no aplicativo.

### Catálogo online e cache de Releases

O NEXUS baixa `assets/ports.json` do branch `main` em `raw.githubusercontent.com` ao abrir o Catálogo (e uma vez por sessão, em segundo plano, para os indicadores). Um perfil novo publicado no `main` aparece para todos sem uma nova versão do NEXUS. O documento inteiro precisa passar na validação; caso contrário, vale a última cópia válida em `%LOCALAPPDATA%\NexusPortsPC\catalog-cache.json` ou o catálogo embutido.

O workflow [`releases-cache.yml`](../.github/workflows/releases-cache.yml) roda todo dia, executa [`tools/update-releases-cache.ps1`](../tools/update-releases-cache.ps1) e publica `releases-cache.json` no branch `catalog-data`, substituído a cada execução para não poluir o histórico. Quando a API do GitHub limita as consultas, o NEXUS usa esse cache antes do embutido. Para rodar localmente:

```powershell
./tools/update-releases-cache.ps1 -Output out/releases-cache.json -Limit 5
```

O cache embutido em `assets/releases-cache.json` é um retrato auditado e os testes fixam alguns fatos dele (por exemplo, quais perfis ainda são beta). Para renová-lo, copie o resultado do script e ajuste esses testes no mesmo commit.

### Página do catálogo

[`docs/catalogo/index.html`](catalogo/index.html) lê o mesmo `ports.json` e oferece busca e filtros. Para publicá-la, ative **Settings → Pages → Deploy from a branch → `main` / `docs`**; o endereço fica `https://teasergames.github.io/ports-pc/catalogo/`.

## Dados e privacidade

Use `--data-dir "C:\Minha Biblioteca"` para isolar uma biblioteca. Os dados padrão ficam em `%LOCALAPPDATA%\NexusPortsPC`: `library.json`, backup `.bak`, `artwork/` e log local. A chave opcional do SteamGridDB usa Windows DPAPI, vinculada ao usuário; não inclua bibliotecas, credenciais, dumps, saves ou executáveis de jogos no repositório.

## Referências

- [Microsoft: .NET Framework no Windows](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)
- [MediaWiki PageImages API](https://www.mediawiki.org/wiki/API:Pageimages)
- [SteamGridDB API](https://www.steamgriddb.com/api/v2)
