# NEXUS · Ports PC

Launcher de biblioteca para ports de PC, com interface imersiva e artwork do jogo selecionado. Aplicativo nativo WPF para **Windows 11 x64**, com .NET Framework 4.8, que já acompanha o Windows 11. Não precisa de Node, Python, navegador, conta ou permissão de administrador para executar.

## Usar

1. Extraia `Nexus-Ports-PC-Windows-x64.zip` em uma pasta de sua preferência.
2. Abra `Nexus.exe`, mantendo `Nexus.exe.config` ao lado dele.
3. Na primeira execução, o launcher identifica os três ports conhecidos em Downloads. Para outras pastas, use **Adicionar jogo** e escolha um `.exe` ou atalho `.lnk`.
4. Confira nome, argumentos e pasta de trabalho. Clique em **Salvar** e depois **Jogar**.

O launcher não inclui jogos, emuladores, firmware ou arquivos dos ports. Cada port precisa estar instalado e funcionar por conta própria. A compatibilidade dos próprios jogos, drivers e modificações depende de cada projeto. O executável desta versão não é assinado digitalmente.

## Recursos

- Fundo e cartões com a artwork do jogo selecionado, degradê para leitura e título nativo escuro.
- Adição, edição e remoção de cadastros; arquivos dos jogos são preservados.
- Busca por nome, favoritos e data da última solicitação de inicialização.
- Executável, argumentos e pasta de trabalho individuais, sem comandos intermediários de shell.
- Atalhos `.lnk` são abertos pelo Windows e preservam suas configurações próprias.
- Imagens locais e cache offline; biblioteca salva com substituição atômica e backup recuperável.
- Escala de DPI por monitor, navegação por teclado, janela redimensionável, caminhos com espaços e Unicode.
- Uma instância por biblioteca, minimização opcional ao abrir um jogo.

## Ports conhecidos

| Port | Detecção em Downloads | Inicialização |
| --- | --- | --- |
| God of War III | `gow3-windows/gow3-windows/` ou `gow3-windows/` | `Play God of War III.exe` |
| Shadow of the Colossus | `SotC-PC/` | `SotC.exe --play` |
| Bloodborne | `BBPORT/` | `Bloodborne.exe` |

Você pode cadastrar qualquer outro executável. A detecção verifica esses caminhos, sem varrer o disco inteiro. Pastas Downloads redirecionadas ou instalações fora desses caminhos podem ser cadastradas manualmente. Nomes e argumentos são sugestões editáveis; a configuração original do port continua sendo usada pelo executável.

## Artwork automática

Ao adicionar um jogo, o NEXUS procura, nesta ordem:

1. Arquivos locais: `artwork.jpg/png`, `background.jpg/png`, `hero.jpg/png`, `sce_sys/pic1.png`, `sce_sys/pic0.png`, `assets/background.jpg`, `launcher/background.jpg` ao lado do executável.
2. Para os três ports conhecidos, lê somente o campo `game_dir` das configurações originais em `%APPDATA%/gow3-launcher`, `sotc-launcher` ou `bbport-launcher` e procura `sce_sys/pic1.png`, `pic0.png` ou `icon0.png` na instalação do jogo. Não altera esses arquivos.
3. **SteamGridDB**, se uma chave de API foi informada em Configurações: busca o título exato e uma hero panorâmica estática.
4. **Wikipedia**, sem chave: imagem da página exata do título. Pode ser uma capa vertical ou imagem de resolução limitada; não existe garantia de fundo panorâmico para todo jogo.

Também é possível escolher uma imagem em **Configurar jogo → Escolher imagem**, ou solicitar nova busca em **Buscar automaticamente**. Imagens são copiadas para o cache: não precisam permanecer na pasta de origem. A busca roda em segundo plano, com limite de tamanho e timeout; erros de rede não impedem jogar. Sem imagem, a interface mostra um fundo neutro.

O SteamGridDB exige sua própria chave: [obter chave](https://www.steamgriddb.com/profile/preferences/api). Ela é criptografada com Windows DPAPI para o usuário atual e nunca incluída no repositório. A busca online envia **apenas o nome do jogo**; não envia caminhos, saves ou executáveis. É possível desativá-la. Credenciais são enviadas apenas à API SteamGridDB, nunca aos servidores das imagens. Imagens e jogos pertencem aos respectivos titulares; a licença MIT se aplica somente ao código do launcher. A fonte da imagem aparece na edição do cadastro.

## Dados e backup

Os dados ficam em `%LOCALAPPDATA%/NexusPortsPC/`: `library.json`, backup `.bak`, `artwork/` e log local. **Configurações → Abrir dados locais** abre a pasta. Copie a pasta inteira para backup; a chave SteamGridDB deve ser informada novamente se mudar de usuário/PC. A biblioteca não vai para o GitHub. Para um perfil separado: `Nexus.exe --data-dir "C:\Minha Biblioteca"`.

## Compilar e testar

No Windows 11 x64, PowerShell:

```powershell
./build.ps1 -Test
```

O script usa o compilador do .NET Framework que acompanha o Windows; nenhuma dependência externa ou restauração NuGet é necessária. A saída fica em `dist/Nexus/` e `dist/Nexus-Ports-PC-Windows-x64.zip`. O projeto usa a sintaxe C# 5 do compilador incluído. O workflow GitHub Actions compila, testa e disponibiliza o ZIP como artifact.

Os testes verificam persistência Unicode, argumentos, validação dos caminhos, recuperação de backup, credenciais DPAPI, identificação dos ports e cache de imagens. Não abrem jogos. Um teste de renderização pode ser executado com:

```powershell
./dist/Nexus/Nexus.exe --data-dir "C:\Temp\NexusTest" --smoke-test "C:\Temp\nexus-preview.png"
```

O smoke test abre e renderiza a janela, salva a imagem e encerra, sem detecção ou consultas online. A compatibilidade é direcionada ao launcher no Windows 11 x64. Windows em ARM e a execução dos jogos não foram certificados nesta versão.

Testes de integração opcionais: `./tests/integration.ps1 -Mode online` verifica o download público da Wikipedia; `-Mode local` verifica os ports conhecidos instalados em Downloads e suas imagens locais. O modo local requer esses ports e seus launchers originais configurados. Nenhum dos testes inicia jogos. Os dados ficam em `test-output/`, ignorado pelo Git.

## Referências

- [Microsoft: .NET Framework no Windows 11](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)
- [MediaWiki PageImages API](https://www.mediawiki.org/wiki/API:Pageimages)
- [SteamGridDB API](https://www.steamgriddb.com/api/v2)

