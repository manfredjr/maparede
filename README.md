# MT Mapa de Rede

Scanner de inventário de rede para Windows, da MT - Manfred Tecnologia. Roda na rede do cliente e mostra quais equipamentos estão ligados nela, num relatório que abre em qualquer navegador.

Não é scanner de vulnerabilidade: o programa não testa senha nem tenta entrar em nada.

As regras do projeto estão no [`AGENTS.md`](AGENTS.md) e o desenho em [`docs/superpowers/specs/2026-09-25-mapa-rede-mt-design.md`](docs/superpowers/specs/2026-09-25-mapa-rede-mt-design.md).

## Situação do projeto

| Fatia | Conteúdo | Ramo | Pull Request | Situação |
|---|---|---|---|---|
| 0 | Estrutura do método da MT: regras, ganchos, CI, spec | `metodo-mt` | [PREENCHER] | Em revisão |
| 1 | Descoberta de hosts, fabricante, nome, relatório HTML básico | `descoberta-hosts` | [PREENCHER] | Em teste |
| 2 a 5 | Portas, identificação leve, classificação, relatório completo | a definir | - | A fazer |

## Primeira vez na máquina

```bat
git clone https://github.com/manfredjr/mapa-rede-mt.git MAPA-REDE-MT
cd MAPA-REDE-MT
git config core.hooksPath .githooks
```

O último comando liga os ganchos que enviam cada commit ao GitHub na hora. A configuração é local e não vem com o clone.

## Publicação

Ver [`docs/publicacao.md`](docs/publicacao.md), com as seções "Antes de publicar" e "Depois de publicar".

## A confirmar com

Nada no momento.

## Autor

Manfred Heil Junior - MT - Manfred Tecnologia.
