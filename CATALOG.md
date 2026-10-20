# Catálogo de ports

O NEXUS consulta a última **release estável** de cada repositório e seleciona somente o ZIP Windows definido pelo perfil. Não escolhe arquivos de código-fonte, Linux ou atualizadores isolados. Os projetos abaixo são ports/forks independentes, sem vínculo oficial com os detentores dos jogos.

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

**Xbox One:** a organização [XWine1](https://github.com/XWine1) publica componentes de pesquisa da camada de compatibilidade, como [SlimEra](https://github.com/XWine1/SlimEra). Não foi identificado nesta pesquisa um instalador de port de jogo Windows pronto que possa ser cadastrado automaticamente. O filtro existe, aceita cadastros manuais e apresenta esse limite.

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
