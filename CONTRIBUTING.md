# Como contribuir com o MapNet - MT

O MapNet nasceu do atendimento a redes de pequenas e médias empresas. Se você usa o programa nesse trabalho, relatar o que atrapalha já é contribuir, mesmo sem escrever uma linha de código.

## Sem escrever código

**Relatar um problema.** Abra uma *issue* em [Issues](../../issues) contando o que aconteceu. Ajuda muito informar:

- a versão do Windows (10 ou 11);
- se a varredura foi pela janela ou pela linha de comando;
- o que você esperava ver e o que apareceu;
- se dá para repetir o problema e como.

**Nunca anexe o relatório de uma rede real** nem print com IP, MAC ou nome de equipamento de cliente. A issue é pública. Se precisar mostrar um caso, troque os dados por exemplos.

**Sugerir uma melhoria.** Também pelas *issues*. Explique a situação de atendimento em que a mudança faria diferença.

## Escrevendo código

### O que você precisa

- [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 ou 11, 64 bits, para rodar o programa. Os testes do núcleo rodam também no Linux.
- Um editor: Visual Studio 2022, Rider ou VS Code

### Preparar o ambiente

```bash
git clone https://github.com/manfredjr/mapnet.git
```

```bash
dotnet build mapnet.sln -c Release
```

```bash
dotnet test mapnet.sln -c Release
```

Os testes devem passar numa cópia limpa antes de você mexer em qualquer coisa. Se algum falhar, isso já é uma issue.

### Gerar o executável

```bat
ferramentas\publicar.cmd
```

O roteiro roda os testes e recusa gerar o `.exe` se algum falhar. O resultado sai em `publicar\mapnet.exe`.

### O fluxo

1. Faça um *fork* do repositório.
2. Crie um ramo com nome descritivo: `git checkout -b portas-configuraveis`.
3. Faça as alterações, com commits pequenos e mensagens claras.
4. Garanta que `dotnet test` continua passando.
5. Abra um *pull request* explicando **o problema de atendimento** que a mudança resolve, e não só o que o código faz.

### Regras da casa

**Só olhar, nunca entrar.** O MapNet levanta o que a rede já mostra. Contribuição que teste senha, explore falha, tente login ou mude configuração de equipamento não será aceita.

**Sem internet e sem telemetria.** O programa não manda nada para fora da rede local e não coleta dado de uso. Isso é promessa feita a quem usa.

**Sem administrador.** O programa roda como usuário comum. O que exigir elevação precisa de conversa antes, numa issue.

**Sem dependência externa.** Um arquivo só, sem Npcap, sem Nmap e sem instalador. Um pacote NuGet novo precisa de justificativa forte.

**Testes acompanham a lógica.** Tudo em `src/mapnet.nucleo` é testável sem janela, e é assim que deve continuar. Mudou a interpretação de um pacote, uma conta de sub-rede ou o relatório? Traga o teste junto.

**Português nos textos.** Interface, relatório, comentários e nomes de código são em português do Brasil. O teste `CaracteresProibidosTestes` confere a regra de caracteres.

### Antes de abrir um PR grande

Se a ideia for grande (um recurso novo, uma mudança de arquitetura), abra uma issue antes para conversarmos. As próximas fatias previstas estão na "Situação do projeto" do [README](README.md).

## Licença das contribuições

O MapNet - MT é distribuído sob a [GNU General Public License v3.0](LICENSE). Ao enviar um pull request, você concorda que sua contribuição seja licenciada nos mesmos termos.
