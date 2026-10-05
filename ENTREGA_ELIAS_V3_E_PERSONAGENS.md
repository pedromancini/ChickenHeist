# Elias v3 e alinhamento dos personagens — 05/10/2026

Pedido do usuário: usar o protagonista gerado por IA, deixar os personagens coerentes, sem quebras de assets ou animações.
Nenhuma build nova foi gerada.

## Protagonista

- Modelo novo convertido para o estilo do elenco: low poly facetado, paleta plana, cabeça em proporção dos NPCs, 1,74 m, mãos proporcionais, pele morena natural, sem sujeira pintada nem rasgos. Fonte e script em `Tools/SourceArt/ProtagonistV3`.
- Esqueleto no padrão `ProtagonistRig` com cinco dedos por mão; as 29 animações foram recriadas pelo `EliasNativeInstall` (Humanoid).
- Trocado no jogador, `Protagonist.prefab`, abertura (`OpeningStage`, `VisitorStage`) e cena; a visão do declínio também mostra o Elias novo.
- Instalador: paleta própria (`EliasV3_Palette.png`, `EliasV3.mat`) e corpo de primeira pessoa sem corte no pescoço (o pescoço vem fechado do Blender).

## Correções encontradas na revisão

- Câmera em primeira pessoa: o componente `PlayerFirstPersonView` (câmera 22 cm à frente do tronco) tinha sumido do jogador em uma instalação anterior; olhando para baixo, a câmera ficava sobre o pescoço. Restaurado e garantido pelo instalador.
- Elias flutuava 6,6 cm (margem da cápsula do CharacterController). `ProtagonistArticulation` desconta a margem quando ele está em pé; dirigindo continua igual.
- Moradores afundavam até 11 cm nos passos: `NPCFootContact` passou a usar calcanhar e ponta de cada bota, calibrados da malha por `NPCFootCalibration` (pontos gravados nos componentes, válidos na build). Seu Anselmo, sem cápsula, mede o piso uma vez ao iniciar.
- Resultado (vértice mais baixo da malha sobre o chão): Elias −1,4 cm; moradores entre −2,7 e +2,0 cm; Seu Anselmo +1,7 cm.

## Verificado e mantido

- Fazendeiros dormindo: deitados na cama, dentro do quarto (forro a 2,7 m), braços junto ao corpo. A marca "T-pose?" da `VisualAlignmentReview` é falso positivo dos limites da malha deitada (`FarmerSleepAudit`).
- Janelas de celeiro "flutuando", canteiros "enterrados", peças de galinheiro: posições corretas; falsos positivos do detector de objetos.

## Validação

- Regressão ampla: 579 PASS, 0 FAIL (`RegressionWithoutBuild.Run`).
- Ações do protagonista: 17 PASS (volante, chave, trava, galinha, dedos, cabeça) — `output/articulation-review`.
- Combate e sono do fazendeiro: 44 PASS — `output/farmer-combat-review`.
- Abertura com visitante: 70 PASS (pular em qualquer ponto, câmera e controles restaurados) — `output/visitor-opening`.
- Revisão do Elias (primeira pessoa, fila com o elenco): `output/elias-v3` (`review.txt`, `fp-*.png`, `lineup.png`).
- Contato com o chão: `output/character-ground-audit/report.txt`.

## Backups

`output/elias-v3-backup-20261005-1345`: pasta EliasNative anterior, cena e os três prefabs alterados.

## Pendências

- Build Windows (`E:/Jogo3D/Build`) ainda tem o Elias anterior; gerar quando o usuário pedir.
- Vozes da abertura ainda não gravadas (pendência anterior).
- Animações enviadas pelo usuário (idle, walk, backward, left) guardadas em `Tools/SourceArt/ProtagonistV3/source`, não integradas (ver README).
