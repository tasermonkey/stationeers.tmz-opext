using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using LaunchPadBooster;
using UnityEngine;

namespace TmzOpExtMod
{
    // StationeersLaunchPad calls OnLoaded on this, as it does for Fast Vent's plugin.
    public class TmzOpExtPlugin : MonoBehaviour
    {
        public const string Version = "0.1.0";

        public static readonly Mod MOD = new Mod("TmzOpExt", Version);

        public void OnLoaded(List<GameObject> prefabs, ConfigFile config)
        {
            // Clients compile chip code too, for the editor: without the mod they'd show the opcodes as errors.
            MOD.Networking.Required = true;
            var harmony = new Harmony("tasermonkey.tmzopext");
            harmony.PatchAll();
            ServerRackOpsPatch.Apply(harmony);
            IC10EditorPatch.Apply(harmony);
            // Not Ops here: it would run the chip's static constructor before the game does.
            Debug.Log($"[TmzOpExt] {Version} loaded");
        }
    }
}
