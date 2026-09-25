# Pendências

O que ficou de fora de cada fatia, com o motivo e o que fecha o item.

## Fatia 1: descoberta de hosts

| Item | Motivo | O que fecha |
|---|---|---|
| Teste da janela e da varredura num Windows de verdade | A fatia foi compilada e testada fora do Windows. Os testes do núcleo e a varredura rodaram no Linux, e o `.exe` abriu no Wine, mas o Wine não lê as interfaces de rede (`GetAdaptersAddresses` sem suporte) | O Manfred rodar o `.exe` na rede da MT, pela janela e pela linha de comando, e conferir o relatório |
| Repositório no GitHub | Criar repositório e enviar código são ações que pedem autorização | O Manfred autorizar a criação de `manfredjr/mapa-rede-mt`, privado |
| Arquivo `LICENSE` | Texto jurídico passa pela `legal-br` e não sai de memória | Verificação jurídica da licença proprietária |
| Relatório e LGPD | O relatório pode trazer dado pessoal: nome de celular com nome de pessoa ("iPhone-de-Maria") e MAC ligado a um aparelho pessoal. Cabe definir o aviso ao cliente, a guarda e o descarte dos relatórios | Análise pela `legal-br`, com o arquivo de verificação em `docs/legal/` |
| Assinatura digital do `.exe` | Sem certificado de assinatura de código, o Windows SmartScreen avisa na primeira execução, e algum antivírus pode desconfiar de um programa que varre a rede | Decisão do Manfred sobre comprar certificado de assinatura de código |
| Ícone do programa | Não havia arte da MT para ícone | Arquivo `.ico` da marca da MT |
| Tabela OUI mais recente | O site do IEEE não estava acessível no ambiente de trabalho. A tabela embutida veio do `oui.txt` do IEEE distribuído no netaddr 1.3.0, de 2024 | Rodar `ferramentas\atualizar-oui.ps1` no Windows e fazer commit do resultado |
| Prefixos MA-M e MA-S do IEEE | A tabela tem só os blocos MA-L (24 bits), que cobrem a grande maioria dos fabricantes. Os blocos menores (28 e 36 bits) ficaram de fora | Acrescentar `mam.txt` e `oui36.txt` ao roteiro e à busca, se fizer falta na prática |
| Faixa manual e sub-rede maior que /22 | Escopo da fatia 1 é a sub-rede local. Rede maior é varrida só no /22 em volta do computador | Fatia futura de faixa manual |
| IPv6 | Fora do escopo definido | A decidir |
| Prompt de Comando devolve o cursor antes do fim | O `.exe` é de janela, para abrir sem console no clique duplo | Aceito por ora, com `start /wait` documentado no README. Revisitar se incomodar no uso |
