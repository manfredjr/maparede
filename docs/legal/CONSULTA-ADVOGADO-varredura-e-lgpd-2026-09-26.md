# Consulta ao advogado: varredura de rede, sondagem de portas e LGPD

Data: 26/09/2026. Cliente da consulta: MT - Manfred Tecnologia (`[PREENCHER]` razão social e CNPJ). Análise de base: `docs/legal/verificacao-varredura-e-lgpd-2026-09-26.md`.

**Contexto em uma linha.** O MapNet - MT é um programa gratuito e de código aberto que o técnico da MT roda na rede de empresas clientes, a pedido delas, para fazer o inventário dos equipamentos. Qualquer pessoa também pode baixá-lo. Ele não testa senha nem explora falha. A próxima versão vai abrir e fechar conexão TCP em portas comuns de cada equipamento, sem enviar dados, e depois ler o que o equipamento anuncia (título de página, banner, certificado).

Quatro perguntas fechadas. Urgência: as questões 1 e 2 travam a publicação da versão 0.3.0; as questões 3 e 4 travam o modelo de termo e o contrato.

## Questão 1. Sondagem de portas e autorização para aparelhos pessoais

**Pergunta.** (a) Abrir e fechar conexão TCP em portas de um equipamento, sem enviar dados, e ler o que o serviço anuncia sozinho (banner, título, certificado) pode configurar "invadir dispositivo informático" "com o fim de obter ... dados ou informações" (CP, art. 154-A)? (b) Na rede de um cliente, a autorização da empresa dona da rede basta para esses atos em aparelhos pessoais de funcionários e visitantes, dado que o tipo fala em autorização "do usuário do dispositivo"?

**Já apurado.** Texto do art. 154-A com a redação da Lei nº 14.155/2021 e o § 1º da Lei nº 12.737/2012, conferidos no Planalto em 26/09/2026. A ação é condicionada a representação (art. 154-B).

**Interpretações possíveis.**
- Não há invasão: nenhum mecanismo é contornado e só se lê o que o serviço oferece a qualquer um da rede. Risco baixo com a autorização da empresa.
- Há risco em aparelho pessoal: o usuário do aparelho não autorizou, e a leitura de banners pode ser vista como obtenção de informação.

**Impacto.** Define se a 0.3.0 pode sondar todos os equipamentos com a autorização da empresa, ou se deve excluir por padrão os aparelhos pessoais (MAC aleatório) e exigir que o termo trate deles.

## Questão 2. Distribuição pública do programa

**Pergunta.** Publicar o programa para download livre, com a finalidade declarada de inventário e com a lista do que ele não faz, afasta o risco do § 1º do art. 154-A ("produz, oferece, distribui ... programa de computador com o intuito de permitir a prática da conduta definida no caput")? Há texto que a página deva trazer?

**Já apurado.** § 1º conferido no Planalto em 26/09/2026. A página e o README já dizem que o programa não testa senha, não explora falha e não entra em equipamento.

**Interpretações possíveis.**
- Sem intuito de permitir invasão, não há o crime, e o texto atual basta.
- Um texto de uso responsável, com a exigência de autorização, reforça a ausência de intuito.

**Impacto.** Texto da página e do README.

## Questão 3. Papel da MT na LGPD e conteúdo do termo

**Pergunta.** Quando a MT faz o inventário a pedido do cliente, ela é operadora (LGPD, art. 5º, VII, e art. 39) e o cliente é o controlador? O que o termo de autorização ou o contrato precisam trazer, no mínimo, para esse enquadramento se sustentar (instruções, prazo de guarda, descarte, segurança, avisos aos titulares)?

**Já apurado.** Arts. 5º, 7º, 10, 15, 16, 37, 39 e 46 da LGPD, conferidos no Planalto em 26/09/2026. Base legal provável do cliente: legítimo interesse (art. 7º, IX, e art. 10).

**Interpretações possíveis.**
- MT operadora, cliente controlador, com instruções no termo. É a leitura da análise.
- MT controladora em conjunto, se ela decidir o que coletar e como. Nesse caso, a MT responde também pela base legal e pela transparência aos titulares.

**Impacto.** Cláusulas do contrato e do termo; registro das operações da MT.

## Questão 4. Relação de consumo e ausência de garantia

**Pergunta.** A distribuição gratuita do programa, cuja finalidade é divulgar a MT e gerar clientes, cria relação de consumo com quem baixa (CDC, arts. 2º e 3º, § 2º, "mediante remuneração")? Se sim, as seções 15 e 16 da GPL-3.0 (ausência de garantia e limitação de responsabilidade) valem diante dos arts. 25 e 51, I, do CDC?

**Já apurado.** Arts. 2º, 3º, 25 e 51 do CDC, conferidos no Planalto em 26/09/2026. O `LICENSE` é a GPL-3.0 completa, com as seções 15, 16 e 17.

**Interpretações possíveis.**
- Sem remuneração, não há relação de consumo, e as seções 15 e 16 valem no regime civil.
- A remuneração indireta pela divulgação cria relação de consumo, e a exclusão de responsabilidade seria nula nesse ponto.

**Impacto.** Texto de aviso na página e no programa; avaliação de seguro ou de limitação contratual com clientes pagantes.
