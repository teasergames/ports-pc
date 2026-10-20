# NEXUS · Ports PC

Launcher de biblioteca para ports de PC, com interface imersiva e artwork do jogo selecionado. Aplicativo nativo WPF para **Windows 11 x64**, com .NET Framework 4.8, que já acompanha o Windows 11. Não precisa de Node, Python, navegador, conta ou permissão de administrador para executar.

Na **1.3.1**, **BLACK e Darkwatch** têm um menu integrado em **Configurar jogo**: escolha uma pasta extraída com `default.xbe` e todos os arquivos do jogo, ou uma **ISO do Xbox original**, e pressione **Preparar arquivos**. O NEXUS copia para `assets` ao lado do port e mostra progresso e cancelamento. A origem permanece no lugar; se houver uma pasta `assets` anterior, ela fica preservada em `assets.nexus-backup-…`. A cópia pode ocupar vários GB. Se os arquivos ainda não estiverem preparados, **Jogar** abre esse menu antes de iniciar. Após concluir, Jogar continua abrindo o executável direto. O port ainda valida a região e a revisão do seu jogo.

Na **1.3.0**, foram integrados **BLACK**, **Darkwatch**, **Forza Motorsport 2, 3 e 4**, **Forza Horizon / Pinyon Shift** e **Forza Horizon 2 / Azimuth**. São **30 perfis**: 24 com download GitHub, 1 com importação do ZIP oficial do itch.io e 5 com instruções manuais.

- Darkwatch está em beta. O catálogo mostra o aviso e só habilita a instalação após marcar a aceitação. Os demais perfis continuam consultando apenas releases estáveis.
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

Para o catálogo: `-Mode catalog` verifica os 12 perfis de release, identificando se vieram da consulta ao vivo ou do cache auditado; `-Mode catalog-install` testa uma instalação real; `-Mode catalog-packages` baixa, verifica, extrai e cadastra os sete pacotes novos, sem executá-los. O último modo usa espaço em disco para os pacotes extraídos. O smoke test aceita `--catalog-smoke` para renderizar o catálogo sem rede.

## Referências

- [Microsoft: .NET Framework no Windows 11](https://learn.microsoft.com/en-us/dotnet/framework/install/on-windows-and-server)
- [MediaWiki PageImages API](https://www.mediawiki.org/wiki/API:Pageimages)
- [SteamGridDB API](https://www.steamgriddb.com/api/v2)
