# Uniformidade visual, NPCs, cutscenes e mapa — 24/09/2026

Pedido do usuário: todos os personagens no mesmo estilo low poly e coerentes com a história; revisar e melhorar as cutscenes; NPCs sem vai-e-vem fixo; melhorar o mapa. Nenhuma build nova foi gerada.

## Personagens

- Família única: pacote Medieval People, com roupas recoloridas para o rural atual (`Assets/Editor/CastPalette.cs`). O pacote pinta roupas por faixas de uma paleta 64×64; cada variante troca só faixas seguras para os modelos a que é atribuída.
- Elias: construído a partir de `peasant_3` e encaixado no esqueleto existente do protagonista (`Tools/SourceArt/ProtagonistV2/build_lowpoly_elias.py`). Animações, primeira pessoa, espelho e cutscenes continuam no mesmo rig. Mãos passam a ser simples, como as dos NPCs.
- Fazendeiros (rodízio): camisa cáqui e jeans (`peasant_5`), flanela vermelha (`rich_citizzens_1`), camisa azul e colete (`city_dwellers_1`). Seu Anselmo: avental verde (`peasant_1`). Moradores e atores do "declínio" (Osvaldo, Joana) sem toucas nem corpetes medievais.
- Instalador: `UniformCastUpgrade.Install`. Backup da cena anterior: `output/scene-backups/ChickenHeistRuralWorld-before-uniform-cast.unity`.

## Moradores

`RoadsideWalker` passa a usar uma rede de pedestres pelos acostamentos: escolhas nos cruzamentos, sem retorno imediato, pausas olhando em volta, desvio de quem vem no sentido contrário e raio de 180 m da casa. Revisão: `WalkerBehaviourReview` (0/10 repetitivos, 0 bloqueios em 90 s).

## Cutscenes

- Abertura: a fala 3 filmava o visitante através da porta fechada (tela marrom); agora tem plano externo da varanda.
- Declínio e memória Elias/Lia: enquadramento sobre o ombro, sempre do mesmo lado do eixo da conversa; ninguém cortado ao meio na borda.
- Falas da abertura reescritas (sem voz gravada ainda); roteiro de gravação atualizado em `ROTEIRO_VISITANTE_3D.md`. As falas gravadas do declínio não foram alteradas.

## Mapa

`WorldCohesionUpgrade.Install` (backup: `output/scene-backups/ChickenHeistRuralWorld-before-world-cohesion.unity`):

- Correção de cor global (Volume URP: saturação −22, contraste +10, filtro levemente quente) e pós-processamento na câmera.
- Folhagens neon esmaecidas; atlas Pandazole e ColorAtlas substituídos por cópias esmaecidas (originais intactos); celeiros com vermelho envelhecido e branco-gelo.
- Cercas das fazendas em madeira envelhecida.
- 1.705 árvores isoladas em campo aberto desativadas (não apagadas) para formar pastos; matas, margens de estrada e entornos de fazendas mantidos.
- Materiais gerados do terreno com smoothness 0,1 (antes 0,5).

Geografia, estradas, fazendas e pontos de missão não foram movidos.

## Validação

- Vila jogável: 579 PASS, 0 FAIL (`RegressionWithoutBuild.Run`, modo batch, sem build).
- Combate e sono do fazendeiro: 44 PASS, 0 FAIL.
- Testes atualizados por estarem desatualizados antes desta entrega: entrega no ponto do galinheiro (regra de 10/09), identificador persistente do fazendeiro no checkpoint e camisa interna do modelo antigo.
- Capturas: `output/visual-alignment-review`, `output/walker-review`, `output/map-review/play`.

## Pendências

- Gravar as vozes da abertura com o texto novo.
- Conferência jogando: proporções do Elias em close (pescoço um pouco longo), mãos simplificadas ao segurar objetos e densidade de árvores.
- Mapa: as fazendas continuam em lotes quadrados iguais num grid; variar formato e layout exige regenerar o mundo.
