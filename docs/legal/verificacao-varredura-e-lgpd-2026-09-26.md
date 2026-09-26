# Verificação jurídica: varredura de rede, sondagem de portas e LGPD

Data: 26/09/2026. Objeto: MapNet - MT, software livre (GPL-3.0) da MT - Manfred Tecnologia, versão 0.2.5 publicada e versão 0.3.0 planejada (sondagem de portas TCP).

> **Sinalização.** Esta análise tem 4 pendências de validação, encaminhadas em `docs/legal/CONSULTA-ADVOGADO-varredura-e-lgpd-2026-09-26.md`. Os requisitos marcados como dependentes dessas pendências não devem ir para a interface como texto final antes da resposta.

## 1. Contexto e fatos

- **Quem usa:** o técnico da MT, no notebook dele, ligado à rede de uma pequena ou média empresa cliente, a pedido dela. Também qualquer pessoa que baixe o programa da página, na rede que quiser.
- **O que o programa faz hoje (0.2.5):** descobre os equipamentos da sub-rede por ping e ARP; lê IP, MAC, fabricante (tabela OUI) e nomes (DNS reverso, NetBIOS, mDNS); mostra dados do próprio computador; ferramentas de diagnóstico (ping, tracert, DNS, tabelas do Windows); manutenção do próprio computador; consulta opcional do IP público. Não testa senha, não explora falha, não entra em equipamento.
- **O que vem (0.3.0 e seguintes):** abrir e fechar conexão TCP em portas comuns de cada equipamento encontrado, sem enviar dados; depois, ler o que o equipamento anuncia (título HTTP, banner SSH e FTP, certificado HTTPS, UPnP).
- **Resultado:** relatório HTML (depois XML e CSV) gravado só no computador do técnico, em `Documentos\MapNet - MT`. Pode trazer nome de aparelho com nome de pessoa (por exemplo, o celular de um funcionário) e o MAC de aparelho pessoal.
- **Dados da empresa:** MANFRED TECNOLOGIA LTDA, CNPJ 21.075.901/0001-12, porte ME, atividade principal 62.09-1-00 (suporte técnico, manutenção e outros serviços em tecnologia da informação), conforme o comprovante de inscrição do CNPJ emitido em 26/09/2026. O foro fica como `[PREENCHER]`.

## 2. Fontes e verificação

| Norma | Onde foi verificada | Data |
|---|---|---|
| Código Penal, arts. 154-A e 154-B (Lei nº 12.737/2012, com a redação da Lei nº 14.155/2021) | planalto.gov.br, páginas das Leis nº 12.737/2012 e nº 14.155/2021 | 26/09/2026 |
| LGPD (Lei nº 13.709/2018), arts. 4º, 5º, 7º, 10, 15, 16, 37, 39 e 46 | Biblioteca local da `legal-br` (coleta de 21/08/2026) e conferência no texto compilado do Planalto: a única alteração de 2025 em diante é a Lei nº 15.352/2026, já presente na cópia local | 26/09/2026 |
| CDC (Lei nº 8.078/1990), arts. 2º, 3º, 25 e 51 | Biblioteca local e texto compilado do Planalto | 26/09/2026 |
| Código Civil, arts. 186 e 927 | Biblioteca local e texto compilado do Planalto | 26/09/2026 |
| Resolução CD/ANPD nº 2/2022, art. 9º | Só na biblioteca local (21/08/2026), sem conferência online | 26/09/2026 |

O relatório `STATUS-FONTES.md` da biblioteca não existe. Por isso cada norma usada como fato legal foi conferida no Planalto na data acima. A Resolução CD/ANPD nº 2/2022 entra só como recomendação.

## 3. Premissas de incidência

| Premissa | O que aciona | Fundamento |
|---|---|---|
| Há tratamento de dados pessoais | LGPD | O relatório traz nome de aparelho que pode conter nome de pessoa e o MAC de aparelho pessoal, ligados a uma rede e a uma data. É "informação relacionada a pessoa natural identificada ou identificável" (LGPD, art. 5º, I), e gravar e entregar o relatório é tratamento (art. 5º, X). A identificabilidade depende do contexto, por isso é INTERPRETAÇÃO, mas conservadora e razoável |
| Entre a MT e o cliente, a relação é entre empresas | Regime civil; CDC só se o cliente for destinatário final | Hipótese, sem contrato-modelo analisado |
| Quem baixa o programa de graça pode ser consumidor | Pendência | O programa é gratuito, mas a finalidade declarada pelo Manfred é divulgar a MT e gerar clientes. Ver questão 4 da consulta |

## 4. Análise

### 4.1 Papel da MT na LGPD e base legal

**FATO LEGAL.** LGPD, art. 5º: "VI - controlador: pessoa natural ou jurídica, de direito público ou privado, a quem competem as decisões referentes ao tratamento de dados pessoais; VII - operador: pessoa natural ou jurídica, de direito público ou privado, que realiza o tratamento de dados pessoais em nome do controlador". Art. 39: "O operador deverá realizar o tratamento segundo as instruções fornecidas pelo controlador, que verificará a observância das próprias instruções e das normas sobre a matéria." Verificado no Planalto em 26/09/2026. Incidência: há tratamento (premissa 1) feito pela MT para o cliente.

**INTERPRETAÇÃO.** Quando o cliente contrata o inventário da própria rede e decide para que ele serve, o cliente é o controlador e a MT, que roda a varredura em nome dele, é a operadora. A MT passa a controladora da parte que usar para fim próprio, por exemplo guardar o relatório para prospecção comercial. Ver questão 3 da consulta.

**INTERPRETAÇÃO.** A base legal do cliente tende a ser o legítimo interesse (LGPD, art. 7º, IX), para gestão e segurança da própria rede, que é situação concreta de apoio à atividade do controlador (art. 10, I). O art. 10, § 1º, limita o tratamento "aos dados pessoais estritamente necessários para a finalidade pretendida", e o § 2º exige transparência.

**FATO LEGAL.** LGPD, art. 4º, I: a lei não se aplica ao tratamento "realizado por pessoa natural para fins exclusivamente particulares e não econômicos". Verificado no Planalto em 26/09/2026. Incidência: alcança a pessoa que baixa o programa e o usa na rede da própria casa, sem fim econômico. Não alcança empresa nem técnico que atende cliente.

**INTERPRETAÇÃO.** A MT, só por distribuir o programa, não é agente de tratamento dos dados que terceiros levantam com ele: o programa não envia nada para a MT.

**FATO LEGAL.** LGPD, art. 37: "O controlador e o operador devem manter registro das operações de tratamento de dados pessoais que realizarem, especialmente quando baseado no legítimo interesse." Art. 46, caput: os agentes de tratamento devem adotar "medidas de segurança, técnicas e administrativas aptas a proteger os dados pessoais de acessos não autorizados"; § 2º: desde a concepção do produto. Verificados no Planalto em 26/09/2026.

**RECOMENDAÇÃO.** A MT é microempresa (porte ME no CNPJ), o que a coloca, em princípio, entre os agentes de tratamento de pequeno porte da Resolução CD/ANPD nº 2/2022. O art. 9º dessa resolução permite o registro do art. 37 de forma simplificada. O art. 3º afasta o benefício em tratamento de alto risco, e o inventário de rede de pequenas empresas não parece ser esse caso. Texto só na biblioteca local: confirmar a vigência antes de usar.

### 4.2 Risco penal e civil da varredura e da sondagem de portas

**FATO LEGAL.** Código Penal, art. 154-A, redação da Lei nº 14.155/2021: "Invadir dispositivo informático de uso alheio, conectado ou não à rede de computadores, com o fim de obter, adulterar ou destruir dados ou informações sem autorização expressa ou tácita do usuário do dispositivo ou de instalar vulnerabilidades para obter vantagem ilícita: Pena - reclusão, de 1 (um) a 4 (quatro) anos, e multa." § 1º (Lei nº 12.737/2012): "Na mesma pena incorre quem produz, oferece, distribui, vende ou difunde dispositivo ou programa de computador com o intuito de permitir a prática da conduta definida no caput." Art. 154-B: nesses crimes "somente se procede mediante representação", salvo contra a administração pública ou concessionárias de serviço público. Verificados no Planalto em 26/09/2026.

**INTERPRETAÇÃO.** O tipo exige invadir o dispositivo com o fim de obter, adulterar ou destruir dados, sem autorização do usuário. A descoberta por ping e ARP e a leitura de nomes pelos protocolos que os equipamentos anunciam na rede não entram no equipamento. A sondagem de portas (abrir e fechar conexão, sem enviar dados) e a leitura do que o serviço anuncia ficam mais perto da fronteira, porque interagem com cada equipamento. O tipo fala em autorização "do usuário do dispositivo", e não do dono da rede: na rede de um cliente, o celular de um funcionário ou de um visitante tem outro usuário. É a questão 1 da consulta.

**INTERPRETAÇÃO.** O § 1º pune quem distribui programa "com o intuito de permitir" a invasão. O MapNet declara em código, README e página que não testa senha, não explora falha e não entra em nada. Isso afasta o intuito, mas o texto de uso responsável na página reforça. É a questão 2 da consulta.

**FATO LEGAL.** Código Civil, art. 186: "Aquele que, por ação ou omissão voluntária, negligência ou imprudência, violar direito e causar dano a outrem, ainda que exclusivamente moral, comete ato ilícito." Art. 927, caput: quem causar dano por ato ilícito "fica obrigado a repará-lo". Verificados no Planalto em 26/09/2026. Incidência: uma varredura mal dimensionada que derrube ou degrade a rede do cliente pode causar dano.

**RECOMENDAÇÃO.** Manter os limites que já existem (sub-rede local, no máximo /22, paralelismo com teto) e os previstos para a 0.3.0 (128 conexões ao todo, 8 por host, 800 ms por porta), e registrar no relatório quem autorizou e quando.

### 4.3 Autorização do responsável pela rede

**RECOMENDAÇÃO.** A autorização expressa e escrita é o que dá segurança ao técnico:

1. **Termo de autorização de varredura**, assinado pelo representante do cliente antes do atendimento, com: rede e endereço, período, o que o programa faz e o que não faz, quem fica com o relatório, prazo de guarda e descarte, e declaração de que o cliente avisou os usuários de aparelhos pessoais ligados à rede ou vai restringir a varredura (ver questão 1). O texto do termo depende das respostas da consulta e do foro (`[PREENCHER]`).
2. **Cláusula no contrato de prestação de serviço** da MT com o mesmo conteúdo, para clientes recorrentes.
3. **Confirmação na tela** antes da primeira varredura em cada rede, com caixa não marcada e o registro no relatório (nome do técnico, data, hora e a frase aceita). Na linha de comando, uma opção obrigatória equivalente. A confirmação na tela não substitui o termo: ela registra, dentro do programa, que o técnico declarou ter a autorização.

### 4.4 Aviso na tela e uso responsável na página

**RECOMENDAÇÃO.** Rascunhos, a revisar depois da consulta e a passar pela `humanizar-ptbr` antes de entrar no código:

- Tela, antes da varredura: "Este programa levanta os equipamentos desta rede e pode registrar nomes de aparelhos e endereços MAC, inclusive de aparelhos pessoais. Só faça a varredura com autorização do responsável pela rede. [ ] Tenho autorização do responsável por esta rede."
- Página, seção de uso responsável: "Use o MapNet só em rede sua ou em rede cujo responsável autorizou o inventário. O programa não testa senha, não explora falha e não entra em equipamento, mas o relatório pode trazer dados pessoais, como o nome de um celular. Guarde o relatório com cuidado e apague quando não precisar mais."

### 4.5 Guarda e descarte do relatório

**FATO LEGAL.** LGPD, art. 15, I: o tratamento termina com a "verificação de que a finalidade foi alcançada ou de que os dados deixaram de ser necessários". Art. 16, caput: "Os dados pessoais serão eliminados após o término de seu tratamento", salvo as hipóteses dos incisos. Verificados no Planalto em 26/09/2026. Incidência: o relatório é tratamento de dado pessoal (premissa 1).

**INTERPRETAÇÃO.** Como operadora, a MT segue a instrução do cliente sobre o prazo (art. 39). Sem instrução, o relatório serve até a entrega ao cliente e a conclusão do serviço.

**RECOMENDAÇÃO.** Prazo de guarda no termo e no contrato (`[PREENCHER]`, por exemplo até a entrega do serviço); entrega ao cliente por canal protegido; exclusão da cópia do técnico no fim do prazo; notebook do técnico com disco criptografado (BitLocker) e conta com senha, em atenção ao art. 46.

### 4.6 Licença GPL-3.0 e ausência de garantia

**FATO.** O arquivo `LICENSE` do repositório é a GPL-3.0 completa, com as seções 15 (Disclaimer of Warranty) e 16 (Limitation of Liability), e a seção 17 prevê que, onde essas cláusulas não tiverem efeito, os tribunais apliquem a lei local mais próxima de uma renúncia de responsabilidade.

**FATO LEGAL.** CDC, art. 3º, § 2º: serviço é atividade fornecida "mediante remuneração". Art. 25: é vedada cláusula "que impossibilite, exonere ou atenue a obrigação de indenizar". Art. 51, I: são nulas as cláusulas que "impossibilitem, exonerem ou atenuem a responsabilidade do fornecedor", admitida limitação "em situações justificáveis" com consumidor pessoa jurídica. Verificados no Planalto em 26/09/2026. Incidência: só se houver relação de consumo com quem baixa o programa, o que é a questão 4.

**INTERPRETAÇÃO.** Entre a MT e empresa cliente, fora de relação de consumo, a limitação de responsabilidade é admitida. Para quem baixa de graça, a validade das seções 15 e 16 depende de haver ou não relação de consumo, por causa da remuneração indireta (divulgação da MT).

## 5. Requisitos para implementação

- [ ] Confirmação de autorização antes da primeira varredura em cada rede, com caixa não marcada; registro no relatório de técnico, data, hora e frase aceita. Na linha de comando, opção obrigatória equivalente. (Texto final depende da questão 1.)
- [ ] Seção "Uso responsável" na página e no README. (Texto final depende das questões 1 e 2.)
- [ ] Opção de relatório sem nomes de aparelhos e sem MAC de aparelhos com MAC aleatório, para minimização (LGPD, art. 10, § 1º).
- [ ] Na 0.3.0, por padrão, não sondar portas de aparelhos com MAC aleatório, que são quase sempre celulares pessoais, até a resposta da questão 1.
- [ ] Aviso, no relatório, de que ele contém dados da rede do cliente e deve ser guardado e apagado conforme o combinado.
- [ ] Modelo de termo de autorização de varredura, depois da consulta e com os dados da MT.
- [ ] Registro das operações de tratamento da MT (art. 37), simplificado se a MT for de pequeno porte.

## 6. Pendências de validação

1. Sondagem de portas e leitura de banners frente ao art. 154-A, e de quem é a autorização exigida para aparelhos pessoais na rede do cliente.
2. Risco do § 1º do art. 154-A na distribuição pública do programa.
3. Enquadramento da MT como operadora e o que o contrato ou o termo precisam conter.
4. Relação de consumo na distribuição gratuita com finalidade de divulgação, e validade das seções 15 e 16 da GPL-3.0.

Detalhe de cada uma em `docs/legal/CONSULTA-ADVOGADO-varredura-e-lgpd-2026-09-26.md`.
