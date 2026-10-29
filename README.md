# NEXUS · Ports PC

Launcher de biblioteca para ports de PC, com interface imersiva e artwork do jogo selecionado. Aplicativo nativo WPF com alvo **Windows 10 22H2 e Windows 11 x64**, com .NET Framework 4.8. O Windows 10 precisa ter o .NET Framework 4.8 instalado. A validação histórica foi feita no Windows 11; a confirmação em uma máquina Windows 10 permanece pendente. Não precisa de Node, Python, navegador, conta ou permissão de administrador para executar.

Na **1.6.0**, o NEXUS instala **255 perfis diretamente do GitHub**: **223 ZIPs**, **10 executáveis portáteis** e **22 instaladores do autor**, sem precisar abrir o GitHub no navegador. O catálogo mantém **274 fontes**: os 255 perfis integrados, 1 importação oficial do Azimuth e 18 projetos sem integração utilizável nesta versão. Os números incluem forks, não apenas jogos únicos.

**Instalar:** abra Baixar ports, escolha o projeto e clique **Baixar e adicionar**. O NEXUS consulta a Release mais recente compatível com Windows, baixa, verifica e extrai o pacote. Se houver vários aplicativos, escolha o principal e o configurador na própria janela do NEXUS. Executáveis auxiliares e binários ARM64 são filtrados. Para os 22 instaladores, **Baixar e instalar** abre o assistente do autor; conclua-o e selecione o aplicativo instalado para o cadastro. NEXUS não inventa parâmetros de instalação silenciosa.

**Atualizar:** ao abrir o catálogo, o NEXUS consulta os ports instalados por ele. Também há **Verificar atualizações** e o filtro **Atualizações disponíveis**. Quando a Release Windows é mais recente, o botão vira **Atualizar**; a mesma entrada da biblioteca mantém seu nome, favorito e artwork. Uma resposta em cache exige nova confirmação online; versões iguais ou semanticamente mais antigas não habilitam atualização.

Nos ZIPs e executáveis portáteis, o NEXUS prepara a atualização separadamente, copia arquivos e saves, preserva configurações modificadas e troca a pasta somente ao concluir. Guarda a instalação completa anterior em uma pasta `.nexus-backup-…` ao lado dela. Mudanças no nome da pasta que envolve o pacote são tratadas para manter os dados no lugar correto. Feche o jogo antes de atualizar e reserve espaço para a cópia. Não apague o backup antes de conferir a nova versão. Os instaladores externos controlam suas próprias alterações; não recebem essa garantia de rollback do NEXUS.

As versões anteriores instaladas pelo NEXUS, com comprovante `.nexus-install.json`, são reconhecidas. Jogos adicionados manualmente e arquivos fora da instalação gerenciada não são sobrescritos. Azimuth não usa GitHub e continua com importação do ZIP oficial. Os projetos sem Release Windows, arquivos RAR sem suporte, pacotes multipartes e outros fluxos específicos permanecem identificados no catálogo; a lista de exceções está em [CATALOG.md](CATALOG.md).

Validação: **677 testes**, instalação real e verificação de SHA-256/PE nos pacotes de Pokémon Snap e Perfect Dark, testes de tela para instalação, configuração e atualização. Os testes cobrem cancelamento, checksum inválido, preservação de saves/configurações, migração de pastas de versão, rollback e comparação de versões. Os demais novos perfis são inspecionados no momento da instalação; não foram testados individualmente em gameplay. Nenhum jogo ou instalador de terceiros foi executado durante essa verificação.

Na **1.5.1**, foram corrigidos os botões indisponíveis do catálogo. **Os quatro perfis de Wii U agora têm Baixar e adicionar:** Breath of the Wild / BotW Native, Wind Waker HD, New Super Mario Bros. U e Super Smash Bros. for Wii U. Outros **13 perfis** também passaram da preparação manual para a instalação automática: Twilight Princess / Dusklight, Super Mario Strikers, Luigi’s Mansion, Metroid Prime, New Super Mario Bros. Wii, Mario Strikers Charged / Recharged, Super Mario Maker 2, Pokémon Let’s Go, Killzone Shadow Fall, Dragon’s Crown Pro, UFC 3, Unicorn Overlord e inFAMOUS First Light.

O catálogo mantém 274 fontes: **61 downloads GitHub integrados**, **1 importação de ZIP oficial** e **212 perfis de instalação manual / desenvolvimento**. Nestes últimos, o botão agora é **Ver downloads oficiais**, habilitado para abrir as Releases do autor no navegador. Ele não faz extração nem cadastro automático; um projeto em desenvolvimento pode não ter pacote pronto. O campo de pasta e o botão de consulta ficam desativados nesses perfis, pois não participam desse fluxo.

Wind Waker HD e NSMBU usam **Configurar jogo** para reabrir o assistente original com `--setup`. BotW Native e Smash 4 usam seus launchers originais em ambos os botões, para preservar a preparação e as opções. First Light usa `--play` em Jogar e abre o launcher normalmente em Configurar. A primeira preparação de alguns ports compila seus próprios arquivos e pode baixar ferramentas do autor. O NEXUS instala o pacote do port; não executa essa preparação por conta própria.

Validação: **270 testes**, **17 ZIPs oficiais** baixados e conferidos por SHA-256 e PE AMD64, instalação e cadastro pelo pipeline real, mais teste visual do botão habilitado nos quatro perfis Wii U. Nenhum jogo ou compilador de terceiros foi executado nessa verificação.

Na **1.5.0**, o catálogo indicado do [RecompLauncher](https://recomplauncher.com/?lang=pt-BR) foi comparado com o NEXUS: **232 novas fontes**, totalizando **274**. São **44 downloads GitHub validados**, **1 importação de ZIP oficial** e **229 entradas de preparação manual / desenvolvimento**. Esses números representam projetos e forks, não jogos únicos. O catálogo fica incluído no aplicativo; não há sincronização automática com o site.

Dez novos downloads Windows x64: Zelda 64: Recompiled, Ship of Harkinian, SpaghettiKart (Mario Kart 64), 2 Ship 2 Harkinian, Starship (Star Fox 64), Banjo: Recompiled, Ghostship (Super Mario 64), Donkey Kong 64: Recompiled, Fable II e Pirates of the Caribbean: At World's End. Os ZIPs oficiais foram conferidos por SHA-256 e arquitetura AMD64, sem executar os jogos. Fable II possui launcher separado para Configurar; Pirates abre seu launcher original também em Jogar. Os ports N64 preparam a ROM e configuram opções dentro do próprio aplicativo.

O catálogo agora possui **busca por jogo, port ou repositório**, filtro por tipo de instalação e filtros próprios de Nintendo 64, GameCube, Wii, Wii U, Super Nintendo, Game Boy, Game Boy Advance, Nintendo DS, PlayStation, PlayStation 2, PSP e Dreamcast. Perfis manuais têm o botão **Abrir projeto**; seus pacotes Windows e sua integração automática ainda não foram validados pelo NEXUS. Quatro projetos publicados apenas para outros sistemas foram excluídos. Consulte a lista e os limites em [CATALOG.md](CATALOG.md), e o registro da comparação em [assets/catalog-sources.json](assets/catalog-sources.json).

Na **1.4.0**, a pesquisa de **28/10/2026** acrescentou **12 perfis** ao catálogo, então com **42 fontes**: 34 com download GitHub, 1 com importação do ZIP oficial e 7 com preparação manual.

- **PS4:** Knack, Knack II e WipEout Omega Collection.
- **PS3:** Resident Evil Code: Veronica X HD e Drakengard 3.
- **Xbox 360:** Blue Dragon (re:Blue), Lost Odyssey e Project Gotham Racing 4.
- **Xbox original:** Conker: Live & Reloaded.
- **Switch:** Link’s Awakening (LAS Recomp / peiikai), como fonte separada do projeto já listado.
- **Projetos com instruções manuais:** Burnout 3: Takedown (instalador EXE) e Super Mario Odyssey (sem release Windows pronta na consulta).

Knack II, Drakengard 3, Conker e LAS Recomp usam pré-releases sinalizadas e exigem aceitação antes da instalação. O estágio experimental não é uma garantia de campanha completa. Para PGR4 e Conker, **Configurar jogo** escolhe a pasta extraída; o NEXUS salva o caminho e passa os parâmetros/ambiente documentados ao abrir. Para Lost Odyssey, Configurar abre a página de preparação (`--setup-only`). Os demais perfis usam seus launchers ou menus internos conforme indicado no catálogo. A lista completa de fontes e limites está em [CATALOG.md](CATALOG.md).

Na **1.3.2**, o perfil Darkwatch foi corrigido para **[linkssy2/DarkwatchDarkerEdition](https://github.com/linkssy2/DarkwatchDarkerEdition)**. **Configurar jogo** abre `Darkwatch Darker Edition.exe` para importar sua ISO/XISO e **Jogar** abre `Play Darkwatch.exe`. O perfil consulta releases estáveis. A cópia para assets descrita no histórico se aplica ao port antigo Arizona Vamp; instalações antigas permanecem separadas e não são renomeadas como Darker Edition. Baixe a nova fonte pelo catálogo e prepare seus arquivos no launcher original.

Na **1.3.1**, **BLACK e Darkwatch** têm um menu integrado em **Configurar jogo**: escolha uma pasta extraída com `default.xbe` e todos os arquivos do jogo, ou uma **ISO do Xbox original**, e pressione **Preparar arquivos**. O NEXUS copia para `assets` ao lado do port e mostra progresso e cancelamento. A origem permanece no lugar; se houver uma pasta `assets` anterior, ela fica preservada em `assets.nexus-backup-…`. A cópia pode ocupar vários GB. Se os arquivos ainda não estiverem preparados, **Jogar** abre esse menu antes de iniciar. Após concluir, Jogar continua abrindo o executável direto. O port ainda valida a região e a revisão do seu jogo.

Na **1.3.0**, foram integrados **BLACK**, **Darkwatch**, **Forza Motorsport 2, 3 e 4**, **Forza Horizon / Pinyon Shift** e **Forza Horizon 2 / Azimuth**. São **30 perfis**: 24 com download GitHub, 1 com importação do ZIP oficial do itch.io e 5 com instruções manuais.

- Na 1.3.0, o perfil Arizona Vamp estava em beta. O catálogo mostra o aviso e só habilita a instalação após marcar a aceitação. Os demais perfis continuam consultando apenas releases estáveis.
- Azimuth: filtre **Xbox One**, escolha **Forza Horizon 2 · Azimuth**, clique **Baixar no site oficial** e obtenha `azimuth-windows.zip` no itch.io. Depois clique **Importar ZIP e adicionar** no NEXUS e escolha o arquivo. A extração e o cadastro são automáticos. O download e a versão atual são controlados pelo autor; não há espelho ou redistribuição no NEXUS.
- Forza Motorsport 2/3/4 têm entradas de Jogar e Configurar separadas. Prepare seus discos e perfis no launcher antes de jogar. Em FM2, use o Play do launcher na preparação de shaders; em FM4, use-o quando alterar opções de mods/120 FPS que são aplicadas ao iniciar.
- Forza Horizon e Azimuth abrem seus launchers originais em ambos os botões. Pinyon Shift compila a partir do seu disco, processo que pode levar 20–60 minutos e requer cerca de 30 GB. NEXUS não executa essa preparação por conta própria.
- BLACK e Darkwatch possuem configurações internas, sem botão de launcher separado.

Histórico de recursos:

Na **1.2.0**, a coleção e o catálogo têm filtros para **PlayStation 4, PlayStation 3, Xbox, Xbox 360, Xbox One, Nintendo Switch e Outras**. O filtro combina com busca, favoritos, grade e lista. Em **Editar cadastro**, escolha a plataforma de qualquer instalação manual. Os cadastros antigos conhecidos são classificados automaticamente; jogos desconhecidos ficam em Outras.

O catálogo tem **23 perfis**, dos quais **18 permitem baixar e adicionar**. Novos downloads: Halo/OpenCE (Xbox), Sonic Unleashed Recompiled (Xbox 360), Hyrule Warriors e Bayonetta 2 (Switch), Gravity Rush Remastered e Gravity Rush 2 (PS4). Os demais projetos novos estão marcados como instalação manual/em desenvolvimento, com o motivo e link para a fonte. Xbox One ainda não tem port de jogo com pacote Windows validado nesta pesquisa; seu filtro aceita instalações adicionadas manualmente.

**Configurar jogo** aparece quando há um launcher associado. Em ports com opções internas, use o menu do jogo. Bayonetta 2 exige preparação no builder e Jogar abre seu launcher. Esses detalhes aparecem antes de baixar. O NEXUS não transforma versões de console em ports nem garante a compatibilidade dos projetos de terceiros.

Na versão **1.0.1**, **Configurar jogo** abre o launcher original de configuração. **Editar cadastro** altera nome, executável, argumentos e artwork no NEXUS. **Jogar** usa a entrada direta do port. Você pode escolher manualmente um launcher de configuração em Editar cadastro.

Na **1.0.2**, escolha **Grade** ou **Lista** na barra da coleção. Use o controle **Tamanho** ou os botões **− / ＋** para ajustar as capas da grade (140–340 px) ou a altura das linhas da lista (48–132 px). O modo e o tamanho de cada visualização são lembrados separadamente ao trocar de modo e ao reabrir o aplicativo. Busca, favoritos e seleção funcionam nos dois modos. Na lista, cada linha mostra a imagem, o nome e a última abertura; em linhas maiores também aparece o estado de favorito.

Na **1.1.0**, **Baixar ports**, ao lado de Detectar ports, abre o catálogo de **11 jogos / 12 fontes**. Consulta a última release estável, baixa o ZIP Windows, verifica SHA-256 quando publicado, extrai e adiciona à biblioteca. Há progresso, cancelamento e escolha do destino. Inclui GOW III, Bloodborne, Shadow (original/fork Oussama268), Uncharted e os sete novos projetos: DRIVECLUB, inFAMOUS Second Son, The Last Guardian, CTR Nitro-Fueled, MediEvil, Gran Turismo Sport e The Order: 1886. Veja [CATALOG.md](CATALOG.md) para fontes, funcionamento e diferenças de inicialização.

Cada instalação usa uma pasta nova por versão; não substitui ports existentes nem migra automaticamente saves/configurações entre cópias. Após baixar, siga as instruções do perfil para preparar seus próprios arquivos. Se a API do GitHub estiver limitada, o catálogo sinaliza quando usa o cache verificado, com data visível. O suporte a caminhos longos da extração não exige editar o Registro do Windows.

## Versão de testes 1.7.0-beta.1

Esta pré-release adiciona o alvo Windows 10 22H2 x64 à edição WPF e uma primeira edição nativa Linux x86_64 em AppImage. **Não é uma versão estável nem há paridade de recursos entre as edições.** Use uma biblioteca separada e faça backup antes de testar. Windows 10 real ainda precisa ser validado; testes Windows Server do GitHub Actions não substituem essa verificação. Os recursos e limites da prévia Linux estão na seção Linux abaixo.

A automação de release só publica o ZIP Windows e o AppImage depois dos builds, autotestes e smoke tests de interface das duas edições. Nenhum jogo de terceiros é executado nesses testes.

## Usar

1. Extraia `Nexus-Ports-PC-Windows-x64.zip` em uma pasta de sua preferência.
2. Abra `Nexus.exe`, mantendo `Nexus.exe.config` ao lado dele.
3. Na primeira execução, o launcher identifica os quatro ports conhecidos em Downloads. Para outras pastas, use **Adicionar jogo** e escolha um `.exe` ou atalho `.lnk`.
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
| Uncharted: The Nathan Drake Collection (BilalReloaded) | `Uncharted PC port Bila/` | Python incluído no próprio port + `tools/probe_uncharted.py` |

Para Uncharted, **Configurar jogo** abre `Uncharted Launcher.exe`. **Jogar**, com os argumentos do cadastro vazios, lê a seleção e as opções salvas em `%APPDATA%/Uncharted Launcher/launcher.json` e inicia o jogo escolhido sem abrir a interface do launcher. Usa somente o Python já incluído no port; não precisa instalar Python. Configure a pasta do jogo uma vez no launcher original. Não execute diretamente `gow3_pc/out/Uncharted.exe`, pois essa entrada pula a preparação e as opções do port. Se preencher argumentos personalizados para `Uncharted Launcher.exe`, o NEXUS respeita essa inicialização personalizada.

Você pode cadastrar qualquer outro executável. A detecção verifica esses caminhos, sem varrer o disco inteiro. Pastas Downloads redirecionadas ou instalações fora desses caminhos podem ser cadastradas manualmente. Nomes e argumentos são sugestões editáveis; a configuração original do port continua sendo usada pelo executável.

## Artwork automática

Ao adicionar um jogo, o NEXUS procura, nesta ordem:

1. Arquivos locais: `artwork.jpg/png`, `background.jpg/png`, `hero.jpg/png`, `sce_sys/pic1.png`, `sce_sys/pic0.png`, `assets/background.jpg`, `launcher/background.jpg` ao lado do executável.
2. Para os ports conhecidos, lê o campo `game_dir` das configurações originais em `%APPDATA%/gow3-launcher`, `sotc-launcher`, `bbport-launcher` ou `Uncharted Launcher/launcher.json` e procura `sce_sys/pic1.png`, `pic0.png` ou `icon0.png` na instalação do jogo. Não altera esses arquivos.
3. **SteamGridDB**, se uma chave de API foi informada em Configurações: busca o título exato e uma hero panorâmica estática.
4. **Wikipedia**, sem chave: imagem da página exata do título. Pode ser uma capa vertical ou imagem de resolução limitada; não existe garantia de fundo panorâmico para todo jogo.

Também é possível escolher uma imagem em **Editar cadastro → Escolher imagem**, ou solicitar nova busca em **Buscar automaticamente**. Imagens são copiadas para o cache: não precisam permanecer na pasta de origem. A busca roda em segundo plano, com limite de tamanho e timeout; erros de rede não impedem jogar. Sem imagem, a interface mostra um fundo neutro.

Na versão 1.0.1, as opções de artwork ficam em **Editar cadastro**. O campo **Título para buscar artwork** permite informar um nome de busca diferente do nome exibido. Nomes como “Uncharted Collection” são reconhecidos pelo port e associados a “Uncharted: The Nathan Drake Collection”. Cadastros existentes sem imagem recebem uma nova tentativa ao abrir o NEXUS atualizado.

O SteamGridDB exige sua própria chave: [obter chave](https://www.steamgriddb.com/profile/preferences/api). Ela é criptografada com Windows DPAPI para o usuário atual e nunca incluída no repositório. A busca online envia **apenas o nome do jogo**; não envia caminhos, saves ou executáveis. É possível desativá-la. Credenciais são enviadas apenas à API SteamGridDB, nunca aos servidores das imagens. Imagens e jogos pertencem aos respectivos titulares; a licença MIT se aplica somente ao código do launcher. A fonte da imagem aparece na edição do cadastro.

## Dados e backup

Os dados ficam em `%LOCALAPPDATA%/NexusPortsPC/`: `library.json`, backup `.bak`, `artwork/` e log local. **Configurações → Abrir dados locais** abre a pasta. Copie a pasta inteira para backup; a chave SteamGridDB deve ser informada novamente se mudar de usuário/PC. A biblioteca não vai para o GitHub. Para um perfil separado: `Nexus.exe --data-dir "C:\Minha Biblioteca"`.

## Compilar e testar

No Windows 10 22H2 / Windows 11 x64, PowerShell:

```powershell
./build.ps1 -Test
```

O script usa o compilador do .NET Framework que acompanha o Windows; nenhuma dependência externa ou restauração NuGet é necessária. A saída fica em `dist/Nexus/` e `dist/Nexus-Ports-PC-Windows-x64.zip`. O projeto usa a sintaxe C# 5 do compilador incluído. O workflow GitHub Actions compila, testa e disponibiliza o ZIP como artifact.

Os testes verificam persistência Unicode, argumentos, validação dos caminhos, recuperação de backup, credenciais DPAPI, identificação dos ports e cache de imagens. Não abrem jogos. Um teste de renderização pode ser executado com:

```powershell
./dist/Nexus/Nexus.exe --data-dir "C:\Temp\NexusTest" --smoke-test "C:\Temp\nexus-preview.png"
```

O smoke test abre e renderiza a janela, salva a imagem e encerra, sem detecção ou consultas online. O alvo do launcher é Windows 10 22H2 / Windows 11 x64. Execute `./tests/windows-compatibility.ps1` na máquina alvo para compilar, executar os autotestes e renderizar as cinco telas; a validação em Windows 10 ainda não foi executada neste ambiente. Windows em ARM e a execução dos jogos não foram certificados nesta versão.

Testes de integração opcionais: `./tests/integration.ps1 -Mode online` verifica o download público da Wikipedia; `-Mode local` verifica os ports conhecidos instalados em Downloads e suas imagens locais. O modo local requer esses ports e seus launchers originais configurados. Nenhum dos testes inicia jogos. Os dados ficam em `test-output/`, ignorado pelo Git.

Para o catálogo: `-Mode catalog` verifica os 12 perfis de release, identificando se vieram da consulta ao vivo ou do cache auditado; `-Mode catalog-install` testa uma instalação real; `-Mode catalog-packages` baixa, verifica, extrai e cadastra os sete pacotes novos, sem executá-los. O último modo usa espaço em disco para os pacotes extraídos. O smoke test aceita `--catalog-smoke` para renderizar o catálogo sem rede.

## Referências

- [Microsoft: .NET Framework no Windows 11](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)
- [MediaWiki PageImages API](https://www.mediawiki.org/wiki/API:Pageimages)
- [SteamGridDB API](https://www.steamgriddb.com/api/v2)

## Linux nativo · prévia AppImage

A edição Linux usa Avalonia e .NET 10 LTS, sem Wine. Ela está em prévia e **ainda não tem paridade com a edição Windows**. Permite adicionar, editar e remover cadastros de executáveis Linux e AppImages, buscar, favoritar, escolher artwork local e iniciar o jogo ou seu configurador com argumentos e pasta de trabalho separados. A remoção exclui somente o cadastro.

O catálogo incluído serve para consultar e abrir os projetos oficiais. Os 274 perfis existentes selecionam pacotes Windows: a edição Linux não oferece esses arquivos como downloads Linux. Instalação automática, atualização com rollback, importação/preparação de conteúdo de consoles, detecção de ports Windows e artwork online ainda não foram portadas. A compatibilidade dos jogos depende de cada projeto; o AppImage do NEXUS não converte jogos Windows em jogos Linux.

Em Linux x86_64 com desktop X11 ou Wayland/XWayland (alvo de distribuição: Ubuntu 22.04 ou posterior), baixe o artifact `Nexus-Linux-x86_64-preview` do workflow Linux, confira o SHA-256 e execute:

```sh
sha256sum --check Nexus-Linux-x86_64.AppImage.sha256
chmod +x Nexus-Linux-x86_64.AppImage
./Nexus-Linux-x86_64.AppImage
```

O runtime .NET vai incluído. Bibliotecas do desktop, Fontconfig e ICU são dependências do sistema. Se FUSE não estiver disponível, use `APPIMAGE_EXTRACT_AND_RUN=1 ./Nexus-Linux-x86_64.AppImage`. Não é necessário instalar Python ou .NET para usar o pacote.

Os dados ficam em `$XDG_DATA_HOME/NexusPortsPC`, ou `~/.local/share/NexusPortsPC`. Para separar uma biblioteca, use `--data-dir /caminho/biblioteca`. O formato JSON preserva campos da edição Windows, mas caminhos Windows não funcionam no Linux e a chave de artwork protegida por DPAPI não é descriptografada. Faça backup antes de migrar; não abra a mesma biblioteca nas duas edições simultaneamente.

Para desenvolver e empacotar (SDK .NET 10 LTS, curl, Python 3 e ferramentas Linux padrão):

```sh
dotnet run --project desktop/Nexus.Core.Tests -c Release
dotnet run --project desktop/Nexus.Desktop -c Release
./packaging/linux/build-appimage.sh
```

A saída fica em `dist/linux/Nexus-Linux-x86_64.AppImage`. O empacotamento fixa por SHA-256 o appimagetool e o runtime AppImage; se uma origem mudar, investigue antes de atualizar o hash. A publicação Windows existente continua usando `build.ps1`; a nova interface não substitui o launcher WPF.
