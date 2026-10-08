# Animações do Elias pelo Mixamo

O Elias já aceita animações humanas de qualquer fonte (Mixamo, Asset Store, captura de movimento). Basta baixar o arquivo, salvar com o nome certo em `E:\Jogo3D\Animacoes` e rodar a instalação: ela adapta a animação ao esqueleto do Elias e substitui a animação atual daquele estado no jogo.

## Como baixar no Mixamo

1. Entre em **mixamo.com** com uma conta Adobe (é grátis).
2. **Não precisa subir o Elias.** Use o personagem padrão do site (Y Bot); a adaptação para o Elias é feita depois, no projeto.
3. Pesquise o termo da tabela e escolha a animação que mais se parece com a descrição. Os nomes no Mixamo variam um pouco, então escolha olhando a prévia.
4. Se aparecer a caixa **In Place** (nas animações de andar e correr), **marque**.
5. Clique em **Download** com estas opções:
   - Format: **FBX for Unity (.fbx)**
   - Skin: **Without Skin**
   - Frames per Second: **30**
   - Keyframe Reduction: **none**
6. **Renomeie** o arquivo para o nome da coluna "Salvar como" e coloque em `E:\Jogo3D\Animacoes`.

Nas ações de carregar galinha, arrombar, dirigir e dar partida, as mãos são posicionadas pelo jogo. Por isso, nessas animações, escolha pela **postura do corpo** (agachado, sentado, inclinado); a posição exata das mãos não importa.

## Prioridade 1: o que mais melhora o jogo

Hoje estas são poses feitas à mão, e são as que mais aparecem "duras".

| Salvar como | Pesquisar no Mixamo | Como deve parecer | In Place |
|---|---|---|---|
| `CrouchIdle.fbx` | crouch idle | Agachado parado, olhando para a frente | — |
| `Crouch.fbx` | crouched walking | Andando agachado, devagar | marcar |
| `Pickup.fbx` | picking up | Abaixa e pega algo do chão com as duas mãos | — |
| `Carry.fbx` | carry / box idle | Em pé, segurando algo na frente do peito com os dois braços | — |
| `Lockpick.fbx` | inspecting / fixing / working | **Em pé** (no máximo levemente curvado), mexendo com as duas mãos em algo na altura do peito. Ajoelhado não serve: o cadeado fica na altura do peito de quem está em pé | — |
| `Drive.fbx` | driving | Sentado com as mãos no volante (também é usada para dar a partida) | — |
| `Trade.fbx` | talking / handing | Gesto curto de conversar ou entregar algo | — |

## Prioridade 2: opcional

Estas já são captura de movimento real. Troque só se quiser outro estilo.

| Salvar como | Pesquisar no Mixamo | In Place |
|---|---|---|
| `Idle.fbx` | idle | — |
| `Walk.fbx` | walking | marcar |
| `Run.fbx` | running | marcar |
| `WalkBackward.fbx` | walking backwards | marcar |
| `WalkLeft.fbx` | left strafe walking | marcar |
| `WalkRight.fbx` | right strafe walking | marcar |
| `RunBackward.fbx` | running backward | marcar |
| `RunLeft.fbx` | left strafe | marcar |
| `RunRight.fbx` | right strafe | marcar |
| `Jump.fbx` | jump | marcar |
| `Fall.fbx` | falling idle | — |
| `Talk.fbx` | talking (abertura do jogo) | — |
| `Breathe.fbx` | breathing idle (abertura do jogo) | — |

As diagonais (andar ou correr na diagonal) continuam com a captura atual mesmo se `Walk` e `Run` forem trocados.

## Instalação (feita no projeto)

```
Unity.exe -batchmode -quit -projectPath . -executeMethod MixamoInstall.Run
```

- Lê `E:\Jogo3D\Animacoes`. Também reconhece os nomes originais do Mixamo mais comuns (`Walking.fbx`, `Crouched Walking.fbx`...); arquivos com nome desconhecido são ignorados e listados no relatório.
- Antes de substituir, guarda a animação atual de cada estado em `Assets/ChickenHeistGenerated/Characters/EliasNative/Backup/`.
- Relatório em `output/mixamo/install.txt` (o que entrou, o que foi ignorado, o quanto cada animação mudou) e quatro quadros de cada animação em `output/mixamo/<estado>.png`.
- Para testar sem mexer no jogo: `-executeMethod MixamoInstall.Trial -mixamoSource <pasta>`.

Depois da instalação ainda é preciso conferir no jogo, principalmente a posição sentado na caminhonete, o agachamento no galinheiro e a primeira pessoa.
