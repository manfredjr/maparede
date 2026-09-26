# Consulta ao advogado: distribuição do MapNet - MT

Data: 26/09/2026. Cliente da consulta: MANFRED TECNOLOGIA LTDA (MT - Manfred Tecnologia), CNPJ 21.075.901/0001-12, microempresa. Análise de base: `docs/legal/verificacao-varredura-e-lgpd-2026-09-26.md`.

**Contexto em uma linha.** A MT desenvolve e distribui de graça o MapNet - MT, programa de código aberto (GPL-3.0) para inventário de rede, parecido com o Advanced IP Scanner. Quem baixa roda no próprio computador, na rede que escolher. A MT não executa varreduras em clientes, e o programa não envia nada a ela. A próxima versão vai verificar portas TCP comuns e, depois, identificar serviços (título de página por HTTP, certificado por TLS). A distribuição tem a finalidade de divulgar a MT.

Duas perguntas fechadas. Urgência: média. Nenhuma trava o desenvolvimento; as duas definem o texto da página e do aviso ao lado da licença.

## Questão 1. Distribuição pública e o § 1º do art. 154-A

**Pergunta.** Distribuir de graça um programa de inventário de rede, com verificação de portas e identificação de serviços, com a finalidade declarada e a lista do que ele não faz (não testa senha, não explora falha, não entra em equipamento), afasta o § 1º do art. 154-A do Código Penal ("produz, oferece, distribui ... programa de computador com o intuito de permitir a prática da conduta definida no caput")? O texto "Uso autorizado" proposto na análise é suficiente, ou falta algo?

**Já apurado.** Art. 154-A com a redação da Lei nº 14.155/2021 e § 1º da Lei nº 12.737/2012, conferidos no Planalto em 26/09/2026. Programas da mesma categoria são distribuídos livremente.

**Interpretações possíveis.**
- Sem intuito de permitir invasão, não há o crime; o aviso só reforça a finalidade.
- A descrição precisa ser exata quanto ao que o programa envia; aviso em desacordo com o funcionamento enfraquece a defesa.

**Impacto.** Texto da página e do README.

## Questão 2. Relação de consumo e ausência de garantia

**Pergunta.** A distribuição gratuita, cuja finalidade é divulgar a MT, cria relação de consumo com quem baixa (CDC, arts. 2º e 3º, § 2º, "mediante remuneração")? Se sim, o que as seções 15 e 16 da GPL-3.0 ainda afastam diante dos arts. 25 e 51, I, do CDC, e qual aviso em português a MT deve pôr ao lado da licença?

**Já apurado.** Arts. 2º, 3º, 25 e 51 do CDC, conferidos no Planalto em 26/09/2026. O `LICENSE` é a GPL-3.0 completa, com as seções 15, 16 e 17.

**Interpretações possíveis.**
- Sem remuneração, não há relação de consumo, e as seções 15 e 16 valem no regime civil.
- A divulgação é remuneração indireta, há relação de consumo, e a exclusão de responsabilidade é nula no que a lei torna irrenunciável.

**Impacto.** Aviso ao lado da licença, na página e no programa.
