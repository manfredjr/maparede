# Verificação jurídica: distribuição do MapNet - MT, varredura de rede e LGPD

Data: 26/09/2026. Objeto: MapNet - MT, software livre (GPL-3.0) desenvolvido e distribuído pela MANFRED TECNOLOGIA LTDA (MT - Manfred Tecnologia), CNPJ 21.075.901/0001-12, microempresa. Versão 0.2.5 publicada e versão 0.3.0 planejada (sondagem de portas TCP).

> **Situação.** As 2 pendências desta análise foram respondidas pelo parecer recebido em 26/09/2026, em `docs/legal/parecer-distribuicao-2026-09-26.md`. A página e o README passaram a trazer os avisos "Uso autorizado" e "Licença e garantias", e a frase "não entra em equipamento" saiu dos textos.

## 1. Contexto e fatos

- **Papel da MT:** a MT desenvolve o programa e o distribui de graça, pela página `mapnet.manfred.com.br` e pelo GitHub. A MT não executa varreduras em clientes (informação do Manfred em 26/09/2026). A finalidade declarada da distribuição é divulgar a MT.
- **Quem usa:** qualquer pessoa ou empresa que baixa o programa e o roda no próprio computador, na rede que escolher.
- **O que o programa faz hoje (0.2.5):** descobre os equipamentos da sub-rede por ping e ARP; lê IP, MAC, fabricante (tabela OUI) e nomes (DNS reverso, NetBIOS, mDNS); mostra dados do próprio computador; ferramentas de diagnóstico e manutenção do próprio computador; consulta opcional do IP público. Não testa senha, não explora falha, não entra em equipamento.
- **O que vem (0.3.0 e seguintes):** abrir e fechar conexão TCP em portas comuns (sem enviar dados); depois, identificar serviços, o que já exige enviar uma requisição HTTP para ler o título da página e iniciar o TLS para ler o certificado.
- **Resultado:** relatório gravado só no computador de quem roda o programa. O programa não envia nada à MT.
- **Programas da mesma categoria:** o Advanced IP Scanner, da Famatech, é distribuído de graça, roda sem instalar e declara detectar equipamentos, endereços MAC, pastas compartilhadas, servidores FTP e acesso por RDP e Radmin (página oficial consultada em 26/09/2026). Isso é contexto de mercado, não fundamento jurídico.

## 2. Fontes e verificação

| Norma | Onde foi verificada | Data |
|---|---|---|
| Código Penal, arts. 154-A e 154-B (Lei nº 12.737/2012, com a redação da Lei nº 14.155/2021) | planalto.gov.br, páginas das Leis nº 12.737/2012 e nº 14.155/2021 | 26/09/2026 |
| LGPD (Lei nº 13.709/2018), arts. 4º, 5º, 7º, 10, 15, 16, 37, 39 e 46 | Biblioteca local da `legal-br` (coleta de 21/08/2026) e texto compilado do Planalto: a única alteração de 2025 em diante é a Lei nº 15.352/2026, já presente na cópia local | 26/09/2026 |
| CDC (Lei nº 8.078/1990), arts. 2º, 3º, 25 e 51 | Biblioteca local e texto compilado do Planalto | 26/09/2026 |
| Código Civil, arts. 186 e 927 | Biblioteca local e texto compilado do Planalto | 26/09/2026 |

## 3. Premissas de incidência

| Premissa | O que aciona | Fundamento |
|---|---|---|
| A MT não trata os dados levantados pelo programa | A MT fica fora dos deveres de agente de tratamento quanto a esses dados | O programa não envia nada à MT, e a MT não roda varreduras. Não há, por parte dela, nenhuma das operações do art. 5º, X, da LGPD sobre esses dados |
| Quem roda o programa pode tratar dados pessoais | LGPD, para esse usuário | O relatório pode trazer nome de aparelho com nome de pessoa e MAC de aparelho pessoal (art. 5º, I) |
| Quem baixa de graça pode ser consumidor | CDC, se houver remuneração indireta | Pendência: questão 2 da consulta |

## 4. Análise

### 4.1 LGPD

**FATO LEGAL.** LGPD, art. 5º: "VI - controlador: pessoa natural ou jurídica, de direito público ou privado, a quem competem as decisões referentes ao tratamento de dados pessoais; VII - operador: pessoa natural ou jurídica, de direito público ou privado, que realiza o tratamento de dados pessoais em nome do controlador". Verificado no Planalto em 26/09/2026.

**INTERPRETAÇÃO.** A MT não é controladora nem operadora dos dados que os usuários levantam: ela não decide sobre esse tratamento nem o realiza. O controlador é quem roda o programa na própria rede, quando a LGPD se aplica a ele.

**FATO LEGAL.** LGPD, art. 4º, I: a lei não se aplica ao tratamento "realizado por pessoa natural para fins exclusivamente particulares e não econômicos". Verificado no Planalto em 26/09/2026. Incidência: alcança quem usa o programa na rede de casa, sem fim econômico. Não alcança empresa nem técnico que atende cliente.

**RECOMENDAÇÃO.** Mesmo sem obrigação da MT, o programa deve continuar ajudando o usuário a cumprir a LGPD: relatório só local, opção de relatório sem nomes de aparelhos pessoais (minimização, art. 10, § 1º) e aviso de que o relatório pode conter dados pessoais.

**Se a MT passar a usar o programa em atendimento a clientes**, a análise muda: a MT tende a ser operadora do cliente (art. 5º, VII, e art. 39), com termo de autorização, instruções, prazo de guarda e registro das operações (art. 37). Rever este documento nesse caso.

### 4.2 Distribuição pública e o § 1º do art. 154-A

**FATO LEGAL.** Código Penal, art. 154-A, redação da Lei nº 14.155/2021: "Invadir dispositivo informático de uso alheio, conectado ou não à rede de computadores, com o fim de obter, adulterar ou destruir dados ou informações sem autorização expressa ou tácita do usuário do dispositivo ou de instalar vulnerabilidades para obter vantagem ilícita: Pena - reclusão, de 1 (um) a 4 (quatro) anos, e multa." § 1º (Lei nº 12.737/2012): "Na mesma pena incorre quem produz, oferece, distribui, vende ou difunde dispositivo ou programa de computador com o intuito de permitir a prática da conduta definida no caput." Verificados no Planalto em 26/09/2026.

**INTERPRETAÇÃO.** O § 1º exige o intuito de permitir a invasão. O MapNet é programa de inventário, da mesma categoria de ferramentas distribuídas livremente há anos, e o código, o README e a página dizem o que ele não faz. O risco para a MT é baixo. Um aviso de uso autorizado reforça a finalidade, mas não substitui a coerência entre o que se diz e o que o programa faz. É a questão 1 da consulta.

**RECOMENDAÇÃO.** A descrição tem de corresponder exatamente ao programa publicado. "Sem enviar dados" vale para a verificação de conexão TCP da 0.3.0, e não para a identificação de serviços, que manda requisição HTTP e inicia TLS.

### 4.3 Autorização de quem roda o programa

**INTERPRETAÇÃO.** O caput do art. 154-A fala em autorização "do usuário do dispositivo". Isso pesa sobre quem roda o programa, não sobre a MT. Numa rede de empresa, a permissão da empresa não equivale, em todos os casos, à do usuário de um celular pessoal ligado a ela.

**RECOMENDAÇÃO.** Como decisão de produto, e não como obrigação da MT: pedir o escopo antes da sondagem de portas, deixar aparelhos pessoais ou de dono incerto fora da sondagem por padrão (o MAC aleatório serve de sinal, não de prova) e mostrar, antes de rodar, o que cada etapa vai enviar.

### 4.4 Dano à rede

**FATO LEGAL.** Código Civil, art. 186 e art. 927, caput: quem, por negligência ou imprudência, causa dano a outrem comete ato ilícito e fica obrigado a repará-lo. Verificados no Planalto em 26/09/2026.

**RECOMENDAÇÃO.** Manter os limites de tamanho de sub-rede e de conexões ao mesmo tempo, que evitam degradar a rede de quem usa.

### 4.5 CDC e a ausência de garantia da GPL-3.0

**FATO.** O `LICENSE` é a GPL-3.0 completa, com as seções 15 (Disclaimer of Warranty), 16 (Limitation of Liability) e 17.

**FATO LEGAL.** CDC, art. 3º, § 2º: serviço é atividade fornecida "mediante remuneração". Art. 25: é vedada cláusula "que impossibilite, exonere ou atenue a obrigação de indenizar". Art. 51, I: são nulas as cláusulas que "impossibilitem, exonerem ou atenuem a responsabilidade do fornecedor", admitida limitação "em situações justificáveis" com consumidor pessoa jurídica. Verificados no Planalto em 26/09/2026. Incidência: só se houver relação de consumo com quem baixa, o que é a questão 2 da consulta.

**INTERPRETAÇÃO.** Como a distribuição serve para divulgar a MT, é prudente considerar a possibilidade de relação de consumo. Nesse caso, as seções 15 e 16 não afastam o que a lei torna irrenunciável.

**RECOMENDAÇÃO.** Manter a GPL-3.0 íntegra e acrescentar, em português, um aviso que descreva os limites técnicos do programa e ressalve os direitos garantidos pela lei aplicável, sem prometer ausência total de responsabilidade.

## 5. Requisitos para implementação

- [ ] Texto "Uso autorizado" perto do download na página e no README.
- [ ] Descrição exata, na página, no README e na tela, do que cada etapa envia (conexão TCP na 0.3.0; requisição HTTP e TLS na identificação de serviços).
- [ ] Escopo pedido antes da sondagem de portas, com aparelhos pessoais ou incertos fora por padrão.
- [ ] Aviso, na tela e no relatório, de que o relatório pode conter dados pessoais e deve ser guardado e apagado com cuidado.
- [ ] Opção de relatório sem nomes de aparelhos pessoais.
- [ ] Aviso em português sobre os limites do programa e a ressalva dos direitos legais, ao lado da GPL-3.0.

## 6. Pendências de validação

1. Se a distribuição pública, com a finalidade e os avisos descritos, afasta o § 1º do art. 154-A.
2. Se há relação de consumo na distribuição gratuita com finalidade de divulgação, e como ficam as seções 15 e 16 da GPL-3.0.

Detalhe em `docs/legal/CONSULTA-ADVOGADO-varredura-e-lgpd-2026-09-26.md`.
