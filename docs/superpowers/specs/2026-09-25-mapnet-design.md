# MapNet - MT: desenho da versão 1

Data: 25/09/2026. Autor: Manfred Heil Junior.

Este desenho registra o pedido do Manfred de 25/09/2026 e as decisões tomadas na fatia 1. A fatia 1 foi construída antes deste arquivo existir. A partir da fatia 2, o desenho de cada fatia vem antes do código, como pede o método.

## 1. O que é

Programa para Windows que o Manfred roda na rede do cliente para fazer o inventário dela: quais equipamentos estão ligados, com IP, MAC, fabricante, nome, portas comuns abertas e tipo provável. O resultado é um relatório que ele guarda e entrega.

Não é scanner de vulnerabilidade. Não testa senha, não contorna autenticação e não explora vulnerabilidade.

## 2. Para quem

- **Quem usa:** o Manfred e, no futuro, técnicos da MT, no atendimento a pequenas e médias empresas.
- **Quem recebe o resultado:** o cliente, pelo relatório.

## 3. Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Linguagem | C# com .NET 8 | Acesso direto às APIs de rede do Windows, janela nativa e `.exe` único |
| Entrega | `.exe` único, autocontido, `win-x64` | Rodar no notebook do técnico sem instalar nada |
| Dependências | Sem Npcap e sem Nmap | Nada para instalar nem para explicar ao cliente |
| Permissão | Sem administrador sempre que der | Notebook de técnico e máquina de cliente nem sempre dão elevação |
| Interface | Janela WinForms e linha de comando no mesmo `.exe` | Janela para o dia a dia, linha de comando para roteiro e agendamento |
| Escopo | Sub-rede local da interface escolhida | Faixa manual fica para depois |
| Descoberta | Ping em paralelo e ARP pelo `SendARP` do `iphlpapi` | O ARP pega também quem bloqueia ping, e não pede administrador |
| Fabricante | Tabela OUI do IEEE embutida | Funciona sem internet na rede do cliente |
| Nome | DNS reverso, NetBIOS e mDNS | Cada tipo de aparelho responde a um deles |
| Relatório | HTML em arquivo único, XML e CSV opcional | HTML para ler e entregar, XML e CSV para outros sistemas |
| Nome | MapNet - MT, executável `mapnet.exe` | Decisão do Manfred em 25/09/2026. O nome anterior, MapaRede - MT, foi trocado no mesmo dia |
| Código | Aberto, repositório público `manfredjr/mapnet`, licença GPL-3.0 | Decisão do Manfred em 25/09/2026, no mesmo modelo do CronoAula |
| Página | `mapnet.manfred.com.br`, estática, na pasta `public/`, publicada pelo Git do cPanel | Decisão do Manfred em 25/09/2026. O `.exe` é baixado do GitHub Releases |

## 4. Arquitetura

- **Núcleo** (`src/mapnet.nucleo`, `net8.0`): toda a lógica, sem janela. Testável no Windows e no Linux.
- **Aplicativo** (`src/mapnet`, `net8.0-windows`): janela WinForms e ponto de entrada da linha de comando. Só chama o núcleo.
- **Testes** (`testes/mapnet.testes`): xUnit sobre o núcleo. O ARP entra por interface (`ISondaArp`) para ser simulado.

A varredura tem duas etapas. Na descoberta, cada endereço recebe ping e ARP, com paralelismo limitado. Na identificação, cada host encontrado passa pelas consultas de nome, e nas fatias seguintes pelas portas, pela identificação leve e pela classificação.

Não há banco de dados. O resultado de uma varredura vive na memória e sai em arquivo.

## 5. Privacidade e segredos

- **Dado pessoal:** o relatório pode trazer nome de aparelho com nome de pessoa ("iPhone-de-Maria") e MAC de aparelho pessoal. Isso é dado pessoal pela LGPD. A MT faz a varredura a pedido do cliente, o que pode fazer dela operadora, e não controladora. O enquadramento, o aviso ao cliente, a guarda e o descarte dependem da análise da `legal-br`, que é pendência (ver `pendencias.md`).
- **Autorização da varredura:** a varredura só se faz em rede cujo responsável autorizou. A forma dessa autorização (termo, cláusula de contrato, aceite na tela) também depende da `legal-br`.
- **Onde o dado fica:** só na máquina do técnico, em `Documentos\MapNet - MT`. O programa não envia nada para fora da rede local e o repositório nunca recebe relatório.
- **Segredos:** o programa não usa senha, chave nem token. O certificado de assinatura de código, se vier, fica fora do repositório.

## 6. Ambiente e publicação

Windows 10 ou 11 de 64 bits, com o SDK do .NET 8 para compilar. O CI roda em Windows no GitHub Actions, guarda o `.exe` como artefato e, quando chega uma marca de versão, publica a Release. A página é o único item no servidor: arquivos estáticos, sem PHP e sem banco.

## 7. Ordem das fatias

| Fatia | Conteúdo |
|---|---|
| 1 | Descoberta de hosts com IP, MAC, fabricante e nome, relatório HTML básico, janela e linha de comando |
| 2 | Portas TCP comuns abertas, com lista configurável (21, 22, 23, 80, 443, 445, 3389, 9100, 554, 161, 8080 e outras) |
| 3 | Identificação leve: título HTTP, banner SSH e FTP, certificado HTTPS, UPnP/SSDP |
| 4 | Classificação por heurística: roteador ou gateway, switch, impressora, câmera ou NVR, servidor Windows, estação Windows, Linux ou NAS, access point, celular, IoT, desconhecido |
| 5 | Relatório completo em HTML (resumo, tabela filtrável, detalhe por host), XML e CSV opcional |

A ordem das fatias 2 a 5 pode mudar por decisão do Manfred.

## 8. Prioridade dos testes

1. **Não passar do limite:** a varredura nunca sai da sub-rede escolhida nem do tamanho máximo, e nunca tenta mais do que olhar.
2. **Interpretação de pacote:** NetBIOS, mDNS e, nas próximas fatias, banners e respostas UPnP. Pacote malformado não pode travar nem derrubar o programa.
3. **Relatório:** todo texto que vem da rede sai codificado, e o texto fixo segue as regras de caractere.
4. **Contas:** sub-rede, máscara, prefixo e faixa de hosts.

## 9. Fora da versão 1

- Faixa manual e varredura de outras sub-redes.
- IPv6.
- Qualquer teste de vulnerabilidade, senha ou exploração.
- Varredura agendada ou contínua e comparação entre varreduras.
- Envio do relatório por e-mail ou para servidor.
