# Parecer sobre a distribuição do MapNet - MT

Recebido pelo Manfred em 26/09/2026, em resposta a `CONSULTA-ADVOGADO-varredura-e-lgpd-2026-09-26.md`. Transcrito como recebido.

A MANFRED TECNOLOGIA LTDA desenvolve e distribui o programa, mas, conforme as premissas informadas, não escolhe a rede examinada, não executa a varredura e não recebe seus resultados. A análise abaixo trata da responsabilidade da MT como distribuidora, e não como prestadora de um serviço de inventário.

## 1. Distribuição e art. 154-A, § 1º, do Código Penal

A distribuição gratuita não afasta o tipo penal por si só. O ponto decisivo é o intuito de permitir a invasão descrita no caput. Um programa destinado a inventário, que verifica portas e identifica serviços por interações normais com HTTP e TLS, tem uma finalidade legítima. A possibilidade de alguém usá-lo indevidamente não transforma, por si, sua distribuição no crime do § 1º. Essa conclusão depende de o funcionamento e a divulgação corresponderem ao que a MT declara: sem teste de senhas, contorno de autenticação ou exploração de vulnerabilidades. [www.planalto.gov.br](https://www.planalto.gov.br)

O aviso de "Uso autorizado" é recomendável, mas não funciona como proteção isolada. Página, README e interface devem descrever o alcance da varredura e as requisições enviadas. Antes da execução, o usuário deve poder conferir o intervalo de endereços escolhido. Documentar as versões publicadas também ajuda a demonstrar a finalidade do produto. Trata-se de medidas de prudência e prova, não de uma lista de requisitos expressos no § 1º.

Sugiro este texto perto do download:

> Uso autorizado. O MapNet - MT serve para inventariar equipamentos em redes nas quais você tem autorização para realizar a verificação. Ao ativar a identificação de serviços, o programa pode abrir conexões TCP, enviar requisições HTTP e iniciar negociações TLS para ler informações disponibilizadas pelos serviços. Confira o intervalo de endereços antes de iniciar e respeite as regras da rede e dos dispositivos examinados. O programa não testa senhas, não contorna autenticação e não explora vulnerabilidades.

Eu substituiria a frase "não entra em equipamento". Ela é imprecisa para um programa que estabelece conexões e faz requisições HTTP e TLS.

## 2. CDC e seções 15 e 16 da GPL-3.0

Há fundamento relevante para a aplicação do CDC, embora não seja possível afirmá-la de modo definitivo só com esses fatos. O Código inclui bens imateriais no conceito de produto; a expressão "mediante remuneração" está na definição de serviço. O STJ reconheceu que um serviço gratuito na internet pode integrar uma relação de consumo quando produz ganho indireto para quem o oferece. Naquele caso havia um serviço online. O MapNet - MT é um programa executado localmente, sem cadastro e sem envio de dados à MT. O precedente, portanto, não decide diretamente este caso, mas mostra por que a gratuidade e a finalidade promocional precisam ser examinadas em conjunto. Para redigir a oferta pública, recomendo considerar a possibilidade de incidência do CDC. [www.planalto.gov.br](https://www.planalto.gov.br)

As seções 15 e 16 da GPL-3.0 continuam úteis para esclarecer que a licença não promete um resultado específico, compatibilidade com toda rede ou suporte técnico gratuito. Também não fazem a MT responder automaticamente por qualquer rede escolhida pelo usuário ou por uso indevido de terceiros. Se o CDC incidir, porém, essas seções não eliminam a garantia legal aplicável nem afastam a responsabilidade que a lei impõe por defeito do produto ou informação inadequada sobre seus riscos. O próprio texto da GPL condiciona a exclusão de garantia aos limites da lei aplicável. Mantenha a licença integral, com um aviso explicativo separado em português. Free Software Foundation

> Licença e garantias. O MapNet - MT é distribuído gratuitamente sob a GPL-3.0. O inventário depende das respostas dos dispositivos e pode apresentar resultados incompletos ou imprecisos. A licença não inclui promessa de funcionamento em toda rede nem serviço de suporte técnico. Quem executa o programa deve definir uma rede autorizada e conferir o alcance da verificação antes de iniciá-la. As disposições da GPL-3.0 sobre garantias e responsabilidade aplicam-se nos limites permitidos pela legislação brasileira e não restringem direitos assegurados ao consumidor por lei.
