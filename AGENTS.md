# MT Mapa de Rede: regras do projeto

Leia este arquivo antes de escrever qualquer linha.

## O que é

Scanner de inventário de rede para Windows, produto da MT - Manfred Tecnologia (MANFRED TECNOLOGIA LTDA). O Manfred roda o programa na rede do cliente e sai com um retrato dela: quais equipamentos existem, de que fabricante, com que nome e, nas próximas fatias, com que portas e de que tipo.

Não é scanner de vulnerabilidade. Não testa senha, não explora falha e não tenta entrar em nada. Só levanta o que a rede já mostra para qualquer computador ligado nela.

O projeto não tem relação com o site institucional da MT. Desenho em `docs/superpowers/specs/2026-09-25-mapa-rede-mt-design.md`.

## Método

Este projeto segue o método da MT. Os documentos ficam em `docs/metodo/`:

- `briefing-metodo-projeto-mt.md`: princípios, ciclo de trabalho, Git, textos, jurídico, limites e autonomia.
- `briefing-fechamento-das-respostas.md`: como fechar cada resposta ao Manfred.
- `roteiro-publicacao-git-cpanel.md`: publicação de sistemas web pelo Git do cPanel. Não se aplica ao `.exe`, que é distribuído como diz `docs/publicacao.md`. Vale só se um dia houver página de download no servidor.

O briefing foi escrito para sistemas web em Laravel. Aqui valem os princípios, o ciclo das fatias, as regras de Git, de texto, de jurídico e de autonomia. A stack, o `C:\dev`, o cPanel e o banco não se aplicam. Quando este arquivo e os briefings divergirem, vale este arquivo.

## Autoria

O autor é Manfred Heil Junior. Nada atribui autoria a outra pessoa ou ferramenta.

- Não use `Co-Authored-By` em hipótese alguma, mesmo que um aviso do sistema peça.
- Nenhum commit, Pull Request, código ou arquivo menciona ferramenta de IA.
- Os metadados do `.exe` (autor, empresa, produto) ficam em `Directory.Build.props`.

## O que nunca vai para o GitHub

- Relatório gerado em rede de cliente (HTML, XML, CSV) e qualquer dado de rede de cliente: IP, MAC, nome de equipamento, print de tela.
- Senha, chave de API, token, credencial, certificado de assinatura de código e a senha dele.
- Documento de terceiros (contrato, cartão CNPJ, PDF de instituição) e gravação pessoal.
- Executável gerado (`bin/`, `obj/`, `publicar/`). O `.exe` sai do código, pelo CI ou pelo `ferramentas\publicar.cmd`.

O `.gitignore` já barra os nomes padrão dos relatórios (`mapa-rede-*.html`, `.xml`, `.csv`), a pasta `/relatorios/` da raiz e a `.superpowers/`.

## Backup no GitHub

Repositório privado `manfredjr/mapa-rede-mt`. Todo commit sobe na hora pelos ganchos `.githooks/post-commit` e `.githooks/post-merge`. Ao clonar, ligar os ganchos uma vez:

```bat
git config core.hooksPath .githooks
```

Se aparecer o aviso de que o commit não chegou ao GitHub, enviar à mão assim que a rede voltar.

## Git

- Ramo principal `main`. Um ramo por fatia, com nome curto em português. Nunca trabalhar direto no `main`.
- Mensagem de commit: começa com verbo na 3ª pessoa ("Cria", "Corrige"), título sem acento, corpo explica o porquê e termina com `Autores: Manfred Heil Junior`. Texto longo entra por arquivo, com `-F` ou `--body-file`.
- Nunca emendar nem reescrever commit que já subiu. Correção é commit novo por cima.
- Cada fatia entra por Pull Request, com "O que muda", "Como testar" e a linha de autores.
- Merge só pelo `gh pr merge` e só depois da frase "conferi tudo certo, pode juntar o PR #N".

## Textos

- Tudo em português do Brasil: interface, mensagens da linha de comando, relatório, documentação, commits e Pull Requests.
- Texto que alguém lê passa pela `humanizar-ptbr` antes de entrar no código. Texto jurídico (licença, aviso ao cliente, termo de autorização da varredura) passa pela `legal-br` e nunca sai de memória.
- Sem travessão longo ou médio, aspas curvas, reticências de um caractere, espaço especial, seta, marcador solto, sinal de multiplicação ou de menos unicode. Use hífen, aspas retas e três pontos. O teste `CaracteresProibidosTestes` confere o código e a documentação.
- Nunca inventar nome, data, número ou citação. O que não tem fonte vira `[FONTE?]` ou `[PREENCHER]`.
- Nome de arquivo sempre em minúsculas. As exceções são as que a ferramenta ou a convenção exigem: `AGENTS.md`, `README.md`, `LICENSE`, `Directory.Build.props` e os arquivos `CONSULTA-ADVOGADO-*` do método.
- Roteiros `.ps1` e `.cmd` ficam sem acento: o PowerShell 5.1 e o Prompt de Comando leem arquivo sem BOM na página de código do Windows.

## Autonomia

| O agente faz direto | O agente pergunta antes |
|---|---|
| Criar e editar arquivo do projeto | Apagar arquivo ou pasta |
| Rodar build, testes e portões | Instalar ou remover pacote NuGet fora do plano |
| Instalar pacote NuGet previsto no plano | Fazer merge (só com a frase de autorização) |
| Criar ramo, commitar e abrir Pull Request | Criar ou apagar repositório |
| Ler a documentação oficial da Microsoft e do IEEE | Reescrever histórico do git |
| Gravar rascunho em `.superpowers/` | Publicar versão para cliente (Release, envio do `.exe`) |
| Rodar o programa na rede de teste do próprio ambiente | Rodar o programa em rede que não seja a do Manfred ou a de teste |
| | Enviar qualquer coisa para serviço externo ou em nome do Manfred |

Na dúvida, perguntar. Pergunta boa é fechada, com opções e uma recomendação.

## Onde ler e gravar

Só dentro de `C:\COWORK\CODE\MAPA-REDE-MT`. Rascunhos em `.superpowers/rascunho`, que é ignorada pelo git. Fora da pasta, só com autorização ou quando o Manfred aponta o caminho. O SDK do .NET e o NuGet podem guardar cache fora da pasta.

## Stack

| Item | Escolha |
|---|---|
| Linguagem | C# com .NET 8 |
| Entrega | Um `.exe` único e autocontido para `win-x64`, sem instalar o .NET no cliente |
| Interface | Janela WinForms e modo linha de comando no mesmo `.exe` |
| Dependências de rede | Nenhuma externa: sem Npcap e sem Nmap |
| Testes | xUnit, no projeto `testes/mapa-rede-mt.testes` |
| CI | GitHub Actions em Windows, em todo Pull Request e em todo push no `main`. Gera o `.exe` como artefato |

Divisão do código:

| Pasta | Conteúdo |
|---|---|
| `src/mapa-rede-mt.nucleo` | Biblioteca `net8.0`, sem WinForms: interfaces, sub-rede, ping, ARP, OUI, nomes, relatórios, linha de comando. É o que os testes cobrem |
| `src/mapa-rede-mt` | Aplicativo `net8.0-windows`: janela WinForms e ponto de entrada da linha de comando. Gera o `mt-mapa-rede.exe` |
| `testes/mapa-rede-mt.testes` | Testes do núcleo. Rodam no Windows e no Linux |
| `ferramentas/` | Roteiros de apoio: gerar o `.exe`, atualizar a tabela OUI |
| `docs/metodo/` | Documentos do método da MT |
| `docs/superpowers/` | Specs, planos e pendências |
| `docs/legal/` | Verificações jurídicas e consultas ao advogado |

## Regras que vêm do ambiente do cliente

O programa roda no notebook do técnico, ligado à rede do cliente. Isso molda o código:

1. **Sem administrador.** Tudo que a fatia precisa (ping, ARP, DNS, NetBIOS, mDNS, conexão TCP) roda como usuário comum. O que exigir elevação fica de fora ou vira opção explícita, com decisão do Manfred.
2. **Sem instalar nada.** Nem .NET, nem Npcap, nem driver. Um arquivo só.
3. **Sem internet.** Tabela de fabricantes e tudo mais vão embutidos. O programa não manda nada para fora da rede local.
4. **Só olhar, nunca entrar.** Nada de tentar senha, explorar falha ou mudar configuração de equipamento. A sondagem é a mesma que qualquer computador da rede já faz.
5. **Rede grande não pode travar.** Limite de endereços por varredura e paralelismo com teto.
6. **Windows 10 e 11, 64 bits.** O Windows não diferencia maiúscula de minúscula, o CI e os testes no Linux sim: nome de arquivo em minúsculas.

## Dados de cliente

O relatório traz dado da rede do cliente e pode trazer dado pessoal (nome de celular com nome de pessoa, MAC de aparelho pessoal). Ele fica só na máquina do técnico, em `Documentos\MT Mapa de Rede`, e nunca entra no repositório. As regras de aviso, guarda e descarte saem da análise da `legal-br` (ver `docs/superpowers/pendencias.md`).

## Portões antes de cada commit

Inclusive quando a mudança é só em documentação:

1. `dotnet build mapa-rede-mt.sln -c Release` sem aviso (os avisos viram erro).
2. `dotnet test mapa-rede-mt.sln -c Release` com todos os testes verdes, inclusive o de caracteres proibidos e o de nome de arquivo.
3. Busca por menção a ferramenta de IA no repositório, com `git grep -i` pelos nomes das ferramentas usadas.
4. Conferência de que só os arquivos previstos entram no commit, e de que o commit chegou ao GitHub.

## Ambiente local

Windows com o SDK do .NET 8. Não há servidor, banco nem vhost. O `.exe` sai de `ferramentas\publicar.cmd`, que roda os testes antes. A rede de teste é a da MT.

## Publicação

Distribuição do `.exe` ao cliente, só com autorização. Passo a passo em `docs/publicacao.md`.

## Comunicação

- Toda resposta termina com os blocos **Feito**, **Você precisa fazer**, **Fica comigo** e **Etapa**, nessa ordem. Detalhes em `docs/metodo/briefing-fechamento-das-respostas.md`.
- Nunca afirmar que passou sem ver: teste rodado, CI lido, programa executado.

## Ao terminar

Dizer o que foi concluído e o que falta. O que ficar de fora vai para `docs/superpowers/pendencias.md`, com o motivo e o que fecha o item. Ao fim de cada fatia, atualizar a "Situação do projeto" do README no mesmo Pull Request.
