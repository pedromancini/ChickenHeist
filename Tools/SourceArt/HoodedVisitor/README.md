# Visitante encapuzado: modelo fornecido, com esqueleto próprio

O visitante da cutscene de abertura vem de `Assets/c12cb2ea-013a-4d05-b832-3f5e1cf0ad28.glb`: uma malha de 98 mil triângulos em pose A, com textura de cor e sem esqueleto. Antes, essa malha era colada em tempo de execução ao esqueleto de um aldeão, com pesos cortados por altura e sem dedos. Por isso o braço quebrava e as mãos ficavam abertas.

`build_visitor.py` monta nele o mesmo padrão de ossos do Elias (`ProtagonistRig`): quadril, coluna, peito, pescoço, cabeça, ombros, braços, antebraços, mãos, cinco dedos com três falanges, coxas, canelas, pés e dedos dos pés.

- **Pose:** a pose A vira pose T com as palmas para baixo, pelo mesmo método do Elias.
- **Articulações:** são localizadas na própria malha. O pescoço fica na proporção do corpo, porque o capuz esconde o pescoço real.
- **Malha:** a superfície e as UVs originais são mantidas, já que o modelo é fechado tirando alguns furinhos. O sombreamento facetado também é mantido.
- **Low poly:** no fim, a malha é reduzida para cerca de 11 mil triângulos (`VISITOR_TRIS`; com 0 fica inteira). As mãos e a faixa dos olhos guardam a densidade original, porque os dedos dobram e seguram o tablet nos closes. Os pesos e as UVs passam junto.
- **Pesos:** vêm do "bone heat" do Blender, com quatro ajustes:
  - o pulso tem uma transição curta (`VISITOR_WRIST`);
  - a mão segue as cadeias dos dedos;
  - a máscara acompanha o osso da cabeça;
  - cada antebraço tem um osso de torção (`ForearmTwistL/R`), igual ao do Elias, para a rotação da mão não dar nó no pulso.

Execução:

```
blender -b --factory-startup --python build_visitor.py                    # prévias em generated/
VISITOR_EXPORT=1 blender -b --factory-startup --python build_visitor.py   # grava Visitor_Rigged.fbx e VisitorHumanoid.fbx
VISITOR_DEBUG_HANDS=1 blender -b --factory-startup --python build_visitor.py  # pinta os dedos detectados
```

Em seguida rode `python pad_texture.py`. Ele pinta cada faceta da textura com uma cor só, a média dos pixels que ela cobre, como o Elias. A faixa dos olhos fica com o desenho em 4 cores chapadas (`VISITOR_EYE_COLOURS`), para os olhos continuarem legíveis. Depois preenche as folgas entre as ilhas de UV.

Depois, no Unity (modo batch), rode `HoodedVisitorInstall.Install`. Ele cria as animações do visitante (parado, respirando, falando, andando para frente e para trás) a partir das mesmas capturas do Elias, convertidas pelo avatar da pose T do pacote (`HumanM_TPose.fbx`, ver `EliasNativeInstall.ImportTake`), e coloca o visitante em `VisitorStage.prefab`. `HoodedVisitorInstall.Preview` gera quadros de cada animação em `output/visitor-opening/`.

Na cutscene, os braços dos dois personagens são posicionados por `CinematicArm` e as mãos por `CinematicHand`:

- o cotovelo funciona como dobradiça;
- o antebraço faz a rotação da mão;
- o pulso respeita seus limites.

As mãos tomam a forma do que seguram: a mão espalmada na mesa, o punho batendo na porta, o indicador apertando a tecla e a mão segurando a borda do tablet. `VisitorCinematicReview.Review` confere tudo isso e grava closes das mãos e os ângulos dos pulsos em `output/visitor-opening/`.
