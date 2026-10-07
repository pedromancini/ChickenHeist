# Plano — Variedade do mundo

Registrado em 06/10/2026. Trata do item 5 da revisão geral: as 12 fazendas parecem saídas do mesmo molde, nada diferencia uma fazenda rica de uma pobre, e a fachada da casa do Elias está limpa demais para o interior desgastado.

Este documento é um plano: nada dele foi implementado ainda.

## 1. Diagnóstico

Evidência visual: `output/map-review/world-top.png` (vista de cima do vale) e `output/map-review/eye-00-home-front.png`.

| Problema | Onde está no código | Efeito no jogo |
|---|---|---|
| Todas as fazendas têm o mesmo "pente": uma via central com três ruas transversais e 6 setores em grade 3 × 2 | `ProceduralFarmGenerator.Layout.cs` → `LayoutZones()` e `CreateDesignedFarm()` | De cima e no chão, as 12 fazendas se leem como a mesma planta. A tabela `Roles` só troca qual setor recebe o quê (6 permutações), e o `split` tem 3 valores. |
| Todas têm o mesmo programa: casa, celeiro, silo, marco, poço, galinheiro 14 × 11 m, pasto com vacas, horta, 3 pátios, 1 fazendeiro, 5 pontos de patrulha | `CreateDesignedFarm()` | Não existe uma fazenda "de pomar" ou "de granja". O jogador não tem motivo para preferir uma fazenda a outra. |
| Os nomes prometem tipos que o conteúdo não entrega (Leiteira, Granjeira, Cereais, Pomar, Sítio Antigo, Encosta) | `FarmNames` | A foto no tablet de "Vale das Macieiras – Pomar" não mostra um pomar. |
| Não há riqueza nem pobreza: as cercas, as luzes, os materiais e o estado de conservação são iguais | `CreateLotFence`, `CreateFarmLighting`, materiais compartilhados | O tema do jogo (dívida, desespero, roubar de vizinhos) não aparece no cenário. |
| Os lotes ficam em grade 4 × 3 com pouca variação (largura ±12 %), e a entrada é sempre no lado sul do lote | `GetLotRect()`, `info.entrance = (center.x, lot.yMin)` | Todas as fazendas "olham" para a mesma direção. Sobram grandes áreas vazias entre os lotes (manchas claras no mapa). |
| A casa do Elias tem fachada de tábuas uniformes, sem desgaste, fundação ou detalhe | `PlayerHomeBuilder*.cs` (o desgaste de `HomeWornUpgrade.cs` só cobre o interior) | Por fora não se vê que o sítio está à beira da falência, e o contraste com o interior incomoda. |

## 2. Objetivo e critérios de aceite

1. **Reconhecível:** olhando a foto do tablet ou a vista de cima, o time identifica cada fazenda sem ler o nome. Teste prático: embaralhar as 12 fotos e acertar pelo menos 10.
2. **Riqueza legível:** a 30 m da porteira, em até 5 segundos, dá para dizer se a fazenda é rica, remediada ou pobre.
3. **Escolha com consequência:** fazendas diferentes oferecem risco e recompensa diferentes (galinhas, segurança, fazendeiro), e o tablet mostra isso antes da missão.
4. **Sem regressão:** continuam passando `PlayableVillageTests` (12 fazendas, nomes, missões), `PhaseTwoReview` (câmeras e fios), as rotas dos 12 destinos, `HiddenMarketTests`, o save antigo e a auditoria da release. O orçamento de desempenho de `ENTREGA_OTIMIZACAO.md` é mantido.

## 3. Proposta: dois eixos, tipo × riqueza

Cada fazenda recebe um **perfil**: o tipo (6 tipos, já presentes nos nomes) e a riqueza (rica, remediada ou pobre). Os 12 nomes e a ordem atual **não mudam**, porque saves, testes e fotos do tablet dependem de `identity` e `layoutIndex`.

| # | Fazenda | Tipo | Riqueza | Assinatura visual | Planta | Galinhas | Segurança (fase 2) |
|---|---|---|---|---|---|---|---|
| 1 | Santa Clara | Leiteira | rica | estábulo grande, sala de ordenha, tanque de leite | sede ao fundo | 6 | 3 câmeras, 3 fios |
| 2 | Boa Esperança | Granjeira | remediada | 2 galinheiros em fileira, comedouros | pátio central | 8 | 2 / 2 |
| 3 | São Bento | Cereais | rica | 3 silos, galpão de máquinas, trator | linear na estrada | 5 | 3 / 2 |
| 4 | Vale das Macieiras | Pomar | remediada | pomar em grade ocupando ~40 % do lote (serve de cobertura) | sede ao fundo | 5 | 2 / 2 |
| 5 | Recanto do Cedro | Sítio Antigo | pobre | casa de pau a pique, forno de barro, galinhas soltas no quintal | aglomerado | 3 | 1 / 1 |
| 6 | Alto da Serra | Encosta | remediada | terraços em degraus, muro de pedra | terraços | 5 | 2 / 1 |
| 7 | Três Irmãos | Leiteira | pobre | curral pequeno, 2 vacas magras, cerca remendada | aglomerado | 3 | 1 / 1 |
| 8 | Sol Nascente | Granjeira | rica | galpão aviário comprido, refletores, caixa d'água | linear na estrada | 10 | 3 / 3 |
| 9 | Terra Firme | Cereais | pobre | milharal alto (cobertura), trator enferrujado | linear na estrada | 3 | 1 / 1 |
| 10 | Vista Alegre | Pomar | rica | casa-sede de 2 andares, cerca branca, jardim | sede ao fundo | 6 | 3 / 3 |
| 11 | Riacho Fundo | Sítio Antigo | remediada | riacho com pinguela, roda-d'água | pátio central | 5 | 2 / 2 |
| 12 | Pedra Alta | Encosta | pobre | rochas, casebre, galinheiro improvisado com lona | terraços | 2 | 1 / 1 |

Há 4 fazendas de cada nível de riqueza, e cada tipo aparece em dois níveis diferentes. Os números de galinhas e de segurança são um ponto de partida e devem ser calibrados em teste de jogo.

### 3.1 Linguagem visual da riqueza

| Elemento | Rica | Remediada | Pobre |
|---|---|---|---|
| Cerca | tábuas brancas, porteira pintada | madeira natural | estacas tortas, arame farpado, trechos caídos |
| Construções | pintura nova, telhado cerâmico, calhas | pintura gasta, um remendo | variantes "Gasto –", telhado de zinco enferrujado, lona |
| Luz | refletores no pátio e no galinheiro | 1 ou 2 postes | um lampião na varanda |
| Chão | gramado aparado, caminho de cascalho | terra batida | mato alto, poças |
| Objetos | carro/caminhonete nova, caixa d'água | carroça, tambores | ferro-velho, pneus, roupa no varal |

Os materiais "Gasto –" de `HomeWornUpgrade` já existem e devem ser reaproveitados (mesmo pipeline e mesma regra de não encadear "Gasto – Gasto").

### 3.2 Plantas (quebrar o "pente")

Em vez de um único `LayoutZones`, cada planta tem uma função própria, que devolve as mesmas 6 zonas (casa, celeiro, galinheiro, pasto, plantio, serviço). Assim o resto do gerador continua funcionando.

- **Sede ao fundo** (ricas): alameda longa da porteira até a casa, plantações dos dois lados e galinheiro atrás da casa, por isso mais difícil de alcançar.
- **Pátio central:** construções em volta de um terreiro quadrado. O galinheiro fica à vista da casa.
- **Linear na estrada:** construções enfileiradas junto à estrada, campos atrás. É rápida de entrar e de sair.
- **Aglomerado** (pobres): tudo compacto num canto do lote, e o resto é mato. Lote menor.
- **Terraços** (encosta): setores em degraus, seguindo a inclinação do terreno.

Também passam a variar a **orientação** (a porteira fica voltada para o trecho de estrada mais próximo, e não sempre para o sul) e o **formato** do lote (retângulo, L ou trapézio, com a cerca seguindo o contorno).

### 3.3 Escala do vale

- Trocar a grade 4 × 3 por pontos de ancoragem definidos à mão ao longo das estradas: um povoado com 3 fazendas perto do Entreposto, a fazenda rica isolada no morro (Vista Alegre) e as pobres na beira da mata.
- Preencher os vazios com marcos que também ajudam na orientação: capela, escola rural fechada, ponto de ônibus, mata-burro, lago ou açude, pasto aberto com gado solto.

### 3.4 Fachada da casa do Elias

- Tábuas desalinhadas e manchadas, uma tábua faltando, pintura descascada, base de pedra aparente com mato.
- Telhado com remendo de zinco, calha torta, degrau do alpendre quebrado, vidro trincado tapado com papelão, varal.
- **Melhora com o progresso:** 3 estados ligados às dívidas quitadas, como os níveis do galinheiro (dívidas em aberto → tábuas trocadas → pintura nova). A economia passa a aparecer no cenário.

### 3.5 Ligação com a jogabilidade e a história

- O tablet (aba Fotos) mostra o tipo, a riqueza, o número estimado de galinhas e o risco ("Vigilância: alta / média / baixa").
- Fazendas ricas pagam mais e têm mais segurança. Fazendas pobres são fáceis, mas a notícia do dia seguinte pesa ("família perde as galinhas que eram o sustento"). Isso reforça o conflito moral do Elias sem criar sistema novo. O texto atual da notícia precisa continuar contendo "todas as galinhas" (teste).
- O fazendeiro varia por riqueza, usando os parâmetros que já existem em `FarmerSleepSystem` e `FarmerStateMachine`: na fazenda pobre ele tem sono leve e dorme na rede da varanda; na rica, tem sono pesado, mas a fazenda tem mais luz e câmeras.

## 4. Implementação por fases

| Fase | Conteúdo | Arquivos principais | Estimativa |
|---|---|---|---|
| F0 | Tabela `FarmProfile` (tipo, riqueza, planta, galinhas, câmeras, fios, luzes, cerca). Campos `archetype` e `wealth` em `FarmLayoutInfo`. Testes do perfil. Nenhuma mudança visual. | `ProceduralFarmGenerator.Layout.cs`, `FarmLayoutInfo.cs`, novo teste editor | 0,5 dia |
| F1 | Conteúdo por tipo (assinaturas da tabela) e quantidades por riqueza, reaproveitando prefabs existentes (silos, árvores do pomar, vacas, galinheiros). | `CreateDesignedFarm`, `CreateDesignedCrops`, `CreateMountedSecurity`, `CreatePhaseTwoWires` | 2 dias |
| F2 | Kit de riqueza: cercas, materiais gastos, iluminação e objetos. | `CreateLotFence`, `CreateFarmLighting`, `DressYard`, `HomeWornUpgrade` (generalizar) | 2 dias |
| F3 | As 5 plantas, mais orientação e formato do lote. Atualizar rotas, minimapa, patrulha e entrada. Refazer `FarmerCombatUpgrade`, `PhaseTwoUpgrade` e `PlayerHomeBuilder.HiddenMarket`. | `Layout.cs`, `Routes.cs`, `Roads.cs`, `MissionNavigation` | 3 dias |
| F4 | Fachada desgastada do Elias com 3 estados de progresso. | `PlayerHomeBuilder*.cs`, `HouseholdAccount` (estado), checkpoint | 1,5 dia |
| F5 | Reposicionar lotes no vale e preencher vazios com marcos. Refazer estradas. | `GetLotRect`, `BuildDirtPathLayout`, `CreateForest` | 2 dias |
| F6 | Validação: refazer fotos do tablet, `MapOverviewReview`, rotas dos 12 destinos, suítes completas, desempenho, build e auditoria. | revisões em `Assets/Editor` | 1 dia |

Ordem recomendada: F0 → F1 → F2 → F4 → F6 parcial (já entrega a maior parte do ganho visual com risco baixo) → F3 → F5 → F6 final.

**Técnica para girar os lotes (F3):** gerar a fazenda em torno da origem, como hoje, já que tudo fica dentro da raiz da fazenda. Depois mover e girar a raiz até o lote final e reassentar no terreno (`RuralTreeRoots.GroundAll` e a sondagem de chão dos personagens). `FarmLayoutInfo.lot` continua sendo o retângulo envolvente em coordenadas do mundo, com a orientação num campo novo, para não quebrar quem já o usa.

## 5. Riscos e cuidados

- **A cena é "assada":** várias correções foram aplicadas por scripts de upgrade depois da geração (combate, fase 2, casa, mercado oculto). Antes da F3, documentar e automatizar a ordem completa de reconstrução, para que rodar o gerador de novo não apague nada.
- **Saves:** os checkpoints guardam índice e nome da fazenda. Manter a ordem e o `identity`, e testar o carregamento de um save da versão atual.
- **Fotos do tablet:** precisam ser recapturadas sempre que uma fazenda mudar.
- **Desempenho:** mais objetos por fazenda. Usar o batching estático já configurado e conferir draw calls na rota mais pesada.
- **Teste "toda fazenda tem câmeras e fios":** continua válido, porque as pobres ficam com 1 de cada.

## 6. Decisões para o time

1. Girar e mudar o formato dos lotes (F3 e F5) é o maior custo e risco. Fazer agora ou parar após F0–F2 + F4?
2. Usar a camada moral nas notícias (vítimas pobres)?
3. A fachada do Elias deve melhorar com o progresso (3 estados) ou ficar só desgastada?
