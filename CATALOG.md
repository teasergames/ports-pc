# Catálogo de ports

O NEXUS consulta a última **release estável** dos perfis GitHub e seleciona somente o ZIP Windows definido. Darkwatch: Darker Edition usa releases estáveis do repositório linkssy2/DarkwatchDarkerEdition. Azimuth usa sua página oficial no itch.io e importação local do ZIP. Não escolhe arquivos de código-fonte, Linux ou atualizadores isolados. Os projetos abaixo são ports/forks independentes, sem vínculo oficial com os detentores dos jogos.

## Darkwatch corrigido — 1.3.2

O catálogo usa linkssy2/DarkwatchDarkerEdition e escolhe somente o ZIP Windows x64, excluindo o arquivo Source.zip. O pacote 0.3 foi baixado, conferido com SHA-256 e cadastrado sem executar o jogo. Instalações anteriores Arizona Vamp ficam preservadas com sua origem original; não há migração automática de saves entre projetos.

## Histórico: preparação integrada de BLACK e Arizona Vamp — 1.3.1

Configurar jogo abre o menu do NEXUS com seleção de pasta extraída ou ISO. O runtime de BLACK [procura assets junto do executável](https://github.com/TGP482/BLACK-XBERecomp/blob/main/src/common/game_files.cpp); o pacote Darkwatch documenta a mesma estrutura. O NEXUS prepara essa pasta por cópia, sem comandos de inicialização não documentados ou alterações no port. A primeira abertura sem arquivos encaminha para esse menu. Regiões, revisão do disco e conteúdo válido continuam sendo responsabilidade do runtime original. As opções gráficas continuam internas (BLACK: F10; Darkwatch: F1).

## Novos perfis Xbox — versão 1.3.0

| Jogo / fonte primária | Origem | Jogar | Configurar / preparação |
| --- | --- | --- | --- |
| [BLACK · XBERecomp](https://github.com/TGP482/BLACK-XBERecomp) | Xbox | `BLACK.exe` | Xbox original. Forneça seu ISO ou arquivos extraídos em assets ao lado de BLACK.exe. A primeira abertura prepara o código localmente. Opções no jogo e na sobreposição F10; não tem launcher de configuração separado. |
| [Darkwatch: Darker Edition](https://github.com/linkssy2/DarkwatchDarkerEdition) | Xbox | `Play Darkwatch.exe` | Configurar abre `Darkwatch Darker Edition.exe`: importe sua ISO/XISO pelo launcher e aguarde a preparação. Mantenha payload e GameData junto dos EXEs. |
| [Forza Motorsport 2](https://github.com/0herculabs/hl360-forza2) | Xbox 360 | `forza2.exe` | Configure seu ISO, perfil e gráficos no launcher. Na primeira execução e após alterar a resolução, use Play no launcher para preparar shaders. Depois, Jogar abre forza2.exe com os ajustes salvos. Projeto em desenvolvimento. |
| [Forza Motorsport 3](https://github.com/0herculabs/hl360-forza3) | Xbox 360 | `fm3.exe` | Configure os dois discos da Ultimate Collection e o perfil no launcher original. Jogar abre fm3.exe com os arquivos TOML salvos. Prefira uma pasta curta de instalação para os arquivos dos discos. |
| [Forza Motorsport 4](https://github.com/0herculabs/hl360-forza4) | Xbox 360 | `fm4.exe` | Prepare os dois discos USA Rev 1 e seu perfil em Configurar jogo. Jogar abre fm4.exe com os ajustes salvos. Algumas alterações de mods/120 FPS são aplicadas no Play do launcher: use-o ao mudar essas opções. Prefira uma pasta curta. |
| [Forza Horizon · Pinyon Shift](https://github.com/arcanite24/pinyon-shift) | Xbox 360 | `PinyonShiftLauncher.exe` | Prévia jogável. Jogar e Configurar abrem o launcher original. Forneça seu disco e use Verify and build; a preparação pode levar 20–60 minutos e requer cerca de 30 GB. O NEXUS não inicia a compilação nem instala ferramentas automaticamente. |
| [Forza Horizon 2 · Azimuth](https://almost-there-games.itch.io/azimuth) | Xbox One | `azimuth.exe` | Distribuição oficial no itch.io. Use Baixar no site oficial e depois Importar ZIP e adicionar. Jogar e Configurar abrem o Azimuth: indique seus arquivos Xbox One 1.0.0.64 na página Game e inicie por ali. Suporta Storm Island; Fast & Furious é experimental. O NEXUS não redistribui o wrapper nem os jogos. |

Os seis ZIPs GitHub e o ZIP oficial Azimuth 0.9.10 foram obtidos e validados estruturalmente; os SHA-256 publicados pelo GitHub foram conferidos. Foram extraídos e cadastrados pelo código do NEXUS, sem executar jogos, compiladores ou launchers de terceiros. O SHA-256 calculado na importação do Azimuth identifica o arquivo local e evita sobrescrever a mesma instalação; não é uma assinatura nem uma verificação de autenticidade pelo autor. O NEXUS não redistribui nenhum desses ZIPs.

## Catálogo multiplataforma — pesquisa de 20/10/2026

A plataforma é a versão de origem usada por cada port. Uma versão Switch de um jogo lançado também no Wii U continua classificada como Switch. Não foram adicionados emuladores genéricos como se fossem ports nativos. O tipo e o estágio do projeto ficam visíveis no catálogo; uma release sem a marcação prerelease não implica que o jogo esteja finalizado.

| Plataforma | Projeto / fonte primária | Instalação no NEXUS | Preparação e configuração |
| --- | --- | --- | --- |
| Xbox | [Halo: Combat Evolved · OpenCE](https://github.com/OpenCommunityEdition/OpenCE) | Baixar e adicionar | Port do Xbox original. Na primeira abertura, selecione sua imagem do disco. Opções no menu do jogo; não possui launcher de configuração separado. |
| Xbox 360 | [Sonic Unleashed · Unleashed Recompiled](https://github.com/hedge-dev/UnleashedRecomp) | Baixar e adicionar | Recompilação da versão Xbox 360. Primeira abertura guia a instalação dos seus arquivos. Configurações dentro do jogo. Requer CPU AVX e GPU Direct3D 12 ou Vulkan 1.2. |
| Nintendo Switch | [Hyrule Warriors: Definitive Edition](https://github.com/sonsegajp/HWDER) | Baixar e adicionar | Projeto inicial. Coloque seu NSP base e suas prod.keys ao lado do hwder.exe. F1 abre as opções dentro do jogo. Requer AVX2, Vulkan 1.2 e Media Foundation. Instala somente releases sem marcação pré-release. |
| Nintendo Switch | [Bayonetta 2](https://github.com/K0NL/B2recomp) | Baixar e adicionar | Builder e launcher: prepara o jogo localmente a partir do seu próprio dump. Jogar abre o launcher e você clica em Play. Configurar abre a mesma interface. Requer Vulkan 1.3; preparação inicial pode demorar. |
| PlayStation 4 | [Gravity Rush Remastered](https://github.com/LucaPazienza/gravityrush_pc) | Baixar e adicionar | Configure seu dump PS4 no launcher original. Jogar usa o executável direto e os ajustes salvos. Projeto em desenvolvimento. |
| PlayStation 4 | [Gravity Rush 2](https://github.com/HayatoG/gr2_pc) | Baixar e adicionar | Jogar usa a entrada direta e Configurar abre o launcher bilíngue. Forneça seus arquivos PS4. |
| Xbox 360 | [Skate 3](https://github.com/mchughalex/skate3recomp) | Manual / projeto | Existe release Windows, mas o pacote não pôde ser validado neste ambiente. Instalação automática indisponível nesta versão. Consulte o projeto e adicione sua instalação manualmente. |
| Xbox 360 | [GoldenEye 007 · Xbox 360](https://github.com/SunJaycy/GoldenEye-Recomp) | Manual / projeto | Em desenvolvimento; a release publica RAR, formato ainda não suportado pelo instalador NEXUS. Extraia manualmente, forneça seus próprios arquivos em assets e adicione ge.exe. Opções dentro do jogo. O projeto relata problemas em GPUs AMD. |
| Nintendo Switch | [The Legend of Zelda: Link’s Awakening](https://github.com/kaliszmichal/lasrecomp) | Manual / projeto | Sem release publicada na data da pesquisa. Requer compilação e seu próprio dump. Consulte o projeto; adicione o executável manualmente após preparar sua instalação. |
| PlayStation 3 | [The Simpsons Arcade Game · PS3](https://github.com/sp00nznet/simpsonsarcade-ps3) | Manual / projeto | Experimental. Sem release Windows pronta publicada na data da pesquisa. Requer compilação com ps3recomp e seus próprios arquivos PS3. Abra o projeto para o estado e as instruções atuais. |
| PlayStation 3 | [Tornado Outbreak · PS3](https://github.com/sp00nznet/tornado) | Manual / projeto | Experimental: o autor relata que chega ao jogo, ainda com desempenho limitado. Sem release Windows pronta publicada na data da pesquisa. Requer compilação e seus próprios arquivos. |

**Xbox One:** Forza Horizon 2 / Azimuth está integrado na versão 1.3.0. A distribuição oficial é no [itch.io](https://almost-there-games.itch.io/azimuth). O NEXUS abre a página e importa o ZIP escolhido pelo usuário, sem espelhos ou links temporários embutidos. O wrapper usa seus arquivos Xbox One (1.0.0.64); Storm Island é suportado, Fast & Furious é experimental. Jogar e Configurar abrem `azimuth.exe`.

PS3: os dois projetos selecionados documentam progresso em jogo, mas não publicavam uma release Windows pronta na consulta. Não instalamos o SDK ps3recomp como se fosse o jogo. Link’s Awakening também não publicava releases. GoldenEye usa RAR; o instalador aceita somente ZIP. O pacote de Skate 3 não pôde ser lido/validado no ambiente de teste e não recebeu instalação automática. Isso não é uma conclusão sobre a segurança desse projeto.

Foram baixados os seis ZIPs novos habilitados para instalação, conferidos os SHA-256 quando publicados, extraídos pelo código do NEXUS e cadastrados com os executáveis reais. Nenhum jogo de terceiros foi executado. Não houve teste de gameplay, GPU, ROM, chave ou firmware. Os requisitos dos ports são independentes dos requisitos do launcher.

## Identificados nas instalações existentes

| Jogo | Repositório | Observação |
| --- | --- | --- |
| God of War III | [FelipePrado0/gow3_pc](https://github.com/FelipePrado0/gow3_pc) | Identificado no README da instalação |
| Bloodborne | [Awakenchan/BloodBorneButPc](https://github.com/Awakenchan/BloodBorneButPc) | Pacote/launcher 0.9.33 identificado pela distribuição |
| Shadow of the Colossus | [Oussama268/ShadowOfTheColossus-PC](https://github.com/Oussama268/ShadowOfTheColossus-PC) | O CHANGELOG instalado identifica o fork 0.57-opt6 |
| Uncharted: The Nathan Drake Collection | [bc160201983/Uncharted-The-Nathan-Drake-Collection-PC-port](https://github.com/bc160201983/Uncharted-The-Nathan-Drake-Collection-PC-port) | Identificado no README da instalação |

O projeto original de Shadow, [AjncSystem/ShadowOfTheColossus-PC](https://github.com/AjncSystem/ShadowOfTheColossus-PC), também aparece como opção separada. Escolher uma fonte não troca nem substitui a outra.

## Novos projetos informados pelo usuário

| Jogo | Repositório | Jogar no NEXUS |
| --- | --- | --- |
| DRIVECLUB | [OverkillLabs3/driveclub-recomp](https://github.com/OverkillLabs3/driveclub-recomp) | Abre `DriveClub.exe`, launcher original |
| inFAMOUS Second Son | [HayatoG/iss_pc](https://github.com/HayatoG/iss_pc) | `Play inFAMOUS Second Son.exe` |
| The Last Guardian | [HayatoG/tlg_pc](https://github.com/HayatoG/tlg_pc) | `Play The Last Guardian.exe` |
| Crash Team Racing: Nitro-Fueled | [GetLostIn/CTR-NitroFueled-PC](https://github.com/GetLostIn/CTR-NitroFueled-PC) | Abre `CTR-NitroFueled-PC.exe`, launcher original |
| MediEvil | [bc160201983/medievil_2019_pc_port](https://github.com/bc160201983/medievil_2019_pc_port) | `Play MediEvil.exe` |
| Gran Turismo Sport | [mlgprorektm8/RyGT](https://github.com/mlgprorektm8/RyGT) | Abre `RyGT.exe`, que prepara/converte os arquivos do usuário |
| The Order: 1886 | [mateussmeneses/theorder1886-port](https://github.com/mateussmeneses/theorder1886-port) | `Play The Order 1886.exe` |

As sete distribuições Windows foram baixadas, verificadas pelo SHA-256 publicado, extraídas e cadastradas em pastas de teste. Os executáveis de jogar/configurar foram encontrados nos pacotes reais. Nenhum jogo ou launcher desses pacotes foi executado no teste. Isso valida a instalação, não a compatibilidade de gameplay.

## Instalação

**Baixar ports → escolher jogo → conferir versão/pacote/destino → Baixar e adicionar.** O NEXUS mostra o progresso, verifica SHA-256 quando publicado, extrai e registra os executáveis. Depois, abra **Configurar jogo** e indique seus próprios arquivos.

Destino padrão: `%LOCALAPPDATA%/NexusPortsPC/ports`. Você pode escolher outra pasta. Cada pacote fica em uma subpasta `perfil-versão`. Arquivos existentes não são sobrescritos. O download não executa instaladores nem os launchers automaticamente. Não inclui jogos nem firmware.

A instalação de outra versão cria uma cópia separada. **Saves e ajustes portáteis não são migrados automaticamente entre versões.** As instalações anteriores permanecem intactas; use as funções de backup/importação do launcher original quando necessário. Este catálogo não implementa atualização automática sobre instalações existentes.

## Consulta e cache

A fonte normal é a API pública `https://api.github.com/repos/OWNER/REPO/releases/latest`. Resultados recentes são reutilizados por dez minutos durante a janela do catálogo; **Consultar novamente** força nova consulta. Nenhuma chave GitHub é exigida.

Se a rede/API estiver indisponível ou limitada, o catálogo pode usar os metadados públicos verificados incluídos no aplicativo. Nesse caso exibe **cache**, a data da verificação e o aviso de que pode existir uma versão mais recente. Os metadados iniciais foram conferidos em 18/10/2026; não são tratados como uma consulta atual. Os links continuam apontando diretamente à release correspondente do mantenedor.

Os perfis ficam em [assets/ports.json](assets/ports.json), e o cache inicial em [assets/releases-cache.json](assets/releases-cache.json). Se o mantenedor mudar o nome/estrutura do pacote, o NEXUS recusa o cadastro automático e orienta abrir o projeto, em vez de escolher outro executável ou arquivo por aproximação.
