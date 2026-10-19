# Sons para gravar — Chicken Heist

Hoje todos os efeitos do jogo são sintetizados pelo próprio código, como sons provisórios. Cada arquivo que você gravar substitui o provisório com o mesmo nome. A troca é automática, sem precisar mexer em código.

## Como entregar

- **Pasta:** `ChickenHeist/Assets/Resources/Sfx/`. Se preferir, me mande os arquivos que eu coloco lá.
- **Nome:** exatamente o da coluna "Arquivo", por exemplo `knock.wav`.
- **Formato:** WAV, 48 kHz (ou 44,1 kHz), 16 bits.
  - **Mono:** sons que acontecem num lugar do mundo, como passos, porta e galinha.
  - **Estéreo:** só os ambientes.
- **Edição:**
  - Sem silêncio no começo; pouco silêncio no fim.
  - Pico em torno de -3 dB. Sem música e sem eco de sala, a não ser que o som peça.
- **Loops:** os sons marcados como "loop" precisam emendar o fim no começo sem estalo.
- **Variações:** se quiser, mande variações (`step_1.wav`, `step_2.wav`…). Hoje cada som usa um arquivo só; com variações, eu ligo para o jogo alternar entre elas.

## 1. Já têm lugar no jogo (entram assim que o arquivo existir)

### Abertura (cutscene)
| Arquivo | O que é | Duração | Observação |
|---|---|---|---|
| `knock.wav` | Uma batida de nós dos dedos numa porta de madeira | 0,15–0,25 s | Toca a cada batida (3 + 1). Grave uma batida só. |
| `porch_step.wav` | Um passo de bota na varanda de tábuas | 0,2–0,3 s | Passos do visitante e do Elias. |
| `door_creak.wav` | Porta velha de madeira rangendo ao abrir/fechar | 0,8–1,5 s | Dobradiça gasta. |
| `night_ambience.wav` | Noite no sítio: grilos, insetos, sapos ao longe | 10–30 s, **loop**, estéreo | Fundo da conversa na porta. |
| `bell.wav` | Campainha elétrica velha, com zumbido falhando | ~1,2 s | Também toca de manhã, quando a caixa chega. |

### Fazendas e roubo
| Arquivo | O que é | Duração | Observação |
|---|---|---|---|
| `chicken.wav` | Cacarejo calmo | 0,4–1 s | Galinhas no galinheiro. |
| `chicken_scared.wav` | Galinha assustada gritando e batendo asa | 0,5–1 s | Quando a galinha se assusta. |
| `cow.wav` | Mugido | ~1,5 s | Vacas das fazendas. |
| `latch_click.wav` | Clique metálico de trinco cedendo | 0,1–0,2 s | Abrir o trinco do galinheiro. |
| `latch_slip.wav` | Trinco escapando/emperrando (falha) | 0,3 s | Erro no minigame do trinco. |
| `latch_scrape.wav` | Metal raspando de leve | 0,2–0,5 s, **loop** | Enquanto mexe no trinco. |
| `creak.wav` | Portinhola/dobradiça pequena rangendo | ~0,7 s | Portinhola do galinheiro de casa. |
| `wire_rattle.wav` | Tela/cerca de arame balançando | ~0,4 s | Esbarrar na cerca. |
| `camera_alert.wav` | Bipe eletrônico de alerta da câmera de segurança | ~0,3 s | Quando a câmera te vê. |
| `spray.wav` | Lata de spray pintando | ~1,2 s | Pintar a lente da câmera. |
| `sleep.wav` | Ronco do fazendeiro dormindo | ~1 s | Fazendeiro na cama. |
| `wake.wav` | Fazendeiro acordando (resmungo, "hã?") | ~1 s | Quando ele acorda. |
| `shotgun_blast.wav` | Tiro de espingarda (ao ar livre) | ~0,7 s | Fazendeiro atirando. |
| `shotgun_reload.wav` | Recarga de espingarda (abrir, cartucho, fechar) | ~1,1 s | |

### Passos e casa
| Arquivo | O que é | Duração | Observação |
|---|---|---|---|
| `step.wav` | Um passo em terra/grama | 0,3–0,4 s | Passos do jogador e dos fazendeiros. |
| `wood.wav` | Um passo em assoalho de madeira (com leve rangido) | 0,3–0,4 s | Dentro de casa e na varanda. |
| `door.wav` | Porta de casa abrindo/fechando | ~0,5 s | Portas que o jogador usa. |

### Caminhonete
| Arquivo | O que é | Duração | Observação |
|---|---|---|---|
| `engine.wav` | Motor velho em marcha lenta | 2–4 s, **loop** | O jogo muda o tom conforme a velocidade. |
| `starter.wav` | Motor de arranque girando, sem pegar | ~1,5 s | Tentativa de partida. |
| `start.wav` | Motor pegando | ~1 s | Partida que deu certo. |
| `stall.wav` | Motor morrendo | ~1 s | Partida que falhou. |

## 2. Ainda não existem no jogo (eu ligo quando você mandar)
| Arquivo sugerido | O que é | Onde vai tocar |
|---|---|---|
| `box_open.wav` | Fita sendo puxada e caixa de papelão abrindo | Abrir a caixa do tablet na porta |
| `tablet_open.wav` / `tablet_close.wav` | Toque/clique suave de aparelho | Abrir e fechar o tablet (TAB) |
| `ui_click.wav` | Clique de botão discreto | Menus |
| `rooster_morning.wav` | Galo cantando ao longe | Quando o dia amanhece |
| `day_ambience.wav` | Dia no sítio: pássaros, vento leve (**loop**, estéreo) | Fundo durante o dia |
| `chicken_pickup.wav` | Galinha agitada sendo pega no colo | Pegar galinha |
| `cage_lid.wav` | Tampa de gaiola de arame fechando | Gaiolas da caminhonete |
| `truck_door.wav` | Porta de caminhonete velha abrindo/fechando | Entrar e sair da caminhonete |
| `gate_metal.wav` | Porteira de fazenda rangendo | Abrir porteiras |
| `coins.wav` | Dinheiro/moedas | Vender galinhas no mercado |
| `dog_bark.wav` | Cachorro latindo ao longe | Ambiente das fazendas (opcional) |

## 3. Vozes (opcional)
As falas da abertura hoje são só legendas, e a boca dos personagens se mexe pelo texto. Se quiser gravar as vozes, me avise: eu passo a lista das 30 falas com o nome de cada arquivo (`visitor_opening_00_elias.wav`, `visitor_opening_03_visitante.wav`…).
