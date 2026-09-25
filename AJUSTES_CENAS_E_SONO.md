# Ajustes das cenas e do fazendeiro

- Abertura: pose de repouso fixa, sem caminhada de despedida e sem oscilaÃ§Ã£o procedural dos braÃ§os.
- VisÃ£o apÃ³s roubo: atores 3D temporÃ¡rios Osvaldo/Joana e Elias/Lia, planos alternados com aproximaÃ§Ã£o suave, vozes existentes e ruÃ­nas temporÃ¡rias. A imagem anterior sÃ³ funciona como fallback se o prefab faltar.
- Ainda nÃ£o hÃ¡ animaÃ§Ã£o facial ou sincronizaÃ§Ã£o por fonemas; personagens usam os modelos existentes do jogo.
- Acúmulo de alerta por ruído reduzido em 30%, preservando diferenças entre fazendas.
- Despertar: limiar sobe de 30 para 65. Restless 65â€“77, HalfAlert 78â€“89, Searching a partir de 90; contato visual mantÃ©m perseguiÃ§Ã£o.
- Motor/partida tÃªm categoria VehicleEngine prÃ³pria. NÃ£o acumulam alerta enquanto o fazendeiro estÃ¡ dormindo; continuam audÃ­veis/investigÃ¡veis depois de acordado. InvasÃ£o, animais perturbados e armadilhas continuam contribuindo para despertar.
- Saves existentes nÃ£o sÃ£o apagados nem tÃªm alerta atual zerado por esta mudanÃ§a.

ValidaÃ§Ã£o em andamento: cenas completas com save isolado, retorno da cÃ¢mera, remoÃ§Ã£o dos atores, salto da cena e regressÃ£o do combate.


Validacao final: 43 PASS nas cenas e 44 PASS no combate/sono, sem FAIL. Build Windows concluida com codigo 0; OpeningStage, DeclineStage e as 24 falas conferidos no pacote. Executavel: E:/Jogo3D/Build/ChickenHeist.exe. Balanceamento manual prolongado e sincronizacao labial ainda pendentes.
