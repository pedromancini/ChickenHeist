# Casa desgastada, campainha e galinheiro funcional — 06/10/2026

Pedido do usuário: melhorar o interior da casa, a campainha e as animações com aspecto desgastado; o galinheiro com a porta presa aberta deve abrir e fechar com animação; deve ser possível colocar as galinhas dentro do galinheiro do protagonista.
Nenhuma build nova foi gerada.

## Interior (`HomeWornUpgrade`, reaplicado automaticamente pelo `PlayerHomeBuilder`)

- Reboco encardido nas paredes e no forro; manchas de umidade irregulares (pé das paredes, canto da goteira, sob a janela), escorridos da goteira e mancha no forro sobre o balde, gordura atrás do fogão.
- Reboco caído em três pontos, com ripas expostas e cacos no chão; rachaduras nas paredes.
- Piso com frestas, uma tábua solta levantada e terra trazida das botas na entrada.
- Teias nos cantos do forro, caixas de papelão, garrafas vazias, louça suja sobre o fogão, caneca lascada, jornais velhos no chão.
- Móveis do pacote desbotados (cópias de material; os originais ficam intactos); geladeira com esmalte amarelado, ferrugem na base e na dobradiça.
- Luzes quentes e fracas; a lâmpada da cozinha pisca de vez em quando (`HomeLightFlicker`).

## Campainha

- Campainha velha ao lado da porta (espelho de baquelite, botão de latão, fita isolante, fio exposto até a caixa enferrujada sobre o batente).
- E toca a campainha (`HomeDoorbell`): zumbido de dois tons que falha na fiação gasta; a lâmpada da varanda enfraquece enquanto toca.
- Na abertura, o visitante toca a campainha antes das quatro batidas (batidas atrasadas 0,55 s).

## Galinheiro

- A portinhola presa aberta virou uma portinhola funcional (`HomeCoopGate`), fechada por padrão, com tramela: E abre/fecha com dobradiça animada (abre para dentro, assenta com um pequeno tranco) e rangido.
- G na entrada: a portinhola abre sozinha, as galinhas saem das mãos (ou da caminhonete), passam pela portinhola e entram; ela fecha em seguida. G de dentro do galinheiro também funciona.
- As galinhas do sítio andam pelo cercado, desviam do comedouro e do balde e não saem, nem com a portinhola aberta.

## Validação

- Galinheiro e campainha: 16/16 (`HomeCoopReview`, `output/home-review/coop`).
- Regressão ampla: 579/579. Abertura com visitante: 70/70. Casa e celular: 24/24 + 3/3.
- Capturas antes/depois: `output/home-review` (`before/`, `sheet-casa.jpg`, `coop/sheet.jpg`).

## Backups

`output/scene-backups/ChickenHeistRuralWorld-before-home-worn.unity`.

## Revisão de assets e erros

- **Campainha atravessando a porta (corrigido):** a caixa do sino e o fio estavam sobre a folha da porta (a dobradiça fica à esquerda e a folha abre para a varanda). A campainha foi refeita inteira à direita do batente: espelho de latão arredondado com aro escuro, botão de baquelite creme que afunda ao ser apertado e parafusos; campainha de sino com base de madeira, bobina, cúpula de latão e martelo que vibra ao tocar; dois fios presos com grampos e fita isolante.
- **Lanterna da varanda (defeito anterior):** a porta aberta passava pela lanterna; ela foi movida para além do arco da folha.
- **Portinhola do galinheiro:** a travessa entrava até 3,4 cm no mourão ao girar e a última tábua 0,5 cm no mourão da tranca. Portinhola refeita (quatro tábuas, duas travessas, diagonal, tiras de dobradiça, tramela), com a dobradiça fora do mourão e abertura de 88°.
- **Varredura de interseções** (`HomeGeometryProbe`, com penetração exata dos colisores): 0 interseções entre o desgaste e os móveis, entre a porta e a campainha/lanterna em qualquer ângulo, e entre a portinhola e o galinheiro.
- **Saúde do projeto** (`ProjectHealthScan`): 0 scripts ausentes, 0 materiais nulos ou com shader de erro, 0 malhas vazias, 0 áudios sem clipe (cena + 42 prefabs); 0 avisos ou erros numa visita de 30 s à casa, ao galinheiro, à vila e a uma fazenda. Log do executável sem erros.
- **Desempenho:** as ~230 peças de desgaste são estáticas (static batching).
- Validação: galinheiro e campainha 16/16, regressão 579/579, abertura 70/70, casa e celular 24/24 + 3/3.
