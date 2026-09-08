# Caminhonete do protagonista

Pedido de 08/09/2026. Implementada após a progressão regional e a animação de carregar galinhas.

- O protagonista possui uma caminhonete pequena, velha, muito danificada e visualmente coerente com sua situação financeira.
- O jogador compra gaiolas na loja para transportar as galinhas na caminhonete.
- A capacidade máxima total do veículo é de **8 galinhas**, independentemente da mochila.
- Integrar pegar no colo, levar ao veículo, colocar na gaiola, retirar e entregar no sítio.
- Manter contagem e salvamento únicos: mover entre colo, gaiola e sítio não duplica galinhas.
- Gaiolas: R$ 100 cada, duas galinhas por gaiola, até quatro gaiolas.
- Controles: F entrar/sair; W/S acelerar/ré; A/D virar; Espaço frear; E colocar e R retirar na caçamba; G entregar no sítio.

Estado atual: mochila e carga do veículo têm contagens separadas. Uma galinha é mostrada no colo quando há carga na mochila; as galinhas transportadas aparecem nas gaiolas. Compras, carga, posição e inclinação do veículo estacionado participam do salvamento.

Revisão de realismo: carroceria modelada em Blender (`Tools/SourceArt/FarmPickup.blend`), quatro rodas independentes, Rigidbody de 1.200 kg mais carga, tração traseira, suspensão com molas e amortecedores, freios nas rodas, direção Ackermann, limite de esterço por velocidade e colisores de carroceria. Física simulada em FixedUpdate. Fonte técnica: https://docs.unity3d.com/6000.0/Documentation/Manual/WheelColliderTutorial.html
