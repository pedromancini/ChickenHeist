---
name: unity3d-stealth-dev
description: Guia de referência rápida e boas práticas para desenvolvimento de jogos stealth 3D na Unity, abrangendo IA de patrulha, cones de visão (FOV), detecção de som e otimização URP.
---

# Guia de Desenvolvimento Stealth 3D (Unity)

Este guia serve como uma skill de referência rápida para implementar mecânicas de furtividade (Stealth) e gerenciamento de performance 3D em jogos na Unity.

---

## 👁️ 1. Sistema de Visão de IA (Cones de Visão / FOV)

Para fazer uma IA (como o fazendeiro ou câmeras) enxergar o jogador em 3D, usamos a matemática de produto escalar (Dot Product) combinada com Raycasting para detectar obstáculos.

### Lógica Matemática do FOV (C#)
```csharp
public bool CanSeeTarget(Transform viewer, Transform target, float maxDistance, float viewAngle, LayerMask obstacleMask)
{
    Vector3 directionToTarget = (target.position - viewer.position).normalized;
    float distanceToTarget = Vector3.Distance(viewer.position, target.position);

    // 1. Verificar distância
    if (distanceToTarget > maxDistance) return false;

    // 2. Verificar ângulo de visão (FOV)
    float angleToTarget = Vector3.Angle(viewer.forward, directionToTarget);
    if (angleToTarget < viewAngle / 2f)
    {
        // 3. Raycast para verificar se há obstáculos (paredes, feno, etc.) entre eles
        if (!Physics.Raycast(viewer.position, directionToTarget, distanceToTarget, obstacleMask))
        {
            return true; // Jogador está visível!
        }
    }
    return false;
}
```

---

## 🔊 2. Sistema de Som e Ruído (Detecção de Barulho)

O jogador emite ruídos ao correr, pular ou esbarrar em objetos. A IA reage a esses ruídos dependendo da distância e intensidade.

### Lógica de Emissão de Barulho (C#)
```csharp
public interface INoiseReceiver
{
    void OnHearNoise(Vector3 noiseSource, float intensity);
}

public static class NoiseEmitter
{
    public static void EmitNoise(Vector3 position, float radius, float intensity)
    {
        // Encontra todos os colisores na área do ruído
        Collider[] colliders = Physics.OverlapSphere(position, radius);
        foreach (var col in colliders)
        {
            if (col.TryGetComponent<INoiseReceiver>(out var receiver))
            {
                receiver.OnHearNoise(position, intensity);
            }
        }
    }
}
```

---

## 🧠 3. Estados de IA (State Machine)

Uma IA de stealth clássica opera em 4 estados principais:
1. **Patrulha (Patrol):** Anda entre pontos predefinidos tranquilamente.
2. **Alerta/Investigação (Alert/Inspect):** Ouviu um barulho ou viu um vulto e vai até o local inspecionar.
3. **Busca (Searching):** Procura ao redor do último local visto do jogador.
4. **Perseguição (Chase):** Viu o jogador e corre atrás dele.

---

## ⚡ 4. Otimização de Performance 3D (Low Poly / URP)

Para manter o jogo rodando a 60+ FPS (especialmente com muitas fazendas e árvores na cena):

1. **GPU Instancing:** Certifique-se de que os materiais dos prefabs de árvores e pedras tenham a opção **"Enable GPU Instancing"** marcada no Inspector. Isso reduz drasticamente as chamadas de renderização (Draw Calls).
2. **Static Batching:** Marque todas as cercas, casas, moinhos e objetos que não se movem como **Static** (canto superior direito do Inspector). O Unity vai combiná-los em um único grupo de renderização.
3. **Occlusion Culling:** Configure o Occlusion Culling (Window -> Rendering -> Occlusion Culling) para que o Unity não renderize fazendas e árvores que estão atrás das montanhas ou fora da visão da câmera.
4. **LOD (Level of Detail):** Use modelos de árvores com LOD. O próprio pacote *Simple Nature Pack* já vem com LODs configurados nas árvores para reduzir a contagem de polígonos quando vistas de longe.
