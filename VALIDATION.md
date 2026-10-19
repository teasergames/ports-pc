# Validação da versão 1.0

- Compilação Release x64 concluída com o compilador incluído no Windows.
- 24 verificações automatizadas aprovadas: armazenamento, Unicode, argumentos, caminhos, backups, validação dos cadastros, DPAPI, detecção dos ports, leitura das configurações originais e cache de imagens.
- Integração local aprovada para God of War III, Shadow of the Colossus e Bloodborne: identificação dos executáveis, argumentos e pasta de trabalho; recuperação e cache das imagens oficiais das instalações configuradas.
- Integração online aprovada: download real da Wikipedia, decodificação, validação e cache.
- A janela WPF foi aberta, renderizada e encerrada com sucesso em Windows 11 x64, build 26200. A renderização com biblioteca vazia e com os três ports foi inspecionada visualmente.

Os testes não executam os jogos. Desempenho e compatibilidade dos ports dependem de cada projeto, hardware e drivers. Não foi realizado teste em todos os builds do Windows 11, em ARM64 ou com todas as combinações de monitores/DPI. A API SteamGridDB requer uma chave do usuário e não foi testada ao vivo nesta versão. O executável não possui assinatura digital.

O workflow em `.github/workflows/windows.yml` reproduz os testes locais e a renderização no GitHub Actions quando o código for publicado com acesso de escrita e Actions estiver habilitado.
