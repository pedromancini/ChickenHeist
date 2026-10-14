# Realidade aumentada: "Planejamento do roubo"

Aplicativo para celular Android que complementa o jogo de PC. Você aponta o celular para uma mesa, e uma das fazendas do jogo aparece em cima dela como uma maquete de uns 60 cm. A ideia vem da própria história: na abertura, o visitante entrega a Elias um tablet com as fazendas. O app é esse "tablet", em realidade aumentada.

## O que aparece na maquete

As 12 fazendas do mundo do jogo são geradas direto da cena `ChickenHeistRuralWorld`, com as mesmas construções, cercas, plantações, árvores e animais, em escala de cerca de 1:160. Sobre cada uma, em camadas que podem ser ligadas e desligadas:

- **Ronda**: o caminho de patrulha do fazendeiro, em vermelho, com o boneco dele andando na velocidade real da ronda (acelerada 4 vezes).
- **Câmeras**: o cone de visão de cada câmera de segurança, em amarelo, varrendo de um lado para o outro como no jogo.
- **Galinhas**: onde ficam as galinhas.
- **Entrada**: a porteira da fazenda, com uma seta verde.

O painel de cima mostra o nome da fazenda e quantas galinhas, câmeras e fazendeiros ela tem.

## Como usar

1. Instale o `ChickenHeist-PlanejamentoRA.apk` no celular. Ele precisa ser compatível com ARCore (a maioria dos Android dos últimos anos é), e, se for a primeira vez, o celular pede para instalar o "Google Play Services para RA".
2. Abra o app e mova o celular devagar apontando para uma mesa, até aparecer a área detectada (uma mancha clara sobre a mesa).
3. Toque na mesa: a fazenda aparece ali.
4. Para mexer na maquete:
   - **1 dedo** gira a maquete;
   - **2 dedos** aproximam ou afastam;
   - **toque duplo** na mesa muda a maquete de lugar.
5. Nos botões de baixo:
   - **◀ Fazenda / Fazenda ▶** troca de fazenda;
   - **Ronda**, **Câmeras**, **Galinhas** e **Entrada** ligam e desligam cada camada;
   - **Reposicionar** recomeça a busca pela mesa.

Sem realidade aumentada (no editor do Unity ou num PC), a maquete aparece na frente da câmera; o mouse gira e a roda aproxima. Assim dá para revisar sem celular.

## Como foi feito

- `Assets/Editor/ARPlanningBuilder.cs`: gera as maquetes (`Assets/ChickenHeistGenerated/AR/Fazenda_XX.prefab`), a cena `Assets/Scenes/PlanejamentoRA.unity` e as configurações do Android.
  - Cada maquete junta as peças da fazenda numa malha só, com um material por parte, para ficar leve no celular.
  - O chão da maquete segue o relevo real do terreno.
- `Assets/Scripts/ChickenHeist/AR/ARHeistPlanner.cs`: o app em si. Detecta a mesa (AR Foundation e ARCore), posiciona a maquete com um toque, trata os gestos e monta a interface com a fonte do jogo.
- `Assets/Scripts/ChickenHeist/AR/ARFarmModel.cs`: anima a maquete (fazendeiro andando, câmeras varrendo) e liga e desliga as camadas.
- Pacotes: `com.unity.xr.arfoundation` e `com.unity.xr.arcore` 6.6.2. O jogo de PC não inclui a cena de realidade aumentada nem carrega o ARCore.

As maquetes (342 MB de malhas) e a cena `PlanejamentoRA.unity` não ficam no git. Depois de clonar o repositório, rode `BuildAll` uma vez para gerá-las a partir do mundo do jogo.

Para gerar de novo (Unity em modo batch, na pasta do projeto):

```
Unity.exe -batchmode -quit -projectPath . -executeMethod ARPlanningBuilder.BuildAll       # maquetes, cena e configurações
Unity.exe -batchmode -quit -projectPath . -executeMethod ARPlanningBuilder.Preview        # prévias em output/ar/
Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod ARPlanningBuilder.BuildAndroid   # Builds/Android/*.apk
```

Requisitos no Unity Hub: o módulo **Android Build Support**, com o SDK, o NDK e o OpenJDK.
