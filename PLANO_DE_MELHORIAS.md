# Plano de melhorias — Chicken Heist

Registrado em 09/09/2026 a pedido do usuário. Este documento é a lista persistente de trabalho para as próximas sessões.

## Situação e forma de trabalho

- Implementação em andamento; estados e evidências registrados abaixo.
- Projeto ativo: `E:\Jogo3D\ChickenHeist`.
- Implementar uma etapa por vez, conferir no Unity e atualizar este documento com resultados e pendências. Gerar a build somente no final do plano completo, conforme instrução do usuário nesta sessão.
- Usar testes com save isolado. Preservar progresso existente e migrar dados quando necessário.
- Conferência visual e auditiva é obrigatória nas partes correspondentes: testes numéricos não comprovam qualidade de animação, enquadramento ou som.
- Sons serão integrados junto de cada mecânica; a etapa 7 fecha o conjunto e a mixagem.
- Não marcar uma etapa como concluída enquanto houver apenas uma demonstração sem integração ao jogo.

## Base encontrada no projeto

- `FarmSecurityProgression`, `SecurityCamera` e `TrapSystem`: existem segurança regional, câmeras e armadilhas. Precisam de revisão da progressão, colocação e apresentação.
- `HouseholdAccount`: registra assaltos pendentes e publica notícias ao dormir; existe `newsUnread`. Falta garantir a experiência solicitada em cada novo ciclo e integrar as cutscenes.
- `FarmerSleepSystem`: possui estados de sono e alerta. `FarmerStateMachine` persegue e encerra a missão por proximidade; o combate solicitado exige outra implementação.
- `ChickenCoopLockpick`: já contém desbloqueio e um susto; ambos precisam ser reformulados.
- `HouseholdEconomy`: já tem materiais, reparos e compra no tablet; expandir para melhorias legíveis e visíveis do galinheiro.
- Tablet e personagem foram revisados na entrega anterior. Preservar a abertura imediata do tablet, sem animação de segurar aparelho.

## Etapa 1 — Fase 2, segurança e orientação

Status: IMPLEMENTADA, com revisão final pendente. Câmeras, campo ocluído, fios persistentes e minimapa integrados. Testes isolados passaram na rodada de 09/09/2026, incluindo 12 destinos a pé, trajeto para carro, impedimento por paredes e migração de save. Evidências: `output/phase-two-review/checks.txt`, `camera-close.png`, `camera-field.png`, `tripwire.png` e `mission-minimap.png`. Sons sintetizados são provisórios; conferência auditiva e percurso manual completo ainda pendentes. Nenhuma build nova gerada.

- [ ] Garantir câmeras e fios de armadilha nas fazendas da fase 2.
- [ ] Modelar câmeras de segurança parecidas com equipamentos reais, conforme reforçado pelo usuário: carcaça arredondada, lente com profundidade e vidro, proteção superior, articulação e suporte de fixação. Evitar blocos/cubos como modelo final; conferir silhueta, proporções, materiais e detalhes de perto, mantendo coerência com o visual do jogo.
- [ ] Mostrar o campo de detecção das câmeras no cenário: cone/setor, alcance e direção da varredura. O desenho deve corresponder à detecção real e respeitar paredes/obstáculos.
- [ ] Diferenciar câmera ativa, detectando, desativada e coberta por spray; o indicador deve acompanhar esses estados.
- [ ] Tornar os fios reconhecíveis de perto e conferir ativação, colisões, ruído e persistência.
- [ ] Selecionar/iniciar uma missão no tablet define o destino do minimapa automaticamente.
- [ ] Traçar caminho pelas estradas e passagens transitáveis, atualizar ao desviar e retirar/substituir a rota ao encerrar/trocar a missão. Evitar linha reta atravessando cercas e construções.
- [ ] Conferir a rota a pé e dirigindo; destino sem caminho deve ser informado, sem desenhar um trajeto falso.

Premissa de planejamento: “fase 2” é a progressão de segurança após o primeiro assalto seguido de sono, como sugere o sistema atual; não assumir que qualquer dia 2 ou a segunda fazenda equivale automaticamente à fase 2. Confirmar essa correspondência na implementação e registrar eventual ajuste.

Aceite: ciclo fase 1 → assalto → dormir → fase 2 demonstra câmeras/fios; câmera reconhecível como equipamento real, sem aparência quadradona ou de bloco provisório, inclusive de perto; campo visível coincide com detecção; missão escolhida fornece rota utilizável até a entrada da fazenda.

## Etapa 2 — Fazendeiro, escopeta e vida

Status: IMPLEMENTADA, com revisão final pendente. Quartos e navegação nas 12 fazendas; despertar, saída física, investigação por ruído, contato visual, escopeta, vida, ferimento e checkpoint integrados. Testes isolados passaram em 09/09/2026: caminhos das 12 casas, saída sem salto, cobertura, dano, pausa, morte, retomada e migração de save. Evidências: `output/farmer-combat-review/checks.txt` e capturas na mesma pasta. Poses adaptadas ao esqueleto atual dos NPCs. Ainda pendentes: balanceamento jogando, fuga no carro, revisão de recarga e audição/mixagem dos sons provisórios. Nenhuma build gerada.

- [ ] Fazendeiro começa dentro da casa, dormindo, e permanece lá enquanto estiver em sono profundo.
- [ ] Ao atingir o limiar mínimo para sair do sono profundo: despertar, levantar, alcançar a porta e sair fisicamente, sem teletransporte ou atravessar paredes.
- [ ] Separar despertar de localizar o jogador: barulho provoca investigação; enxergar o jogador permite perseguição e combate.
- [ ] Equipar uma doze/escopeta com poses, disparo, recarga, efeitos e sons.
- [ ] Mira de dificuldade intermediária: reação e preparação perceptíveis, dispersão, intervalo de tiro e perda de precisão com distância/movimento. Cobertura bloqueia tiros; nada de acertar através de paredes ou rastrear o jogador escondido perfeitamente.
- [ ] Adicionar vida, dano, ferimento com mancar/redução de velocidade e morte. Compor a penalidade com agachamento, corrida e outras velocidades existentes.
- [ ] Mostrar estado de vida/ferimento e integrar derrota, reinício e checkpoint. Definir recuperação e valores finais durante o balanceamento, sem inventar punições permanentes não pedidas.
- [ ] Conferir tiros durante fuga a pé e no carro, múltiplos impactos e pausa.

Aceite: limiar de sono respeitado; saída da casa visível; oportunidade real de fugir/esconder; ferimento perceptível sem travar o controle; morte e retomada consistentes. Ajustar a dificuldade jogando, além dos testes.

## Etapa 3 — Nova invasão do galinheiro e susto

Status: IMPLEMENTADA, com revisão final pendente. Trava de três peças com pressão contínua, atrito, sequência por fazenda e tolerância de equipamento integrada. Susto usa a galinha real, com deformação da região das asas, enquadramento do rosto e restauração em término, cancelamento, desativação e carregamento. Testes isolados passaram em 09/09/2026 para 12 variações, 30/60/144 fps, ferramentas básica/profissional, ruído, pausa, inventário e checkpoint. Evidências: `output/coop-pressure-review/checks.txt`, `pressure-ui.png`, `scare-16x9.png`, `scare-4x3.png` e `scare-wide.png`. Pendentes: balanceamento manual, refinamento das asas junto da etapa 4 e audição/mixagem dos sons provisórios. Nenhuma build gerada.

Proposta: **trava de pressão e ruído**. O jogador inspeciona e manipula três peças acopladas de um trinco enferrujado. Uma entrada mantém pressão; outra desloca uma peça por vez. Mover uma peça altera a tensão das demais. Pistas visuais de folga/vibração e estalos indicam o estado; força excessiva produz ruído e solta a trava. O objetivo é aliviar a sequência correta sem deixar o trinco bater. Variações coerentes por fechadura evitam decorar um único movimento. Não reutilizar o medidor de partida do carro.

- [ ] Prototipar essa lógica em um galinheiro antes de espalhá-la pelo mapa.
- [ ] Tornar o desafio difícil por leitura, coordenação e domínio da mecânica, sem depender de falhas aleatórias inevitáveis.
- [ ] Incluir explicação curta e feedback claro para aprender; equilibrar equipamentos sem permitir vitória automática.
- [ ] Ao errar, uma galinha real do cenário salta em direção à câmera, bate as asas e ocupa praticamente toda a tela.
- [ ] Enquadrar o rosto: olhos, bico e crista claramente reconhecíveis. Evitar câmera dentro da malha, imagem genérica ou apenas um flash.
- [ ] Sincronizar salto, cabeça em primeiro plano, grito e recuperação do jogador; impedir sustos simultâneos e repetição contínua injusta.
- [ ] Integrar falha ao ruído/alerta e preservar quantidade de galinhas, inventário e estado da fechadura.

Aceite: experiência claramente diferente do carro, difícil mas aprendível; susto identificável como galinha em diferentes resoluções; sem perda/duplicação de animais.

## Etapa 4 — Animação dos animais

Status: INTEGRADA, revisão final pendente. Galinhas e vacas receberam deformação procedural das malhas existentes, passos alternados por deslocamento, transição para repouso, bicada, asas, fases distintas e ajuste de altura ao solo. Galinhas no colo e no sítio usam animação. Evidências em `output/remaining-review`: `chicken-peck-flap.png`, `cow-stride.png`, capturas de movimento e `animation-runtime.txt`. Ainda falta certificar contato individual de cada pata em terreno irregular, qualidade das asas e desempenho jogando o ciclo completo; não há rig esquelético completo.

- [ ] Conferir se as malhas de galinhas e vacas têm esqueleto adequado; preparar no Blender quando necessário.
- [ ] Galinhas: passos alternados com contato no chão, bicar abaixando cabeça/pescoço, pausas e bater asas.
- [ ] Reutilizar o movimento de asas no susto, na fuga e ao serem recolhidas, com transições coerentes.
- [ ] Vacas: caminhada quadrúpede, apoio correto das patas, velocidade de passada compatível com deslocamento e transição para repouso.
- [ ] Evitar animais deslizando, girando sobre patas imóveis ou atravessando terreno/cercas.
- [ ] Introduzir variação de ritmo para não sincronizar todo o rebanho; conferir desempenho com vários animais.

Aceite: observar animais parados, andando e reagindo; filmar/capturar quadros intermediários das patas, bicadas e asas, inclusive em terreno irregular.

## Etapa 5 — Casa e evolução do galinheiro do protagonista

Status: INTEGRADA, revisão visual final pendente. Três níveis usam os reparos antigos sem nova cobrança; custos futuros de 1/2/3 kits de tábuas e pregos, com benefícios de alimentação e rendimento de ovos. Tablet mostra nível, próximo benefício e materiais faltantes. Estrutura, cobertura/tela e ninhos/comedouro mudam no cenário; o telhado anterior é ocultado a partir do nível 2. Veios procedurais aplicados às peças identificadas de madeira. Testes de consumo único, falta de recursos, migração e correspondência visual passaram. Capturas `coop-level-0.png` a `coop-level-3.png`. A fachada teve o atlas, normal map e oclusão restaurados; a aplicação procedural agora exclui a casa importada. Captura opening-ui.png revisada em 09/09/2026.

- [ ] Melhorar textura das madeiras da casa: escala dos veios, orientação por peça, acabamento e variedade de tábuas; conferir de perto e à noite.
- [ ] Melhorar o galinheiro visual e funcionalmente: estrutura, tela, telhado, porta, ninhos e comedouro coerentes com os animais.
- [ ] Propor níveis de melhoria com mudanças visuais claras e benefícios identificados. Custos/capacidades serão definidos no balanceamento.
- [ ] Disponibilizar melhorias no tablet, mostrando nível atual, próximo nível, benefício e requisitos.
- [ ] Exibir por material a quantidade possuída, exigida e faltante; exemplo de interface: “Tábuas: 3/8 — faltam 5”. O exemplo não fixa o custo real.
- [ ] Comprar/aplicar melhoria somente quando os requisitos forem atendidos; consumir itens uma vez, atualizar a construção e salvar o estado.
- [ ] Migrar os reparos e materiais existentes sem apagar progresso nem cobrar novamente por melhorias já adquiridas.

Aceite: tablet e construção concordam; recursos insuficientes são explicados; compra, recarga do save e mudança de dia mantêm nível, recursos e animais corretos.

## Etapa 6 — História, sono e notícias

Status: INTEGRADA, revisão narrativa final pendente. Abertura apenas em nova partida; visão de decadência após sono com assalto novo, com destroços temporários, câmera animada, seis falas/legendas PT-BR e opção de pular. pendingVision persiste junto do dia/notícias e é limpo na conclusão; checkpoint é atualizado antes/depois do sono. Assaltos entregues ou vendidos são os crimes registrados; outras ações não foram incluídas. Testes cobrem noites pacíficas, vários assaltos, nova notificação, serialização e restauração da câmera. Narração Faber gerada localmente; origem documentada em `AUDIO_PROVENANCE.md` e `AUDIO_MODEL_CARD.md`. Ainda falta assistir e ouvir integralmente as cenas e testar encerramento do processo em diferentes momentos de transição.

- [ ] Escrever um roteiro curto de abertura coerente com o sítio, as dívidas e as motivações já presentes; apresentar a história antes da primeira partida nova.
- [ ] Criar cutscene de abertura com enquadramentos, animação, narração e legendas. Continuar um save não deve repetir a introdução automaticamente.
- [ ] Criar a cutscene de decadência: ao dormir depois de cometer crime, o protagonista vê a ruína que esse futuro pode trazer.
- [ ] Essa cutscene ocorre **exclusivamente** quando há crime novo desde o último sono processado. Uma noite sem crime não a dispara; um crime antigo não a repete indefinidamente.
- [ ] Registrar o evento de crime de forma persistente. O assalto é obrigatório; definir explicitamente quais outras ações já existentes contam como crime antes de incluí-las no gatilho.
- [ ] Depois de cada ciclo com assalto → dormir → acordar, publicar as notícias correspondentes e mostrar uma notificação para lê-las no tablet.
- [ ] Manter a notificação como não lida até a leitura; um novo assalto deve gerar nova notificação mesmo que as notícias anteriores tenham sido lidas.
- [ ] Processar relato, cutscene, avanço do dia e notificação sem duplicação ao salvar/carregar, pular a cena ou interromper o jogo.
- [ ] Permitir pular cutscenes; manter legendas, controle de volume da voz e restauração dos controles.
- [ ] Preparar roteiro e voz em português brasileiro; confirmar disponibilidade/licença da voz e dos áudios antes de tratá-los como definitivos.

Aceite: testar partida nova e continuação; noites sem crime, com um assalto e com vários; pular/reabrir o jogo em transições; nenhuma notificação perdida ou cena indevida.

## Etapa 7 — Sons e mixagem completa

Status: INTEGRADA, mixagem auditiva pendente. Efeitos procedurais de motor/partida, portas, passos, fazendeiros, animais e spray foram ligados ao jogo, além de escopeta, segurança e trava das etapas anteriores. Categorias de volume geral/efeitos/ambiente/narração, atenuação e pool de 12 efeitos. Testes verificaram sinais finitos, amplitude abaixo do limite, carregamento das seis falas e limite de simultaneidade. A síntese dos efeitos permanece provisória; é necessário ouvir cada evento e ajustar variedade, naturalidade, loops e equilíbrio. Não confundir aprovação numérica com aceite auditivo.

- [ ] Motor tentando ligar, ligando com sucesso e falhando.
- [ ] Carro em marcha lenta e andando, com aceleração/desaceleração e transições sem cortes bruscos.
- [ ] Abrir portas; complementar fechamento coerente com a interação.
- [ ] Passos do jogador, com cadência de caminhada, corrida e mancar; diferenciar superfícies quando houver suporte.
- [ ] Fazendeiro: sono, despertar, passos, reações/voz, escopeta e recarga.
- [ ] Animais: galinhas e vacas, chamadas e movimento pertinente.
- [ ] Jumpscare sincronizado com o salto, sem depender apenas de volume excessivo.
- [ ] Trava/invasão: pressão, movimento, estalos, sucesso e erro; preservar identidade sonora distinta da partida.
- [ ] Piche/tinta spray: início, aplicação contínua e término.
- [ ] Áudio espacial para fontes no mundo, atenuação por distância, variedade e limite de sons simultâneos.
- [ ] Volumes separados de efeitos/ambiente/narração; sem clipping, loops presos ou sons continuando após pausa/encerramento indevidamente.
- [ ] Catalogar fonte e licença dos arquivos; sons provisórios devem ser identificados como provisórios.

Aceite: conferir cada evento ouvindo o jogo, inclusive falhas repetidas, câmera afastada, pausa e retorno; finalizar equilíbrio entre pistas de furtividade, motor, voz e susto.

## Etapa 8 — Integração e entrega final

Status: EM VALIDAÇÃO. Regressões específicas de invasão, combate e segurança passaram após integração. Teste amplo do jogo registrou 579 PASS sem FAIL/ERROR em `output/playable-review/playable-tests.txt` em 09/09/2026. Revisão das etapas 4–7 em `output/remaining-review/checks.txt`. Ainda pendentes ciclo manual completo, revisão auditiva e certificação de desempenho/terreno. Build final continua adiada por instrução do usuário. Incidente: um script externo `BuildGameScript.cs` com InitializeOnLoad iniciou uma build automaticamente durante a abertura do teste em 09/09/2026; terminou em `E:/Jogo3D/Build` antes da interrupção. O script reescreveu-se como manual-only. Essa build incidental foi preservada e não é entrega final; não foi solicitada pelo agente.

- [ ] Jogar um ciclo completo: introdução → tablet/rota → invasão → reação dos animais → fazendeiro/combate → fuga → casa/melhoria → dormir/cutscene → notícias/fase 2.
- [ ] Jogar também um dia sem crime, uma derrota e uma continuação de save anterior.
- [ ] Conferir câmera, mãos e tablet após as novas interações; não reintroduzir punhos torcidos, interior do corpo ou aparelho nas mãos.
- [ ] Validar resoluções, controles, desempenho com segurança/animais/áudio ativos e recuperação de estados após interromper ações.
- [ ] Rodar testes relevantes, registrar a revisão visual/auditiva e gerar a build Windows.
- [ ] Atualizar todos os estados deste plano com evidência e listar limitações reais; não declarar concluído com itens pendentes.

## Registro das entregas

| Data | Entrega | Resultado |
|---|---|---|
| 09/09/2026 | Levantamento inicial e plano das oito etapas | Planejamento registrado; próxima entrega: etapa 1 |

## Revisão adicional — alvo de 144 FPS

Padrão migrado uma vez para 144 FPS com VSync desligado; preferências posteriores preservadas. Removidas cópias de listas por quadro no áudio e cálculos repetidos por vértice nas animações. Revisão atual: 50 PASS de integração e 579 PASS da regressão ampla, concluída em 09/09/2026 às 14:43. Medição de 20 segundos: 140,0 FPS no Editor a 1133x833; P99 12,07 ms. Isso não certifica 144 FPS constantes em 1080p. Detalhes e limitações em ENTREGA_OTIMIZACAO.md.

