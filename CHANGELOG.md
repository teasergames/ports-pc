# Changelog

## 1.3.2 — 2026-10-20

- Corrige o catálogo Darkwatch para linkssy2/DarkwatchDarkerEdition, com ZIP Windows x64 e cache auditado.
- Configurar abre o importador ISO/XISO original; Jogar usa Play Darkwatch.exe.
- Mantém instalações e dados da fonte anterior separados. Darker Edition não usa a cópia legacy para assets nem exige aceitação de beta.
- 158 verificações automatizadas e validação do pacote real 0.3.

## 1.3.1 — 2026-10-20

- Menu integrado para selecionar pasta extraída ou ISO do Xbox em BLACK e Darkwatch.
- Preparação automática da pasta assets com progresso, cancelamento e preservação da seleção.
- Backup de assets existentes e limpeza apenas dos temporários criados pelo NEXUS em caso de falha/cancelamento.
- Jogar abre a preparação quando faltam arquivos e continua usando a entrada direta após concluir.
- 156 verificações automatizadas e renderização do menu integrado.

## 1.3.0 — 2026-10-20

- Sete novos perfis Xbox: BLACK, Darkwatch, Forza Motorsport 2/3/4, Forza Horizon e Forza Horizon 2 / Azimuth.
- Darkwatch beta com aceitação explícita, mantendo exclusão de pré-releases nos demais perfis.
- Download oficial do Azimuth via navegador e importação do ZIP com extração, cadastro e configuração associados ao Xbox One.
- Importação com cópia temporária, identificação SHA-256, cancelamento, limites de extração e proteção contra sobrescrita.
- 140 verificações automatizadas e instalação dos sete pacotes reais sem executar jogos.

## 1.2.0 — 2026-10-20

- Filtros de plataforma na biblioteca e no catálogo; plataforma editável por jogo e migração dos cadastros conhecidos.
- Seis novos perfis de instalação: Halo/OpenCE, Sonic Unleashed, Hyrule Warriors, Bayonetta 2 e Gravity Rush 1/2.
- Cinco projetos com links e limitações explícitas: Skate 3, GoldenEye 007, Link’s Awakening e dois projetos PS3. Xbox One com estado vazio explicado.
- Launcher de configuração opcional: botão aparece somente quando há associação.
- 115 verificações automatizadas; extração e cadastro dos seis pacotes reais e testes visuais dos filtros e modos.


## v1.1.0

- Botão Baixar ports e catálogo de 11 jogos / 12 fontes, incluindo as duas variantes de Shadow of the Colossus.
- Download direto de ZIPs Windows da última release estável do projeto, progresso, cancelamento e escolha da pasta.
- SHA-256, extração com validação dos caminhos e cadastro automático dos executáveis de jogar/configurar.
- Instalações em pastas separadas por versão, sem sobrescrever as existentes; não migra saves/configurações entre cópias.
- Cache de metadados auditados com data e sinalização quando o GitHub fica indisponível/limitado.
- Extração de caminhos longos sem mudanças no Registro, necessária às dependências Qt do inFAMOUS.
- 96 verificações automatizadas; instalação real dos sete novos pacotes validada sem executar seus programas.

## v1.0.2

- Alternância entre visualização em grade e lista.
- Controle de tamanho e botões de aumentar/diminuir: capas de 140 a 340 px e linhas de 48 a 132 px.
- Preferência de modo e tamanhos independentes persistidos na biblioteca, com compatibilidade dos cadastros anteriores.
- Lista com miniaturas, títulos, favoritos e data da última abertura; filtros e seleção compartilhados com a grade.
- Cache em memória das imagens para redimensionamento sem recarregar os arquivos a cada ajuste.
- 43 verificações de dados e smoke tests da interface nos limites pequeno/grande dos dois modos.

## v1.0.1

- Configurar jogo abre o launcher original do port; novo botão Editar cadastro mantém as opções da biblioteca separadas.
- Launcher de configuração e argumentos podem ser escolhidos por jogo.
- Suporte ao port de Uncharted: The Nathan Drake Collection, de BilalReloaded: configuração original, inicialização direta com o Python incluído no port e opções salvas, detecção e artwork local.
- Correção da busca de imagens para aliases como Uncharted Collection e novo campo de título de busca independente do nome exibido.
- Cadastros da v1.0.0 são preservados, sem alterar os arquivos ou configurações originais dos ports.
- 39 verificações automatizadas; integração de caminhos/configuração/artwork validada com os quatro ports locais. Os jogos não foram iniciados nos testes.

## v1.0.0

- Primeira versão da biblioteca imersiva para Windows 11 x64, favoritos, busca, cadastro de ports, inicialização e artworks automáticas.
