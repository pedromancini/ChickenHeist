using System.IO;
using UnityEditor;

[InitializeOnLoad]
public static class OutputFolders
{
    static readonly string[] Folders={"output/ar","output/articulation-review","output/cast-audit","output/character-ground-audit","output/clipping-review","output/coop-pressure-review","output/cow-review","output/cutscene-two","output/elias-gait","output/elias-native","output/elias-v3","output/elias-v3/clips","output/elias-v3/runtime","output/farmer-combat-review","output/farmer-sleep-audit","output/gameplay-fix-review","output/hand-poses","output/health","output/home-experience","output/home-review","output/home-review/coop","output/hooded-rig","output/map-review","output/map-review/play","output/mixamo","output/mocap","output/phase-two-gameplay","output/phase-two-review","output/playable-review","output/player-home","output/playtest-probe","output/polish-round","output/protagonist-review","output/remaining-review","output/scene-backups","output/tablet-body-review","output/tree-review","output/uniform-cast","output/usage-audit","output/villager-review","output/visitor-opening","output/visitor-opening/visitor-","output/visual-alignment-review","output/walker-review","output/wheel-grip","output/world-review"};
    static OutputFolders(){foreach(var folder in Folders)Directory.CreateDirectory(folder);}
}
