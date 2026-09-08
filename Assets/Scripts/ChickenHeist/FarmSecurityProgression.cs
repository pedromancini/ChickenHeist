using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FarmRaidNews
{
    public string farm;
    public int chickens, day;
    public bool emptied;
    public string Headline => "Assalto em " + farm;
    public string Story => "Um invasor levou " + (emptied ? "todas as galinhas" : chickens + " galinha(s)") +
        " da fazenda " + farm + ". Os fazendeiros da regiao estao se ajudando e instalando cameras e armadilhas para proteger seus galinheiros.";
}

public static class FarmSecurityProgression
{
    public static bool Installed => HouseholdEconomy.Instance?.Account.regionalSecurity == true;

    // Keep the authored objects indexed by checkpoints, but hide every physical
    // part and disable collisions until the farmers install their equipment.
    public static void Apply()
    {
        foreach (var camera in UnityEngine.Object.FindObjectsByType<SecurityCamera>()) SetVisible(camera, Installed);
        foreach (var trap in UnityEngine.Object.FindObjectsByType<TrapSystem>()) SetVisible(trap, Installed);
    }

    static void SetVisible(Component item, bool visible)
    {
        foreach (var renderer in item.GetComponentsInChildren<Renderer>()) renderer.enabled = visible;
        foreach (var collider in item.GetComponentsInChildren<Collider>()) collider.enabled = visible;
    }
}
