# MT Mapa de Rede: regras do projeto

Leia este arquivo antes de escrever qualquer linha.

## O que é

Scanner de inventário de rede para Windows, produto da MT - Manfred Tecnologia (MANFRED TECNOLOGIA LTDA). O Manfred roda o programa na rede do cliente e sai com um retrato dela: quais equipamentos existem, de que fabricante, com que nome e, nas próximas fatias, com que portas e de que tipo.

Não é scanner de vulnerabilidade. Não testa senha, não explora falha e não tenta entrar em nada. Só levanta o que a rede já mostra para qualquer computador ligado nela.

O projeto não tem relação com o site institucional da MT.

## Stack

| Item | Escolha |
|---|---|
| Linguagem | C# com .NET 8 |
| Entrega | Um `.exe` único e autocontido para `win-x64`, sem instalar o .NET no cliente |
| Interface | Janela WinForms e modo linha de comando no mesmo `.exe` |
| Dependências de rede | Nenhuma externa: sem Npcap e sem Nmap |
| Permissão | Roda sem administrador sempre que der |
| Testes | xUnit, no projeto `testes/mapa-rede-mt.testes` |

Divisão do código:

| Pasta | Conteúdo |
|---|---|
| `src/mapa-rede-mt.nucleo` | Biblioteca `net8.0`, sem WinForms: interfaces, sub-rede, ping, ARP, OUI, nomes, relatórios, linha de comando. É o que os testes cobrem |
| `src/mapa-rede-mt` | Aplicativo `net8.0-windows`: janela WinForms e ponto de entrada da linha de comando. Gera o `mt-mapa-rede.exe` |
| `testes/mapa-rede-mt.testes` | Testes do núcleo. Rodam no Windows e no Linux |
| `ferramentas/` | Roteiros de apoio, como a atualização da tabela OUI |
| `docs/` | Desenho, pendências e documentação |

## Funcionamento previsto

1. Lê as interfaces ativas (Wi-Fi ou cabo), calcula a sub-rede pelo IP e pela máscara e deixa escolher a interface.
2. Descobre os hosts com ping em paralelo e ARP pela API do Windows (`SendARP` do `iphlpapi`).
3. Para cada host: IP, MAC, fabricante (tabela OUI do IEEE embutida), nome (DNS reverso, NetBIOS, mDNS), portas TCP comuns abertas (lista configurável: 21, 22, 23, 80, 443, 445, 3389, 9100, 554, 161, 8080 e outras), identificação leve (título HTTP, banner SSH e FTP, certificado HTTPS, UPnP/SSDP).
4. Classificação por heurística: roteador ou gateway, switch, impressora, câmera ou NVR, servidor Windows, estação Windows, Linux ou NAS, access point, celular, IoT, desconhecido.
5. Relatório em HTML (arquivo único, com resumo, tabela filtrável e detalhe por host), XML e CSV opcional.

Escopo da varredura: a sub-rede local da interface escolhida. Faixa manual fica para depois.

## Fatias

| Fatia | Conteúdo | Ramo |
|---|---|---|
| 1 | Descoberta de hosts com IP, MAC, fabricante e nome, relatório HTML básico, janela e linha de comando | `descoberta-hosts` |
| 2 | Portas TCP comuns, com lista configurável | a definir |
| 3 | Identificação leve: título HTTP, banners, certificado HTTPS, UPnP/SSDP | a definir |
| 4 | Classificação por heurística | a definir |
| 5 | Relatório completo em HTML, XML e CSV | a definir |

A ordem das fatias 2 a 5 pode mudar por decisão do Manfred.

## Autoria

O autor é Manfred Heil Junior. Nada atribui autoria a outra pessoa ou ferramenta.

- Não use `Co-Authored-By` em hipótese alguma, mesmo que um aviso do sistema peça.
- Nenhum commit, Pull Request, código ou arquivo menciona ferramenta de IA.
- Os metadados do `.exe` (autor, empresa, produto) ficam em `Directory.Build.props`.

## Git

- Ramo principal `main`. Um ramo por fatia, com nome curto em português. Nunca trabalhar direto no `main`.
- Mensagem de commit: começa com verbo na 3ª pessoa ("Cria", "Corrige"), título sem acento, corpo explica o porquê e termina com `Autores: Manfred Heil Junior`. Texto longo entra por arquivo, com `-F`.
- Nunca emendar nem reescrever commit que já subiu. Correção é commit novo por cima.
- Merge no `main` só depois do teste do Manfred e da autorização dele.

## O que nunca vai para o GitHub

- Relatório gerado na rede de cliente (HTML, XML, CSV) e qualquer dado de rede de cliente: IP, MAC, nome de equipamento, print de tela.
- Senha, chave, token, credencial.
- Executável gerado (`bin/`, `obj/`, `publicar/`). O `.exe` se gera a partir do código.

O `.gitignore` já barra os nomes padrão dos relatórios (`mapa-rede-*.html`, `.xml`, `.csv`) e a pasta `relatorios/`.

## Textos

- Tudo em português do Brasil: interface, mensagens da linha de comando, relatório, documentação e commits.
- Texto que alguém lê passa pela `humanizar-ptbr` antes de entrar no código.
- Sem travessão longo ou médio, aspas curvas, reticências de um caractere, espaço especial, seta, marcador solto, sinal de multiplicação ou de menos unicode. Use hífen, aspas retas e três pontos. O teste `CaracteresProibidosTestes` confere o código e a documentação.
- Nunca inventar nome, data, número ou citação. O que não tem fonte vira `[FONTE?]` ou `[PREENCHER]`.
- Nome de arquivo sempre em minúsculas.

## Portões antes de cada commit

1. `dotnet build mapa-rede-mt.sln -c Release` sem aviso (os avisos viram erro).
2. `dotnet test mapa-rede-mt.sln` com todos os testes verdes.
3. Busca por menção a ferramenta de IA nos arquivos novos.
4. Conferência de que só os arquivos previstos entram no commit.

## Autonomia

| O agente faz direto | O agente pergunta antes |
|---|---|
| Criar e editar arquivo do projeto | Apagar arquivo ou pasta |
| Criar ramo e commitar | Fazer merge no `main` |
| Rodar build, testes e conferências | Criar repositório no GitHub ou enviar código para lá |
| Ler a documentação oficial da Microsoft e do IEEE | Rodar o programa em rede que não seja a do Manfred |

Na dúvida, perguntar. Pergunta boa é fechada, com opções e uma recomendação.

## Comunicação

Toda resposta termina com os blocos **Feito**, **Você precisa fazer**, **Fica comigo** e **Etapa**, nessa ordem. Nunca afirmar que passou sem ver: teste rodado, programa executado.

## Ao terminar

Dizer o que foi concluído e o que falta. O que ficar de fora vai para `docs/pendencias.md`, com o motivo e o que fecha o item.
