# Validação 1.2.0 — 20/10/2026

- Build Windows x64 / .NET Framework 4.8: PASS 115 verificações.
- Migração de bibliotecas antigas, plataforma desconhecida, filtros exatos Xbox/Xbox 360 combinados com pesquisa e favoritos: PASS.
- Configuração ausente, perfis manuais sem consulta/download, seleção de release estável excluindo pré-releases: PASS.
- Seis ZIPs oficiais novos: SHA-256 quando publicado, extração com proteção de caminhos e limites, identificação de executáveis, cadastro, configuração e comando de inicialização sem executar o jogo: PASS.
- Interface com os seis ports: grade 140/340 e lista 48/132; troca por todas as plataformas; botão Configurar opcional: PASS.
- Catálogo: filtro de todas as plataformas, estado vazio Xbox One e projetos PS3 com instalação desabilitada: PASS.
- Não houve teste de gameplay ou garantia de compatibilidade do jogo com GPUs/processadores. Skate 3 permaneceu manual porque seu ZIP não pôde ser validado no ambiente.

Validação histórica:

# Validação da versão 1.1.0

- **96 verificações automatizadas**. O catálogo cobre seleção do ZIP correto, exclusão de código-fonte/Linux/prerelease, rejeição de pacotes ambíguos, download simulado, checksum, extração, nomes de executáveis, receipt, cancelamento, rollback e preservação de instalações existentes.
- Cache auditado e datado dos 12 perfis; fallback em limite da API, reaproveitamento de consultas e refresh explícito testados. Durante os testes finais a API pública atingiu o limite; esses testes indicaram explicitamente o uso do cache de Releases previamente consultadas.
- Os **sete pacotes novos reais** foram baixados do GitHub, verificados por SHA-256, extraídos e cadastrados: DRIVECLUB, inFAMOUS Second Son, The Last Guardian, CTR Nitro-Fueled, MediEvil, Gran Turismo Sport e The Order: 1886. Executáveis de jogar/configurar confirmados. Nenhum dos pacotes foi executado.
- Dependências com caminhos acima de 260 caracteres extraídas com suporte explícito a caminhos estendidos, inclusive no ambiente de teste com LongPathsEnabled=0. Não foram feitas mudanças no Registro.
- Catálogo WPF renderizado e inspecionado; o workflow inclui smoke test sem rede do catálogo, além da biblioteca.

- Compilação Release x64 concluída com o compilador incluído no Windows.
- As 43 verificações anteriores continuam aprovadas: armazenamento, Unicode, argumentos, caminhos, backups, validação dos cadastros, DPAPI, detecção dos ports, leitura das configurações originais e cache de imagens. Inclui separação entre configuração e execução, compatibilidade do cadastro anterior, título de artwork independente e construção da inicialização de Uncharted com seleção/opções salvas e caminhos com espaços. Inclui padrões de visualização para bibliotecas anteriores, persistência independente de modo/tamanhos e normalização dos limites.
- Smoke tests WPF de grade a 140/340 px e lista a 48/132 px, com troca real dos botões de visualização, controle de tamanho, seleção, busca e restauração de tamanho ao alternar modos. Renderizações com os quatro ports inspecionadas visualmente.
- Integração local aprovada para God of War III, Shadow of the Colossus, Bloodborne e Uncharted: identificação dos executáveis diretos e dos launchers de configuração; recuperação e cache das imagens oficiais das instalações configuradas.
- Integração online aprovada: download real da Wikipedia, decodificação, validação e cache.
- A janela WPF foi aberta, renderizada e encerrada com sucesso em Windows 11 x64, build 26200. A renderização com biblioteca vazia e com os três ports foi inspecionada visualmente.

Os testes não executam os jogos. Desempenho e compatibilidade dos ports dependem de cada projeto, hardware e drivers. Não foi realizado teste em todos os builds do Windows 11, em ARM64 ou com todas as combinações de monitores/DPI. A API SteamGridDB requer uma chave do usuário e não foi testada ao vivo nesta versão. O executável não possui assinatura digital.

O workflow em `.github/workflows/windows.yml` reproduz os testes locais e a renderização no GitHub Actions quando o código for publicado com acesso de escrita e Actions estiver habilitado.
