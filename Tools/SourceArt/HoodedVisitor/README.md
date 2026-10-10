# Visitante encapuzado: modelo fornecido, com esqueleto próprio

O visitante da cutscene de abertura vem de `Assets/c12cb2ea-013a-4d05-b832-3f5e1cf0ad28.glb`: uma malha de 98 mil triângulos em pose A, com textura de cor e sem esqueleto. Antes, essa malha era colada em tempo de execução ao esqueleto de um aldeão, com pesos cortados por altura e sem dedos. Por isso o braço quebrava e as mãos ficavam abertas.

`build_visitor.py` monta nele o mesmo padrão de ossos do Elias (`ProtagonistRig`): quadril, coluna, peito, pescoço, cabeça, ombros, braços, antebraços, mãos, cinco dedos com três falanges, coxas, canelas, pés e dedos dos pés.

- **Pose:** a pose A vira pose T com as palmas para baixo, pelo mesmo método do Elias.
- **Articulações:** são localizadas na própria malha. O pescoço fica na proporção do corpo, porque o capuz esconde o pescoço real.
- **Malha:** a superfície, as UVs e a textura originais são mantidas, já que o modelo é fechado tirando alguns furinhos. O sombreamento facetado também é mantido.
- **Pesos:** vêm do "bone heat" do Blender, com três ajustes:
  - o pulso tem uma transição curta (`VISITOR_WRIST`);
  - a mão segue as cadeias dos dedos;
  - a máscara acompanha o osso da cabeça.

Execução:

```
blender -b --factory-startup --python build_visitor.py                    # prévias em generated/
VISITOR_EXPORT=1 blender -b --factory-startup --python build_visitor.py   # grava Visitor_Rigged.fbx e VisitorHumanoid.fbx
VISITOR_DEBUG_HANDS=1 blender -b --factory-startup --python build_visitor.py  # pinta os dedos detectados
```

Depois, no Unity (modo batch), rode `HoodedVisitorInstall.Install`. Ele cria as animações do visitante (parado, respirando, falando, andando para frente e para trás) a partir das mesmas capturas do Elias e coloca o visitante em `VisitorStage.prefab`. `HoodedVisitorInstall.Preview` gera quadros de cada animação em `output/visitor-opening/`.

Na cutscene, os braços dos dois personagens são posicionados por `CinematicArm` e as mãos por `CinematicHand`:

- o cotovelo funciona como dobradiça;
- o antebraço faz a rotação da mão;
- o pulso respeita seus limites.

As mãos tomam a forma do que seguram: a mão espalmada na mesa, o punho batendo na porta, o indicador apertando a tecla e a mão segurando a borda do tablet. `VisitorCinematicReview.Review` confere tudo isso e grava closes das mãos e os ângulos dos pulsos em `output/visitor-opening/`.
