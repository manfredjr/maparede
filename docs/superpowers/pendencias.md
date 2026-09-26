# Pendências

O que ficou de fora, com o motivo e o que fecha o item.

## Dia zero

| Item | Motivo | O que fecha |
|---|---|---|
| Enquadramento jurídico inicial | Fechado em 26/09/2026. Análise em `docs/legal/verificacao-varredura-e-lgpd-2026-09-26.md` e parecer em `docs/legal/parecer-distribuicao-2026-09-26.md`. Avisos "Uso autorizado" e "Licença e garantias" na página e no README | A cada fatia, a página, o README e a tela descrevem exatamente o que a versão publicada envia (na identificação de serviços, requisição HTTP e TLS) |
| Assinatura digital do `.exe` | Sem certificado de assinatura de código, o SmartScreen avisa em todo `.exe` baixado da internet ("Fornecedor desconhecido"), e o Smart App Control, onde estiver ativo, bloqueia sem oferecer saída. Algum antivírus também pode desconfiar de um programa que varre a rede. O ícone já está em DIB, que evita o bloqueio visto no CronoAula | Decisão do Manfred sobre comprar o certificado. O tipo EV tira o aviso na hora; o OV, depois de um tempo de circulação |
| Página no endereço novo | O nome mudou para MapNet - MT em 25/09/2026, e a página passa para `mapnet.manfred.com.br`. O endereço antigo foi desligado sem redirecionamento, porque ainda não havia usuários | Seguir `docs/publicacao.md`, seção 3 |
| Logo do MapNet | A arte atual diz "MAPA REDE". A página usa só o símbolo de rede com o nome em texto | O Manfred enviar a arte com "MAPNET", de preferência com fundo transparente |
| Revisão do texto da página | O texto de `public/index.html` segue as regras de estilo, mas é texto público e merece a leitura do Manfred antes de ir ao ar | O Manfred ler a página e aprovar ou pedir ajustes |

## Fatia 1: descoberta de hosts

| Item | Motivo | O que fecha |
|---|---|---|
| Teste da janela e da varredura num Windows de verdade | A fatia foi compilada e testada fora do Windows. Os testes do núcleo e a varredura rodaram no Linux, e o `.exe` abriu no Wine, mas o Wine não lê as interfaces de rede (`GetAdaptersAddresses` sem suporte) | O Manfred rodar o `.exe` na rede da MT, pela janela e pela linha de comando, e conferir o relatório |
| Tabela OUI mais recente | O site do IEEE não estava acessível no ambiente de trabalho. A tabela embutida veio do `oui.txt` do IEEE distribuído no netaddr 1.3.0, de 2024 | Rodar `ferramentas\atualizar-oui.ps1` no Windows e fazer commit do resultado |
| Prefixos MA-M e MA-S do IEEE | A tabela tem só os blocos MA-L (24 bits), que cobrem a grande maioria dos fabricantes. Os blocos menores (28 e 36 bits) ficaram de fora | Acrescentar `mam.txt` e `oui36.txt` ao roteiro e à busca, se fizer falta na prática |
| Faixa manual e sub-rede maior que /22 | Escopo da fatia 1 é a sub-rede local. Rede maior é varrida só no /22 em volta do computador | Fatia futura de faixa manual |
| IPv6 | Fora do escopo da versão 1 | A decidir |
| Prompt de Comando devolve o cursor antes do fim | O `.exe` é de janela, para abrir sem console no clique duplo | Aceito por ora, com `start /wait` documentado no README. Revisitar se incomodar no uso |

## Tela WPF: nomes longos na tabela

Fechado em parte na fatia 3 (0.2.2). No tamanho padrão da janela, IP, Origem, MAC, Fabricante até "MAC aleatório (privativo)", Ping e as marcas sozinhas da Observação aparecem inteiros. Nome e Observação com duas marcas ("Este computador, MAC aleatório") podem ter qualquer tamanho e continuam cortados na célula, com o texto inteiro na dica. Abaixo da largura padrão aparece a rolagem lateral. O painel de detalhe do host da fatia 6 mostra o nome inteiro ao clicar na linha. Fechado.

## Fatia 3: painel "Minha máquina"

| Item | Motivo | O que fecha |
|---|---|---|
| Wi-Fi com a Localização do Windows desligada | O caso está coberto por teste com fonte simulada, mas não foi visto num Windows de verdade: no computador do teste a Localização estava ligada e o nome da rede apareceu | Abrir o programa com a Localização desligada e conferir a mensagem no bloco Wi-Fi |
| Wi-Fi de 6 GHz | A banda sai da frequência do ponto de acesso, com teste para 6 GHz, mas o teste real foi numa rede de 5 GHz | Conferir numa placa e numa rede Wi-Fi 6E |
| Consulta real ao 1.1.1.1 | Os testes não usam a rede, e o teste da tela foi no modo de demonstração, que não consulta nada. A consulta de verdade só roda no `.exe` com o clique | O Manfred clicar em **Consultar IP público** no `.exe` do PR e comparar com o que `https://1.1.1.1/cdn-cgi/trace` mostra no navegador |

## Fatia 4: diagnóstico no console

| Item | Motivo | O que fecha |
|---|---|---|
| Resultado das ferramentas no relatório | O desenho prevê levar ao relatório o resumo das ferramentas, quando o técnico pedir. A fatia 4 mostra tudo no console, e o botão Copiar leva o texto para outro lugar | Botão "levar ao relatório" em cada aba, numa fatia futura |
| ARP e rotas em IPv6 | As tabelas usam `GetIpNetTable` e `GetIpForwardTable`, só IPv4, como a varredura | Trocar pelas versões `2` se o IPv6 fizer falta no campo |
| Tracert com um pacote por salto | O `tracert` do Windows manda três por salto. Um basta para ver o caminho e deixa a ferramenta três vezes mais rápida | Voltar a três se o Manfred preferir no uso |

## Fatia 5: manutenção

| Item | Motivo | O que fecha |
|---|---|---|
| Teste das quatro ações num Windows de verdade | As ações mudam a rede do Windows e abrem o UAC, então não rodaram no teste do agente. Os testes cobrem a lista fechada, a confirmação, o UAC negado e o modo auxiliar com argumento errado | O Manfred rodar cada botão no `.exe` do PR, aceitar e recusar o UAC, e conferir a saída na aba Manutenção |
| `ipconfig /flushdns` sem administrador | O desenho deixou a confirmar. A fatia roda sem elevação, que é o comportamento conhecido do Windows 10 e 11 | Se o Windows do Manfred devolver "requer elevação", mudar `PedeAdministrador` para incluir a ação |

## Fatia 6: detalhe do host

| Item | Motivo | O que fecha |
|---|---|---|
| Portas abertas no detalhe | Fechado na fatia 7 (0.3.0): o detalhe mostra as portas abertas com o serviço, ou o motivo de não terem sido verificadas | Fechado |
| Esc e novo clique na linha testados só no modelo | O agente não teve acesso ao teclado e ao mouse na janela de teste. A lógica de fechar está coberta por teste, e a ligação na janela é curta | O Manfred abrir e fechar o detalhe com Esc e com novo clique |

## Fatia 7: portas TCP

| Item | Motivo | O que fecha |
|---|---|---|
| Portas desligadas por padrão | O plano da versão 1.0 previa as portas ligadas, com `--sem-portas` para desligar. A análise jurídica de 26/09/2026 recomendou pedir o escopo antes da sondagem e deixar aparelhos pessoais de fora por padrão. A fatia seguiu a recomendação: a caixa começa desmarcada, a primeira varredura com portas em cada rede pede confirmação e a linha de comando liga com `--portas` | Fechado em 26/09/2026: o Manfred confirmou as portas desligadas ao abrir, com confirmação por rede |
| Portas numa rede de verdade | A etapa foi testada com sonda simulada e no modo de demonstração. Não rodou numa rede de verdade no teste do agente | O Manfred varrer a rede da MT com as portas ligadas e conferir, por exemplo, a 80 ou 443 no roteador, a 9100 numa impressora e a 445 num Windows com compartilhamento |
| Confirmação vale para a sessão | A pergunta aparece uma vez por sub-rede enquanto o programa está aberto. Fechar e abrir de novo pergunta outra vez | Guardar a confirmação, se o Manfred achar a pergunta repetitiva no uso |

## Fatia 8: identificação de serviços

| Item | Motivo | O que fecha |
|---|---|---|
| Linha de comando com a identificação ligada por padrão | O plano previa `--identificar` para ligar. Com a decisão de 26/09/2026 de deixar **Identificar serviços** marcado na janela, a linha de comando segue o mesmo padrão: `--portas` já identifica e `--sem-identificar` desliga | O Manfred confirmar ou pedir `--identificar` |
| HTTP pedido pelo IP | O `GET /` vai com o IP no cabeçalho `Host`. Servidor que hospeda vários sites por nome pode mostrar uma página genérica | Usar o nome do DNS reverso no `Host`, se fizer falta no campo |
| Janela da identificação vista só no modo de demonstração | A identificação de verdade rodou na rede do Manfred pela linha de comando, com roteador, câmera, TV e Windows respondendo. A janela foi conferida com dados de exemplo | O Manfred varrer pela janela com as portas ligadas |

## Página de privacidade

| Item | Motivo | O que fecha |
|---|---|---|
| Prazo de guarda dos registros de acesso do site | A página de privacidade diz que a hospedagem e a Cloudflare registram IP, data, página e navegador de cada acesso, mas não diz por quanto tempo: o prazo depende da configuração do cPanel e da conta da Cloudflare, que o agente não vê | Fechado em 26/09/2026: no cPanel, Acesso bruto, os logs são arquivados e o arquivo do mês é removido no fim do mês seguinte. A página informa esse prazo e remete a Cloudflare à política de privacidade dela |
