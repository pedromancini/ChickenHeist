# Chicken Heist — roteiro de vozes

Versão proposta: 1. Personagens adultos. Português brasileiro natural, interior sem sotaque caricatural. As falas serão gravadas pelo usuário; não usar a narração sintética anterior nestas cenas.

## Personagens

- **Elias — protagonista:** herdou o sítio da mãe. Está sem renda, envergonhado com as dívidas e tentando aparentar controle. Voz contida, cansada; não é um vilão teatral.
- **Lia — irmã de Elias:** ajuda no que pode, mas não consegue sustentar os dois. Fala com intimidade e firmeza, sem dar sermão.
- **Osvaldo — fazendeiro vizinho:** depende da criação para pagar suas contas. Reservado, abalado quando percebe o roubo.
- **Joana — companheira de Osvaldo:** prática; percebe tanto o prejuízo quanto o cansaço dele.

## Cena 1 — O que ficou

Fim de tarde no sítio. Elias e Lia estão na varanda. Uma conta dobrada sobre uma caixa; ao fundo, o galinheiro vazio. A conversa começa no meio de uma visita, sem narrador explicando a situação.

| Arquivo | Personagem | Fala exata | Intenção / imagem |
|---|---|---|---|
| opening_01_lia.wav | Lia | Você nem abriu a carta, Elias. | Ela aponta a conta; plano dos dois. |
| opening_02_elias.wav | Elias | Eu sei o que tem aí. | Ele evita olhar para ela; aproximação discreta. |
| opening_03_lia.wav | Lia | Então sabe que eles não vão esperar até a próxima colheita. | Preocupação, sem agressividade. |
| opening_04_elias.wav | Elias | Que colheita? Olha isso. O galinheiro vazio... noventa e cinco reais na conta. | Olha para o quintal. Pausa antes do valor. |
| opening_05_lia.wav | Lia | Eu falei com o Osvaldo. Talvez ele consiga uns dias de serviço pra você. | Tentativa concreta de ajudar. |
| opening_06_elias.wav | Elias | Você pediu emprego pra mim? | Orgulho ferido; não gritar. |
| opening_07_lia.wav | Lia | Pedi pra ele conversar com você. É diferente. | Resposta curta, com intimidade. |
| opening_08_elias.wav | Elias | A mãe deixou esse lugar comigo. Eu devia dar conta. | A voz baixa. Close, mãos paradas. |
| opening_09_lia.wav | Lia | Ela deixou uma casa. Não uma obrigação de resolver tudo sozinho. | Afeto contido; pequena pausa depois de “casa”. |
| opening_10_elias.wav | Elias | Eu vou achar um jeito. | Ele ainda não decidiu como. |
| opening_11_lia.wav | Lia | Amanhã eu volto. E você vai falar com ele, tá? | Ela se despede. |
| opening_12_elias.wav | Elias | Tá. | Hesitação; plano fica no rosto por dois segundos. |

Fecho visual: Lia se afasta. Elias olha o galinheiro e depois a estrada. Anoitece. O jogador assume o controle; a cena não afirma que o roubo é sua única saída.

## Cena 2 — Do outro lado da cerca

Somente depois de um ciclo com assalto novo, durante o sono. Uma visão subjetiva de Elias, não uma notícia factual sobre a fazenda roubada. Começa com Osvaldo e Joana diante de um galinheiro; termina na varanda arruinada do protagonista. Não mostrar rostos assustadores nem susto sonoro.

| Arquivo | Personagem | Fala exata | Intenção / imagem |
|---|---|---|---|
| decline_01_joana.wav | Joana | Osvaldo... a porta tá aberta. | Ela para antes do galinheiro. |
| decline_02_osvaldo.wav | Osvaldo | Eu fechei ontem. Eu tenho certeza. | Ele examina a trava. |
| decline_03_joana.wav | Joana | Levaram? | A pergunta quase não sai. |
| decline_04_osvaldo.wav | Osvaldo | Levaram. | Plano do espaço vazio. Sem citar quantidade. |
| decline_05_joana.wav | Joana | E o dinheiro da ração? | Preocupação imediata, cotidiana. |
| decline_06_osvaldo.wav | Osvaldo | Amanhã eu vejo o que dá pra adiar. | Ele tenta tranquilizá-la, mas não tem resposta. |
| decline_07_elias.wav | Elias | Eu só precisava sair do aperto. | Elias observa sem ser visto; fala para si. |
| decline_08_lia.wav | Lia | Elias? | Corte para a varanda dele; Lia é uma lembrança do sonho. |
| decline_09_elias.wav | Elias | Eu ia devolver. Quando as coisas melhorassem. | Ele procura uma justificativa. |
| decline_10_lia.wav | Lia | E até lá? | Não acusatória. Deixar silêncio após a pergunta. |
| decline_11_elias.wav | Elias | Eu não pensei nisso. | A casa aparece deteriorada ao fundo. |
| decline_12_lia.wav | Lia | Então pensa agora. | Voz próxima, sem efeito sobrenatural obrigatório. |

Fecho visual: Elias sozinho na varanda em ruínas. A mão procura o batente, mas encontra madeira quebrada. Corte escuro curto; amanhecer real, casa restaurada ao estado do save. A notificação do jornal só aparece depois de acordar. A visão não altera dinheiro, animais ou melhorias.

## Entrega das gravações

Uma fala por arquivo, com os nomes da tabela. WAV mono, preferencialmente 48 kHz / 24 bits, sem música, reverberação ou normalização agressiva. Não falar os nomes dos personagens nem as indicações cênicas. Deixar aproximadamente 0,2 segundo de silêncio nas pontas, sem cortar respirações naturais. A duração final dos planos será ajustada às gravações; os silêncios dramáticos pertencem à montagem.

Integração preparada: colocar os WAV em `Assets/Resources/Dialogue/`, mantendo exatamente os nomes da tabela. O jogo usa a duração do arquivo mais uma pequena pausa; sem o arquivo, a fala fica silenciosa e o tempo é estimado para leitura da legenda. Os arquivos antigos de `Narration/` não são usados nestas novas cenas.

## Montagem visual desta versão

As cenas combinam enquadramentos em tempo real do sítio no início e no fim com uma ilustração cinematográfica animada por aproximação lenta durante o diálogo. As imagens representam os personagens; não são modelos 3D com animação labial. As ações detalhadas na coluna de intenção orientam a interpretação e uma futura montagem ampliada. Esta entrega contém duas imagens, não um vídeo de personagens animados.
