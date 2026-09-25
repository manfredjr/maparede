# MapaRede - MT

Scanner de inventário de rede para Windows, da MT - Manfred Tecnologia. Roda na rede do cliente e mostra quais equipamentos estão ligados nela, com IP, MAC, fabricante e nome, num relatório HTML que abre em qualquer navegador.

Página do programa: **https://maparede.manfred.com.br**. Download da versão mais recente: [maparede.exe](https://github.com/manfredjr/maparede/releases/latest/download/maparede.exe).

Software livre, sob licença [GPL-3.0](LICENSE).

Não é scanner de vulnerabilidade: o programa não testa senha nem tenta entrar em nada. Ele só registra o que a rede já mostra para qualquer computador ligado nela.

As regras do projeto estão no [`AGENTS.md`](AGENTS.md) e o desenho em [`docs/superpowers/specs/2026-09-25-maparede-design.md`](docs/superpowers/specs/2026-09-25-maparede-design.md).

## Situação do projeto

| Fatia | Conteúdo | Ramo | Pull Request | Situação |
|---|---|---|---|---|
| 0 | Estrutura do método da MT: regras, ganchos, CI, spec | `metodo-mt` | [PREENCHER] | Em revisão |
| 1 | Descoberta de hosts (ping e ARP), MAC, fabricante, nome (DNS reverso, NetBIOS, mDNS), relatório HTML básico, janela e linha de comando | `descoberta-hosts` | [PREENCHER] | Em teste |
| 1b | Página maparede.manfred.com.br, código aberto (GPL-3.0), nome MapaRede - MT, publicação do .exe pelo GitHub Releases | `pagina-e-codigo-aberto` | [PREENCHER] | Em revisão |
| 2 | Portas TCP comuns, com lista configurável | a definir | - | A fazer |
| 3 | Identificação leve: título HTTP, banners SSH e FTP, certificado HTTPS, UPnP/SSDP | a definir | - | A fazer |
| 4 | Classificação por heurística (roteador, impressora, câmera, servidor...) | a definir | - | A fazer |
| 5 | Relatório completo em HTML, XML e CSV | a definir | - | A fazer |

O que ficou de fora de cada fatia está em [`docs/superpowers/pendencias.md`](docs/superpowers/pendencias.md).

## Como usar

O programa é um arquivo só, `maparede.exe`. Não precisa instalar nada no computador do cliente, nem o .NET, e não pede administrador.

### Pela janela

1. Abra o `maparede.exe`.
2. Escolha a interface de rede. A primeira da lista é a que tem gateway, normalmente a certa.
3. Clique em **Iniciar varredura**. A lista vai se enchendo enquanto os hosts respondem.
4. No fim, o relatório é gravado em `Documentos\MapaRede - MT`. O botão **Abrir relatório** abre no navegador, e **Salvar relatório como...** grava em outro lugar.

### Pela linha de comando

```bat
maparede --interfaces
maparede --varrer
maparede --varrer --interface 2 --saida C:\Relatorios --abrir
maparede --ajuda
```

| Opção | Para que serve |
|---|---|
| `--interfaces` | Lista as interfaces ativas, numeradas |
| `--varrer` | Varre a sub-rede e grava o relatório HTML |
| `--interface <n ou nome>` | Escolhe a interface pelo número de `--interfaces`, pelo nome ou pelo IP |
| `--saida <pasta ou arquivo.html>` | Onde gravar o relatório. Padrão: a pasta atual |
| `--abrir` | Abre o relatório no navegador ao terminar |
| `--tempo-ping <ms>` | Espera de cada ping, de 100 a 10000. Padrão: 1000 |
| `--paralelo <n>` | Endereços sondados ao mesmo tempo, de 1 a 256. Padrão: 64 |
| `--sem-arp` | Descobre hosts só pelo ping |

O `.exe` é de janela, então o Prompt de Comando devolve o cursor antes de o programa terminar. Para esperar o fim, use `start /wait maparede --varrer` no Prompt de Comando ou termine a linha com `| Out-Host` no PowerShell. Redirecionar para arquivo (`> saida.txt`) funciona direto, em UTF-8.

Códigos de saída: `0` concluído, `1` erro nos argumentos, `2` interface não encontrada, `3` falha, `4` interrompido com Ctrl+C.

## O que a fatia 1 faz

- **Interfaces:** lê as placas ativas com IPv4 e calcula a sub-rede pelo IP e pela máscara. Adaptadores virtuais (Hyper-V, VMware, VPN) vão para o fim da lista.
- **Descoberta:** cada endereço da sub-rede recebe um ping e um pedido ARP (`SendARP` do Windows), 64 de cada vez. Responder a qualquer um dos dois basta, então o host que bloqueia ping no firewall também aparece.
- **Fabricante:** pelos três primeiros bytes do MAC, na tabela OUI do IEEE embutida no programa (35.084 prefixos). MAC com o bit de administração local ligado aparece como "MAC aleatório (privativo)", que é o caso do celular com endereço privado.
- **Nome:** DNS reverso, NetBIOS (o mesmo do `nbtstat -A`, que traz também o grupo de trabalho ou domínio) e mDNS (nomes `.local` de celulares, Macs, impressoras e aparelhos Linux).
- **Relatório HTML:** arquivo único, com resumo da varredura, cartões de contagem, tabela filtrável e ordenável, detalhe de cada host ao clicar na linha e a lista de fabricantes.

Sub-rede maior que /22 (1022 endereços) é varrida só no bloco /22 em volta do IP do computador, com aviso no relatório. A faixa manual fica para uma próxima versão.

## Primeira vez na máquina

```bat
git clone https://github.com/manfredjr/maparede.git MAPA-REDE-MT
cd MAPA-REDE-MT
git config core.hooksPath .githooks
```

O último comando liga os ganchos que enviam cada commit ao GitHub na hora. A configuração é local e não vem com o clone.

## Como compilar

O jeito mais simples de ter o `.exe` é baixar do CI: no GitHub, **Actions**, a execução do commit e, no fim da página, **Artifacts**. Todo Pull Request e todo push no `main` geram um.

Para compilar na máquina, precisa do [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) no Windows.

```bat
ferramentas\publicar.cmd
```

O roteiro roda os testes e, se passarem, gera `publicar\maparede.exe` (cerca de 67 MB, já com o .NET dentro).

Comandos avulsos:

```bat
dotnet build maparede.sln -c Release
dotnet test maparede.sln -c Release
dotnet publish src\maparede\maparede.csproj -c Release -o publicar
```

## Publicação

São duas publicações, que não se misturam:

| O quê | Para onde | Como |
|---|---|---|
| O programa (`maparede.exe`) | GitHub Releases | Enviar a marca da versão (`git tag v0.1.0` e `git push origin v0.1.0`). O CI testa, gera o `.exe` e publica a Release |
| A página (pasta `public/`) | maparede.manfred.com.br | Git Version Control do cPanel, com o `.cpanel.yml` |

O passo a passo, com "Antes de publicar" e "Depois de publicar", está em [`docs/publicacao.md`](docs/publicacao.md).

## Tabela de fabricantes

A tabela fica em `src/maparede.nucleo/dados/oui.txt.gz`. Para atualizar a partir do site do IEEE:

```bat
powershell -ExecutionPolicy Bypass -File ferramentas\atualizar-oui.ps1
```

Depois, compile e rode os testes de novo antes do commit.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/maparede.nucleo` | Biblioteca sem janela: interfaces, sub-rede, ping, ARP, OUI, nomes, relatório e linha de comando |
| `src/maparede` | Aplicativo WinForms que gera o `maparede.exe` |
| `testes/maparede.testes` | Testes do núcleo (xUnit) |
| `ferramentas/` | Roteiros de compilação e de atualização da tabela OUI |
| `public/` | Página do programa em maparede.manfred.com.br |
| `docs/superpowers/` | Desenho, planos e pendências |
| `.githooks/` | Ganchos que enviam cada commit ao GitHub |
| `.github/workflows/` | CI em Windows: compila, testa e gera o `.exe` |

## Como contribuir

Relatar problema, sugerir melhoria ou mandar código: veja o [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Licença

Distribuído sob a **[GNU General Public License v3.0](LICENSE)**. Qualquer pessoa pode usar, estudar e modificar o programa. Quem distribuir uma versão modificada precisa abrir o código dela sob a mesma licença.

## A confirmar com

Nada no momento.

## Autor

Manfred Heil Junior - MT - Manfred Tecnologia.
