# Plano da fatia 1: descoberta de hosts

Ramo `descoberta-hosts`. Desenho em `docs/superpowers/specs/2026-09-25-mapa-rede-mt-design.md`.

Este plano foi escrito depois da implementação, como registro. A partir da fatia 2, o plano vem antes do código.

## Objetivo

Varrer a sub-rede local da interface escolhida e mostrar cada host com IP, MAC, fabricante e nome, num relatório HTML básico, pela janela e pela linha de comando.

## Tarefas

| # | Tarefa | Arquivos | Testes |
|---|---|---|---|
| 1 | Sub-rede: máscara, prefixo, faixa de hosts, limite de /22 | `src/mapa-rede-mt.nucleo/rede/sub-rede.cs` | `sub-rede-testes.cs` |
| 2 | Interfaces ativas com IPv4, gateway e DNS, virtuais por último | `rede/interface-rede.cs` | Conferido na varredura real |
| 3 | ARP pelo `SendARP`, com interface para simular | `rede/sonda-arp.cs` | `VarredorTestes` com ARP simulado |
| 4 | Tabela OUI embutida e MAC aleatório | `fabricantes/tabela-oui.cs`, `dados/oui.txt.gz`, `rede/endereco-mac.cs` | `fabricantes-testes.cs` |
| 5 | Nomes: DNS reverso, NetBIOS e mDNS | `nomes/*.cs` | `nomes-testes.cs`, com pacotes montados e pacote malformado |
| 6 | Varredura em duas etapas, com paralelismo e cancelamento | `varredura/*.cs` | `VarredorTestes` |
| 7 | Relatório HTML em arquivo único, com texto da rede codificado | `relatorios/relatorio-html.cs` | `relatorio-testes.cs` |
| 8 | Argumentos da linha de comando | `linha-de-comando/argumentos.cs` | `argumentos-testes.cs` |
| 9 | Janela WinForms e modo linha de comando no mesmo `.exe` | `src/mapa-rede-mt/*.cs` | Aberto no Wine; teste no Windows é do Manfred |
| 10 | Regras de texto e de nome de arquivo | `caracteres-proibidos-testes.cs` | O próprio teste |
| 11 | Roteiros de apoio | `ferramentas/publicar.cmd`, `ferramentas/atualizar-oui.ps1` | Roteiro OUI rodado no PowerShell 7 |

## Portões

Build sem aviso, 49 testes verdes, busca por menção a ferramenta de IA e conferência dos arquivos de cada commit.

## Como testar

1. Baixar o `.exe` do CI ou rodar `ferramentas\publicar.cmd`.
2. Abrir o `.exe`, escolher a interface e varrer a rede da MT.
3. Conferir no relatório o gateway, as impressoras e os celulares, com fabricante e nome.
4. No Prompt de Comando: `mt-mapa-rede --interfaces` e `start /wait mt-mapa-rede --varrer --abrir`.
