# Elias v4/v5: modelo gerado por IA, convertido para o jogo

O Elias atual (v5) vem de `source/Elias_v5_source.glb`, enviado pelo usuário em 08/10/2026. É uma malha única de 970 mil triângulos em **pose A** (braços a 39° abaixo da horizontal), com textura de cor 4K e sem esqueleto. A versão anterior (v4, `source/Elias_v4_source.glb`, 07/10/2026) vinha em pose T e tinha seis dedos em cada mão.

`build_elias_v4.py` reconstrói qualquer um dos dois no padrão `ProtagonistRig`:

- **pose A para pose T:** os braços são levantados até a horizontal, com as palmas para baixo, que é o que o resto do script e as pegadas do jogo esperam. Usa pesos temporários de uma cópia fechada; um modelo que já vem em pose T não é alterado;
- articulações localizadas na própria malha (coluna, ombros, cotovelos, pulsos, quadris, joelhos, tornozelos e dedos dos pés);
- mão com seis dedos (v4): o dedo do meio sobrando é removido, os vizinhos são aproximados e o toco é fechado;
- cinco dedos com três falanges por mão, encontrados como peças separadas da malha;
- proporções do elenco calculadas automaticamente: cabeça com 15,6% da altura (medida a partir do queixo) e braço de 0,574 m do ombro ao pulso (o que as mãos precisam para alcançar a galinha, o volante e o cadeado). A mão não é esticada, só acompanha o pulso. Altura final de 1,74 m;
- malha refeita como uma superfície fechada (remesh por voxels de 1,5 mm) antes da redução: os dedos gerados eram duas metades abertas que se separavam ao dobrar e mostravam o lado de dentro. Com 1,5 mm os vãos entre os dedos ficam limpos;
- **parede interna removida:** as solas dos sapatos geradas são abertas (centenas de furinhos), então o remesh envolve o corpo todo numa parede dupla de 1,5 mm, uma pele por fora e outra virada para dentro. A de dentro atravessava a de fora quando as juntas dobravam e aparecia como triângulos escuros no antebraço. Faces que não enxergam o lado de fora em nenhum de 25 raios são apagadas, e depois a malha toda recebe uma única orientação para fora;
- redução para cerca de 22 mil triângulos, guardando mais detalhe nas mãos, no rosto e no antebraço perto do pulso; pedacinhos soltos deixados pela redução são apagados (eles impediam o cálculo dos pesos);
- cores pintadas transferidas (bake) do modelo original para UVs novas, em 2048 px, sem manchas de cabelo ou barba na pele; a mancha azulada nas laterais da barba é recolorida (só na metade de baixo da cabeça, para não mexer nos olhos); nas mãos e antebraços, pontos muito mais escuros que a pele (sombras pintadas entre os dedos) voltam ao tom da pele;
- pesos pelo "bone heat" do Blender. **No pulso**, tudo depois da articulação segue só a mão e os dedos, tudo antes da transição segue só o antebraço, e entre os dois há uma transição de 6,5 cm. O bone heat deixava a base da palma e do polegar presas ao antebraço, e isso fazia pontas ao dobrar a mão (era o pedaço solto que aparecia no volante);
- pesos das mãos pelos eixos dos dedos; barba e queixo acompanham o osso da cabeça;
- cabeça separada (`ProtagonistHead`) com a barba; os dois lados do corte fechados (o do corpo com a cor da pele do pescoço, o da cabeça com a da barba).

Execução:

```
blender -b --factory-startup --python build_elias_v4.py                      # só prévias em generated/
ELIAS_EXPORT=1 blender -b --factory-startup --python build_elias_v4.py       # grava Elias_Rigged.fbx, EliasHumanoid.fbx e EliasV4_Albedo.png
ELIAS_DEBUG_POSE=1 blender -b --factory-startup --python build_elias_v4.py   # mostra o resultado da pose T (generated/dbg_pose_*.png)
ELIAS_DEBUG_HANDS=1 blender -b --factory-startup --python build_elias_v4.py  # pinta os dedos detectados (generated/dbg_hand_*.png)
ELIAS_DEBUG_REMESH=1 blender -b --factory-startup --python build_elias_v4.py # pinta as peças da mão depois do remesh
```

Depois, no Unity (modo batch): `EliasNativeInstall.Install`, `EliasNativeInstall.CleanStages` e `MixamoInstall.Run` (este último reaplica as animações do Mixamo de `E:\Jogo3D\Animacoes` e termina com `ClipGrounding.Run`, que acerta a altura do quadril quadro a quadro para os pés não entrarem no chão; relatório em `output/mixamo/grounding.txt`).

`ClippingReview.Begin` (modo play) confere se o corpo atravessa alguma coisa: o Elias pegando a galinha, carregando (em pé e agachado), dirigindo e negociando, e depois os moradores, os fazendeiros, as vacas e as galinhas andando livres pelo mapa. Relatório e imagens em `output/clipping-review/`.

Parâmetros por variável de ambiente: `ELIAS_SOURCE` (arquivo em `source/`), `ELIAS_HEAD_SCALE`, `ELIAS_ARM_STRETCH`, `ELIAS_HAND_SCALE`, `ELIAS_HEIGHT`, `ELIAS_TRIS`, `ELIAS_TEX`, `ELIAS_VOXEL`, `ELIAS_WRIST` (início,fim da transição do pulso, em metros), `ELIAS_CAGE`, `ELIAS_KEEP`, `ELIAS_WELD`. O v4 usava `ELIAS_SOURCE=Elias_v4_source.glb ELIAS_HEAD_SCALE=0.74 ELIAS_ARM_STRETCH=1.10 ELIAS_HAND_SCALE=0.92 ELIAS_VOXEL=0.0025`.

Ferramentas de conferência:

- `inspect_source.py -- <arquivo>`: conteúdo de um modelo novo (malhas, texturas) e vistas de frente, de lado e das mãos;
- `inspect_slices.py -- <arquivo>`: fatias horizontais, para ver onde os braços se separam do tronco (pose A ou T);
- `inspect_closed.py -- <arquivo>`: arestas abertas e não-manifold do modelo original;
- `inspect_source_holes.py -- <arquivo>`: tamanho e posição de cada abertura do modelo original (as solas, no v5);
- `inspect_holes.py`, `inspect_winding.py`, `inspect_split.py` e `inspect_wrist_weights.py` (sobre `generated/Elias_v4.blend`): aberturas e orientação das faces da malha final, corpo e cabeça separados, e pesos errados em volta do pulso;
- `wrist_lab.py` (sobre `generated/Elias_v4.blend`, com `LAB_KEEP=1`): dobra o pulso esquerdo como as pegadas do jogo dobram e gera imagens de cima, de lado e de baixo.

No Unity, `HandPoseSheet.Run` mostra as duas mãos em várias animações, e `ProtagonistActionReview.RunBatch` gera vistas das mãos em primeira pessoa no volante, na partida, no cadeado e segurando a galinha (`output/articulation-review/*-eyes-hands.png`), mais a pegada no volante vista de fora (`wheel-grip-*.png`). `SteeringWheelProbe.Run` mede o aro do volante.

A pegada no volante fica em `Assets/Scripts/ChickenHeist/PowerGrip.cs`: o aro passa por dentro da mão fechada, logo depois da linha dos nós dos dedos; os dedos fecham em cascata (o mínimo fecha mais) e o polegar abraça o aro por cima.
