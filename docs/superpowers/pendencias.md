# Pendências

O que ficou de fora, com o motivo e o que fecha o item.

## Dia zero

| Item | Motivo | O que fecha |
|---|---|---|
| Repositório público `manfredjr/maparede` no GitHub | A integração do ambiente de trabalho não tem permissão para criar repositório na conta do Manfred | O Manfred criar o repositório **público** vazio, sem README, e liberar o acesso do app, ou enviar os ramos da máquina dele |
| Enquadramento jurídico inicial | Texto e regra jurídica passam pela `legal-br` e não saem de memória. Pontos: a MT é controladora ou operadora dos dados da varredura, base legal, autorização do responsável pela rede, aviso ao cliente, guarda e descarte do relatório, e a frase de uso responsável da página | Análise pela `legal-br`, gravada em `docs/legal/verificacao-varredura-e-lgpd-AAAA-MM-DD.md`, e seção de privacidade da spec atualizada |
| Assinatura digital do `.exe` | Sem certificado de assinatura de código, o Windows SmartScreen avisa na primeira execução, e algum antivírus pode desconfiar de um programa que varre a rede | Decisão do Manfred sobre comprar o certificado |
| Ícone do programa | Não havia arte da MT para ícone. A página usa um `favicon.svg` provisório, nas cores da MT | Arquivo `.ico` da marca da MT, usado no `.exe` e na página |
| Primeira Release no GitHub | Depende do repositório existir, do teste no Windows e da autorização | Marca `v0.1.0` enviada, como em `docs/publicacao.md`. Até lá, o botão de download da página responde 404 |
| Primeira publicação da página | O subdomínio foi criado no cPanel antes do clone, e a pasta `repositories/maparede` pode ter nascido com `public` vazia dentro | Seguir `docs/publicacao.md`, seção 3, a partir da conferência da pasta |
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
