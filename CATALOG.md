# Catálogo de ports

O NEXUS consulta a última **release estável** de cada repositório e seleciona somente o ZIP Windows definido pelo perfil. Não escolhe arquivos de código-fonte, Linux ou atualizadores isolados. Os projetos abaixo são ports/forks independentes, sem vínculo oficial com os detentores dos jogos.

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
