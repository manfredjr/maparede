# MapNet - MT

Scanner de inventário de rede para Windows, da MT - Manfred Tecnologia. Roda na rede do cliente e mostra quais equipamentos estão ligados nela, com IP, MAC, fabricante e nome, num relatório HTML que abre em qualquer navegador.

Página do programa: **https://mapnet.manfred.com.br**. Download da versão mais recente: [mapnet.exe](https://github.com/manfredjr/mapnet/releases/latest/download/mapnet.exe).

Software livre, sob licença [GPL-3.0](LICENSE).

Não é scanner de vulnerabilidade: o programa não testa senha nem tenta entrar em nada. Ele só registra o que a rede já mostra para qualquer computador ligado nela.

As regras do projeto estão no [`AGENTS.md`](AGENTS.md) e o desenho em [`docs/superpowers/specs/2026-09-25-mapnet-design.md`](docs/superpowers/specs/2026-09-25-mapnet-design.md).

## Situação do projeto

| Fatia | Conteúdo | Ramo | Pull Request | Situação |
|---|---|---|---|---|
| 0 | Estrutura do método da MT: regras, ganchos, CI, spec | `metodo-mt` | [#1](https://github.com/manfredjr/mapnet/pull/1) | Concluída |
| 1 | Descoberta de hosts (ping e ARP), MAC, fabricante, nome (DNS reverso, NetBIOS, mDNS), relatório HTML básico, janela e linha de comando | `descoberta-hosts` | [#2](https://github.com/manfredjr/mapnet/pull/2) | Publicada na v0.1.2 |
| 1b | Página mapnet.manfred.com.br, código aberto (GPL-3.0), nome MapNet - MT, publicação do .exe pelo GitHub Releases | `pagina-e-codigo-aberto` | [#3](https://github.com/manfredjr/mapnet/pull/3) | Concluída |
| 2 | Tela WPF do painel do técnico, com a identidade da MT (a v0.2.0 fechava no fim da varredura e teve a Release retirada, a correção saiu na v0.2.1) | `tela-wpf` | [#10](https://github.com/manfredjr/mapnet/pull/10), [#12](https://github.com/manfredjr/mapnet/pull/12), [#13](https://github.com/manfredjr/mapnet/pull/13) | Publicada na v0.2.1 |
| 3 | Painel "Minha máquina" completo: nome, domínio, DHCP, Wi-Fi e IP público sob demanda, e tabela de hosts sem texto cortado (sai como v0.2.2) | `minha-maquina` | Plano em [#14](https://github.com/manfredjr/mapnet/pull/14), código em [#16](https://github.com/manfredjr/mapnet/pull/16) | Em teste |
| 4 | Console de diagnóstico: ping, tracert, DNS, ARP, conexões, rotas e `ipconfig /all` | a definir | - | A fazer |
| 5 | Ações de manutenção com elevação sob demanda | a definir | - | A fazer |
| 6 | Painel de detalhe do host | a definir | - | A fazer |
| 7 em diante | Portas TCP, identificação leve, classificação e relatório completo em HTML, XML e CSV | a definir | - | A fazer |

A versão 0.2 segue o desenho em [`docs/superpowers/specs/2026-09-25-mapnet-0.2-painel-do-tecnico-design.md`](docs/superpowers/specs/2026-09-25-mapnet-0.2-painel-do-tecnico-design.md).

O que ficou de fora de cada fatia está em [`docs/superpowers/pendencias.md`](docs/superpowers/pendencias.md).

## Como usar

O programa é um arquivo só, `mapnet.exe`. Não precisa instalar nada no computador do cliente, nem o .NET, e não pede administrador.

### Pela janela

1. Abra o `mapnet.exe`.
2. Na faixa verde do topo, escolha a interface de rede. A primeira da lista é a que tem gateway, quase sempre a certa. A coluna **Minha máquina** mostra o computador (nome, grupo de trabalho ou domínio, usuário), a placa (tipo, MAC, velocidade, MTU), os endereços (IPv4, IPv6, gateway, DNS, sufixo e a sub-rede que vai ser varrida) e o DHCP com a validade da concessão. No Wi-Fi, mostra também a rede, a banda, o canal e o sinal.
3. O botão **Consultar IP público** pergunta o IP ao serviço `1.1.1.1`, do Cloudflare, e só roda com o clique. Se o relatório for gravado depois da consulta, o IP público vai junto.
4. Clique em **Iniciar varredura**. A tabela vai se enchendo enquanto os hosts respondem, e o mesmo botão vira **Cancelar**. O campo **Filtrar** procura por IP, nome, MAC, fabricante ou observação, e cada coluna ordena com um clique no título.
5. O console embaixo registra o andamento e os avisos, e a barra de estado mostra o resumo. O console tem também uma aba para cada ferramenta: Ping (normal ou contínuo), Tracert, DNS com escolha do servidor, ARP, Conexões e Rotas. Os campos de host e de servidor DNS já vêm com o gateway e o DNS da interface. Enquanto a ferramenta roda, **Executar** vira **Parar**. **Copiar** e **Limpar** valem para a aba aberta.
6. No fim, o relatório é gravado em `Documentos\MapNet - MT`. O botão **Relatório** abre o relatório no navegador, salva em outro lugar ou abre a pasta.

### Pela linha de comando

```bat
mapnet --interfaces
mapnet --varrer
mapnet --varrer --interface 2 --saida C:\Relatorios --abrir
mapnet --ajuda
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

O `.exe` é de janela, então o Prompt de Comando devolve o cursor antes de o programa terminar. Para esperar o fim, use `start /wait mapnet --varrer` no Prompt de Comando ou termine a linha com `| Out-Host` no PowerShell. Redirecionar para arquivo (`> saida.txt`) funciona direto, em UTF-8.

Códigos de saída: `0` concluído, `1` erro nos argumentos, `2` interface não encontrada, `3` falha, `4` interrompido com Ctrl+C.

## Primeira execução

**O Windows pode avisar antes de abrir.** O executável não tem assinatura digital, e todo arquivo baixado da internet recebe do navegador a **Marca da Web** (*Mark of the Web*), que faz o Windows mostrar "O Windows protegeu o seu computador" (SmartScreen). O arquivo está perfeito; o aviso vem da falta de assinatura. Para abrir, escolha um caminho:

1. Na tela de aviso, clique em **Mais informações** e depois em **Executar assim mesmo**.
2. Clique com o botão direito no arquivo, abra **Propriedades**, marque **Desbloquear** e confirme em **OK**.
3. Pelo PowerShell, na pasta do arquivo:

```powershell
Unblock-File .\mapnet.exe
```

Copiar o `.exe` por pen drive ou pasta de rede não aplica a Marca da Web, e o programa abre direto. Cada versão nova começa sem reputação no SmartScreen, então o aviso tende a aparecer nos primeiros dias depois de cada publicação.

Se o computador tiver o **Controle de Aplicativo Inteligente** (Smart App Control) ligado, em **Segurança do Windows**, **Controle de aplicativos e navegador**, o bloqueio não oferece a opção de executar assim mesmo. Nesse caso, só a assinatura digital resolve.

**A primeira execução demora alguns segundos**, porque o Windows descompacta o conteúdo do executável numa pasta temporária. Da segunda vez em diante, abre rápido.

### Conferir se o arquivo chegou íntegro

Cada versão publicada leva um arquivo `mapnet.exe.sha256.txt`. Na máquina que recebeu o `.exe`:

```powershell
Get-FileHash .\mapnet.exe -Algorithm SHA256
```

Se o valor bater com o do `.txt`, o executável é exatamente o que foi publicado.

## O que a fatia 1 faz

- **Interfaces:** lê as placas ativas com IPv4 e calcula a sub-rede pelo IP e pela máscara. Adaptadores virtuais (Hyper-V, VMware, VPN) vão para o fim da lista.
- **Descoberta:** cada endereço da sub-rede recebe um ping e um pedido ARP (`SendARP` do Windows), 64 de cada vez. Responder a qualquer um dos dois basta, então o host que bloqueia ping no firewall também aparece.
- **Fabricante:** pelos três primeiros bytes do MAC, na tabela OUI do IEEE embutida no programa (35.084 prefixos). MAC com o bit de administração local ligado aparece como "MAC aleatório (privativo)", que é o caso do celular com endereço privado.
- **Nome:** DNS reverso, NetBIOS (o mesmo do `nbtstat -A`, que traz também o grupo de trabalho ou domínio) e mDNS (nomes `.local` de celulares, Macs, impressoras e aparelhos Linux).
- **Relatório HTML:** arquivo único, com resumo da varredura, cartões de contagem, tabela filtrável e ordenável, detalhe de cada host ao clicar na linha e a lista de fabricantes.

Sub-rede maior que /22 (1022 endereços) é varrida só no bloco /22 em volta do IP do computador, com aviso no relatório. A faixa manual fica para uma próxima versão.

## Primeira vez na máquina

```bat
git clone https://github.com/manfredjr/mapnet.git MAPNET-MT
cd MAPNET-MT
git config core.hooksPath .githooks
```

O último comando liga os ganchos que enviam cada commit ao GitHub na hora. A configuração é local e não vem com o clone.

## Como compilar

O jeito mais simples de ter o `.exe` é baixar do CI: no GitHub, **Actions**, a execução do commit e, no fim da página, **Artifacts**. Todo Pull Request e todo push no `main` geram um.

Para compilar na máquina, precisa do [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) no Windows.

```bat
ferramentas\publicar.cmd
```

O roteiro roda os testes e, se passarem, gera `publicar\mapnet.exe` (cerca de 67 MB, já com o .NET dentro).

Para ver a tela com dados de exemplo, sem varrer nem consultar nada, abra `mapnet --demonstracao`. As imagens da documentação saem dele, com endereços reservados para documentação.

Comandos avulsos:

```bat
dotnet build mapnet.sln -c Release
dotnet test mapnet.sln -c Release
dotnet publish src\mapnet\mapnet.csproj -c Release -o publicar
```

## Publicação

São duas publicações, que não se misturam:

| O quê | Para onde | Como |
|---|---|---|
| O programa (`mapnet.exe`) | GitHub Releases | Enviar a marca da versão (`git tag v0.1.0` e `git push origin v0.1.0`). O CI testa, gera o `.exe` e publica a Release |
| A página (pasta `public/`) | mapnet.manfred.com.br | Git Version Control do cPanel, com o `.cpanel.yml` |

O passo a passo, com "Antes de publicar" e "Depois de publicar", está em [`docs/publicacao.md`](docs/publicacao.md).

## Tabela de fabricantes

A tabela fica em `src/mapnet.nucleo/dados/oui.txt.gz`. Para atualizar a partir do site do IEEE:

```bat
powershell -ExecutionPolicy Bypass -File ferramentas\atualizar-oui.ps1
```

Depois, compile e rode os testes de novo antes do commit.

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `src/mapnet.nucleo` | Biblioteca sem janela: interfaces, sub-rede, ping, ARP, OUI, nomes, relatório e linha de comando |
| `src/mapnet` | Aplicativo WPF que gera o `mapnet.exe`, com a tela, o tema da MT e a fonte Montserrat |
| `testes/mapnet.testes` | Testes do núcleo e da lógica da tela (xUnit) |
| `ferramentas/` | Roteiros de compilação e de atualização da tabela OUI |
| `public/` | Página do programa em mapnet.manfred.com.br |
| `docs/superpowers/` | Desenho, planos e pendências |
| `.githooks/` | Ganchos que enviam cada commit ao GitHub |
| `.github/workflows/` | CI em Windows: compila, testa e gera o `.exe` |

## Como contribuir

Relatar problema, sugerir melhoria ou mandar código: veja o [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Licença

Distribuído sob a **[GNU General Public License v3.0](LICENSE)**. Qualquer pessoa pode usar, estudar e modificar o programa. Quem distribuir uma versão modificada precisa abrir o código dela sob a mesma licença.

A fonte Montserrat, embutida no `.exe`, é do Montserrat Project e vai sob a SIL Open Font License 1.1, em [`src/mapnet/recursos/fontes/ofl.txt`](src/mapnet/recursos/fontes/ofl.txt).

## A confirmar com

Nada no momento.

## Autor

Manfred Heil Junior - MT - Manfred Tecnologia.
