using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.UI;
using HarmonyLib;
using UnityEngine;

namespace TmzOpExtMod
{
    // The in-game editor colours a line's opcode and greys in the operands still to type, from the
    // game's own opcode list. This does the same for the mod's, after vanilla has done the rest.
    [HarmonyPatch(typeof(Localization), nameof(Localization.ReplaceCommands))]
    static class EditorColourPatch
    {
        static readonly char[] Space = { ' ' };

        static void Postfix(ref string masterString)
        {
            if (string.IsNullOrWhiteSpace(masterString))
                return;
            string line = masterString.TrimStart();
            string lead = masterString.Substring(0, masterString.Length - line.Length);
            foreach (OpInfo op in Ops.All)
            {
                if (!line.StartsWith(op.Name, StringComparison.Ordinal)
                    || (line.Length > op.Name.Length && line[op.Name.Length] != ' '))
                    continue;
                int typed = line.Split(Space, StringSplitOptions.RemoveEmptyEntries).Length - 1;
                string rest = line.Substring(op.Name.Length).TrimEnd();
                masterString = $"{lead}<color=yellow>{op.Name}</color>{rest} {Ops.Example(op, "darkgrey", typed).Trim()}";
                return;
            }
        }
    }

    // The script help window's instruction list comes from the game's opcode list; this adds the
    // mod's, in alphabetical order among them.
    [HarmonyPatch(typeof(ScriptHelpWindow), nameof(ScriptHelpWindow.Initialize))]
    static class HelpWindowPatch
    {
        static void Postfix(ScriptHelpWindow __instance)
        {
            if (__instance.HelpMode != HelpMode.Functions)
                return;
            try
            {
                List<HelpReference> references = __instance._helpReferences;
                List<HelpReference> instructions = references.Where(r => r.ReferenceValue2 == HelpReference.CommandHash).ToList();
                foreach (OpInfo op in Ops.All)
                {
                    HelpReference reference = UnityEngine.Object.Instantiate(__instance.ReferencePrefab, __instance.FunctionTransform);
                    reference.Setup(Ops.Example(op, "yellow"), op.Description, __instance.DefaultItemImage, "Instruction",
                        Animator.StringToHash(op.Name), HelpReference.CommandHash);
                    HelpReference before = instructions.LastOrDefault(r => string.CompareOrdinal(r.TextString, op.Name) < 0);
                    if (before != null)
                        reference.transform.SetSiblingIndex(before.transform.GetSiblingIndex() + 1);
                    references.Add(reference);
                }
                Debug.Log($"[TmzOpExt] help window: added {string.Join(", ", Ops.Names)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TmzOpExt] help window: {e}");
            }
        }
    }
}
