# Elias v4: modelo gerado por IA, convertido para o jogo

Origem: `source/Elias_v4_source.glb` (enviado pelo usuário em 07/10/2026). É uma malha única de 987 mil triângulos em T-pose, com textura de cor 4K e sem esqueleto.

`build_elias_v4.py` reconstrói o modelo no padrão `ProtagonistRig`:

- articulações localizadas na própria malha (coluna, ombros, cotovelos, pulsos, quadris, joelhos, tornozelos e dedos dos pés);
- **cada mão do modelo gerado tinha 6 dedos**: o dedo do meio sobrando é removido, os vizinhos são aproximados e o toco é fechado;
- cinco dedos com três falanges por mão, encontrados como peças separadas da malha;
- braços alongados em 10% (os gerados eram curtos demais para as mãos alcançarem a galinha, o volante e o cadeado), cabeça reduzida a 74%, altura de 1,74 m;
- pesos pelo "bone heat" do Blender; transição antebraço–mão espalhada por cerca de 13 cm, para a torção do pulso não vincar;
- redução para cerca de 22 mil triângulos, guardando mais detalhe nas mãos, no rosto e no antebraço perto do pulso;
- textura pintada mantida (olhos, sobrancelhas e barba) e reduzida a 2048 px; a mancha azulada nas laterais da barba é recolorida;
- cabeça separada (`ProtagonistHead`) e pescoço fechado para a vista em primeira pessoa.

Execução:

```
blender -b --factory-startup --python build_elias_v4.py                  # só prévias em generated/
ELIAS_EXPORT=1 blender -b --factory-startup --python build_elias_v4.py   # grava Elias_Rigged.fbx, EliasHumanoid.fbx e EliasV4_Albedo.png
ELIAS_DEBUG_HANDS=1 blender -b --factory-startup --python build_elias_v4.py  # pinta os dedos detectados (generated/dbg_hand_*.png)
```

Depois, no Unity (modo batch): `EliasNativeInstall.Install`, `EliasNativeInstall.CleanStages` e `MixamoInstall.Run` (este último reaplica as animações do Mixamo de `E:\Jogo3D\Animacoes`).

Parâmetros por variável de ambiente: `ELIAS_HEAD_SCALE`, `ELIAS_HAND_SCALE`, `ELIAS_ARM_STRETCH`, `ELIAS_HEIGHT`, `ELIAS_TRIS`, `ELIAS_TEX`, `ELIAS_KEEP`, `ELIAS_WELD`.

`inspect_source.py` mostra o conteúdo de um modelo novo (malhas, ossos, texturas) e gera vistas de frente, de lado e das mãos.
