# Revisão de integração e desempenho — 09/09/2026

Projeto ativo: E:/Jogo3D/ChickenHeist.

## Alterações

- Alvo padrão de 144 FPS, VSync desligado. Configurações anteriores migram uma vez; mudanças posteriores do jogador são preservadas.
- Áudio deixa de copiar duas listas por quadro e prepara os efeitos no carregamento, evitando sintetizá-los na primeira interação.
- Animação pré-calcula os pesos dos vértices e calcula quatro deslocamentos de pernas e três rotações por pose. Animais distantes já usavam frequência reduzida.
- Fachada recupera o atlas original, normal map e oclusão. A textura procedural das tábuas não substitui mais o atlas da casa inteira.
- Notificações do jornal ficam ocultas durante sonho/sono, preservando o estado não lido.
- Narração PT-BR e melhorias de galinheiro, história, notícias e sons estão integradas; fontes de áudio documentadas em AUDIO_PROVENANCE.md.

## Medição

20 segundos no Editor, 132 animais, GTX 1650 e i5-12400F, janela de 1133 × 833:

- Média: 140,0 FPS.
- Mediana: 6,62 ms; percentil 95: 11,27 ms; percentil 99: 12,07 ms.
- Um quadro acima de 16,67 ms; uma coleta Gen0.
- Alvo confirmado: 144; VSync: 0.

Evidências: output/remaining-review/frame-timings.txt e checks.txt. A medição é curta, em cena de revisão, e não comprova 144 FPS constantes em 1080p nem durante uma partida inteira. O encerramento do Editor também reportou JobTempAlloc; não foi atribuído a código do jogo e continua sem diagnóstico conclusivo.

## Limites da validação

A conferência visual confirma a fachada e os painéis. Os testes automatizados cobrem estados e persistência, mas não substituem uma partida manual completa. A qualidade auditiva dos efeitos provisórios e da voz não foi aprovada por escuta nesta sessão. A build final permanece pendente; o executável antigo não contém estas alterações.
