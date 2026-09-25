# Integração do protagonista — 9 de setembro de 2026

Projeto ativo: `E:\Jogo3D\ChickenHeist`.

Modelo de origem: `Assets/69f0dedb-960e-44f9-8794-a2019f4656e6.glb`, preservado.
O arquivo não tinha esqueleto. A derivação foi preparada no Blender com corpo,
cabeça, braços, pernas e 30 articulações dos dedos. As mãos foram reconstruídas
com anéis de vértices para permitir flexão sem as deformações da malha gerada.

## Assets e ações

- Prefab: `Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab`.
- 27 clipes, incluindo locomoção direcional transferida de Human Basic Motions FREE.
- Poses complementares de agachamento, pegar/carregar galinha, direção, partida e lockpick.
- Cabeça e pescoço acompanham a câmera, com limites e suavização.
- Contato das mãos calculado nos objetos para volante, chave, cadeado e galinha.
- Tablet em paisagem, aberto imediatamente por TAB, sem aparelho ou animação nas mãos.
- Corpo completo e cabeça preservados no espelho; malha própria de braços/pernas na câmera em primeira pessoa, com roupa interna fechada.
- Câmera a 22 cm à frente do eixo do tronco ao andar; posição original preservada ao dirigir.
- Antebraços e cotovelos reconstruídos sob as mangas; punhos neutros ao carregar a galinha.
- Os NPCs continuam usando o sistema anterior.

## Fontes e validação

Fonte Blender instalada: `Tools/SourceArt/ProtagonistV2/Protagonist_Rigged.blend`.
Scripts de preparação e FBXs extraídos do pacote ficam junto da fonte.
O script `build_character.py` ainda aponta para o GLB no projeto E: e gera em `generated`
relativo à sua própria pasta. O pacote original continua em `Assets/Animations`.

Testes de interação: `output/articulation-review/checks.txt`.
Revisão do tablet, câmera e galinha: `output/tablet-body-review`.
Testes gerais: `output/playable-review/playable-tests.txt`.
Build: `output/playable-review/windows-build.txt`.
Executável: `Builds/Windows/ChickenHeist.exe`.

As ações específicas combinam poses autorais simples com controle procedural;
não são capturas de movimento completas. As mãos reconstruídas têm acabamento
estilizado e podem precisar de refinamento artístico em tomadas muito próximas.
Os testes de distância não certificam ausência de interseção em toda combinação
de câmera, terreno, proporção e posição de objetos.

Os backups dos arquivos substituídos estão em `output/animation-upgrade-backup-*`.
Backups desta revisão: `output/tablet-fix-backup-*`.
