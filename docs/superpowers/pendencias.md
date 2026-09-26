# Pendências

O que ficou de fora, com o motivo e o que fecha o item.

## Dia zero

| Item | Motivo | O que fecha |
|---|---|---|
| Enquadramento jurídico inicial | Texto e regra jurídica passam pela `legal-br` e não saem de memória. Pontos: a MT é controladora ou operadora dos dados da varredura, base legal, autorização do responsável pela rede, aviso ao cliente, guarda e descarte do relatório, e a frase de uso responsável da página | Análise pela `legal-br`, gravada em `docs/legal/verificacao-varredura-e-lgpd-AAAA-MM-DD.md`, e seção de privacidade da spec atualizada |
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

No tamanho padrão da janela (1180 pixels), nomes longos de fabricante (como "Aruba, a Hewlett Packard Enterprise Company") e a observação "Não responde a ping" aparecem cortados. O técnico pode alargar a coluna arrastando o título ou maximizar a janela. Fecha o item: dica com o texto inteiro ao passar o mouse, ou o painel de detalhe do host da fatia 6.
