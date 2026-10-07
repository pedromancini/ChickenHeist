# Entrega — Melhoria geral da interface

Registrado em 06/10/2026.

## O que mudou

A interface ganhou uma identidade única: painéis escuros e quentes, de cantos arredondados, com detalhe âmbar, títulos em Zilla Slab e texto em Barlow. Antes eram caixas retas do IMGUI, com texto padrão e cor diferente em cada tela.

- **Tema central** (`Assets/Scripts/ChickenHeist/UITheme.cs`): cores, fontes, escala por resolução (`UITheme.Size`), `GUISkin` próprio (botões, abas, campos, sliders, barra de rolagem) e componentes reutilizáveis: `Panel`, `Meter`, `KeyPrompt` (tecla + ação), `HintBar`, `Toast`, `Check` (caixa de seleção que não se deforma) e `Shadowed`.
- **Menu principal e pausa:** cartão flutuante que se ajusta ao conteúdo, ação principal destacada e acentos corrigidos (Sítio do Recomeço, Configurações, Vídeo, Áudio...).
- **Configurações:** abas, resolução, rótulos de seção, caixas de seleção, valores dos sliders em destaque e lista de comandos com teclas desenhadas.
- **HUD em missão:** cartão de missão (fazenda, estado do sono com barra colorida, distância), vida, contador "No colo", mensagens que esmaecem, prompts "E / F / G" com tecla desenhada e minimapa com moldura e legenda.
- **Tablet:** passou de tela branca para o tema escuro. Tem cartões por seção, saldo em destaque, dívidas com estado colorido (quitada, vencida, a vencer), loja em linhas com botão à direita, nível do galinheiro com barra e indicador de notícia não lida na aba Jornal.
- **Entreposto da Mata, mochila, arrombamento, partida da caminhonete, legendas das cutscenes e console dev:** todos no mesmo tema. O prompt duplicado do Entreposto foi removido; ele já vem do HUD de interação.

Os textos de estado do jogo (`statusMessage`, mensagens da economia, notícias) não foram alterados, porque os testes dependem deles. Os acentos foram corrigidos só nos rótulos de tela.

## Fontes

`Assets/Resources/UIFonts/`: Barlow (Regular, SemiBold, Bold) e Zilla Slab (Bold, SemiBold), do repositório google/fonts, sob a SIL Open Font License 1.1. As licenças ficam junto das fontes (`Barlow-OFL.txt` e `ZillaSlab-OFL.txt`) e precisam acompanhar o jogo distribuído.

## Validação

- `RegressionWithoutBuild`: 579/579.
- Revisão visual na build Windows: `ChickenHeist.exe --review-session --menu-review --ui-review`. Capturas em `output/menu-review/`: menu, configurações (1280 × 800 e 800 × 600), pausa em 1920 × 1080, HUD em missão, as 5 abas do tablet, Entreposto (venda, suprimentos, 800 × 600), mochila e legenda de cutscene.
- A revisão visual estava desatualizada em dois pontos, que foram corrigidos. O "Novo jogo" agora recarrega a cena e toca a abertura, e a revisão passou a esperar e pular a cena. A entrega agora exige estar no galinheiro do sítio, e a revisão leva o jogador até lá.
