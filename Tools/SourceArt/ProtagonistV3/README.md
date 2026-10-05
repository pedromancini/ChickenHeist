# Elias v3 — protagonista gerado por IA, convertido para o elenco low poly

Origem: `source/Elias_AI_source.fbx` (modelo enviado pelo usuário em 05/10/2026, 97.500 triângulos,
textura pintada com sujeira e rasgos, esqueleto de 24 ossos sem dedos).

`build_elias_v3.py` converte o modelo e o reconstrói no padrão `ProtagonistRig`:

- costuras soldadas; pesos renomeados (Hips, Spine, Spine2, Chest, Neck, Head, ShoulderL, UpperArmL...); osso `Root` acrescentado;
- cabeça reduzida a 68 % (proporção do elenco), altura final 1,74 m com o cabelo, mãos reduzidas a 85 %;
- cinco dedos com três falanges por mão (Thumb/Index/Middle/Ring/Little 1-3 L/R), localizados na própria malha;
- redução para cerca de 13 mil triângulos, protegendo mãos, rosto, ombros e peito (vistos de perto em primeira pessoa);
- textura substituída por paleta plana de 12 cores (uma cor por face, como o atlas dos NPCs); sujeira e rasgos removidos por região do corpo; pele em moreno natural;
- cabeça separada (`ProtagonistHead`), pescoço do corpo fechado para a vista em primeira pessoa.

Execução:

```
blender -b --factory-startup --python build_elias_v3.py                 # só prévias em generated/
ELIAS_EXPORT=1 blender -b --factory-startup --python build_elias_v3.py  # grava Elias_Rigged.fbx, EliasHumanoid.fbx e EliasV3_Palette.png
blender -b --python check_deform.py                                     # estiramento de arestas com dedos e articulações dobrados
```

Depois, no Unity (modo batch): `EliasNativeInstall.Install`, `EliasNativeInstall.CleanStages`.
O instalador recria as 29 animações para o esqueleto novo e troca o corpo no jogador, nas cutscenes e na cena.

Parâmetros por variável de ambiente: `ELIAS_HEAD_SCALE`, `ELIAS_HEIGHT`, `ELIAS_HAND_SCALE`, `ELIAS_TRIS`, `ELIAS_KEEP`, `ELIAS_HEAD_KEEP`.

As animações geradas pelo usuário (`source/mocap_*.fbx`, 67 MB, fora do git pelo `.gitignore`; cópia local e originais em `E:\Jogo3D`) não foram integradas:
o jogo já tem um conjunto completo e coerente (Human Basic Motions + poses autorais); `mocap_Left` não é um passo lateral
e as demais não formam um conjunto direcional completo.
