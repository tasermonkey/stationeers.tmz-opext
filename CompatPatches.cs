using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Patches that teach other mods this mod's opcodes: the DS-8 Server Rack's Check tab, and the IC10
// Editor. Each looks once for its mod, the way ServerRackOpsPatch explains.
namespace TmzOpExtMod
{
    // The IC10 Editor mod marks any opcode not in IC10Utils._instructions as unknown, and builds its
    // operand checks and tooltips from each one's help string. It fills that table once, lazily, in
    // _initInstructions (from the game's opcodes and IC10Extender's); the postfix adds ours after them.
    static class IC10EditorPatch
    {
        const string AssemblyName = "IC10Editor";
        const string UtilsName = "StationeersIC10Editor.IC10.IC10Utils";
        const string OpCodeName = "StationeersIC10Editor.IC10.IC10OpCode";

        static Type opCode;
        static IDictionary instructions;
        static IDictionary types;

        public static void Apply(Harmony harmony)
        {
            Assembly editor = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == AssemblyName);
            Type utils = editor?.GetType(UtilsName);
            if (utils == null)
            {
                Debug.Log("[TmzOpExt] IC10 Editor isn't loaded: nothing to patch");
                return;
            }
            try
            {
                opCode = editor.GetType(OpCodeName, true);
                instructions = (IDictionary)AccessTools.Field(utils, "_instructions").GetValue(null);
                types = (IDictionary)AccessTools.Field(utils, "_types").GetValue(null);
                harmony.Patch(AccessTools.Method(utils, "_initInstructions"),
                    postfix: new HarmonyMethod(typeof(IC10EditorPatch), nameof(Postfix)));
                // Already filled (the editor opened before this mod loaded): add ours now.
                if (instructions.Count > 0)
                    Postfix();
                Debug.Log("[TmzOpExt] IC10 Editor: it knows the opcodes");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TmzOpExt] IC10 Editor: couldn't patch IC10Utils._initInstructions - did it change? {e}");
            }
        }

        static void Postfix()
        {
            foreach (OpInfo op in Ops.All)
                instructions[op.Name] = Activator.CreateInstance(opCode, op.Name, op.Description, Ops.Example(op, "yellow"));
            // The word list is built from the instructions, also once; if it's built already, ours go in
            // with the type an existing instruction has.
            if (types.Count > 0 && types.Contains("j"))
                foreach (OpInfo op in Ops.All)
                    types[op.Name] = types["j"];
        }
    }

    // The DS-8 Server Rack mod's Check tab marks any opcode not in the game's list as unknown; its list
    // comes from ServerRack.Ops(). LaunchPad loads every enabled mod's assemblies before it starts any
    // of them, so if the rack's isn't loaded by now, the rack isn't in the mod list: one look, no waiting.
    static class ServerRackOpsPatch
    {
        const string AssemblyName = "ServerRack";
        const string TypeName = "ServerRackMod.ServerRack";

        public static void Apply(Harmony harmony)
        {
            Type serverRack = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == AssemblyName)
                ?.GetType(TypeName);
            if (serverRack == null)
            {
                Debug.Log("[TmzOpExt] DS-8 Server Rack isn't loaded: nothing to patch");
                return;
            }
            try
            {
                harmony.Patch(AccessTools.Method(serverRack, "Ops"),
                    postfix: new HarmonyMethod(typeof(ServerRackOpsPatch), nameof(Postfix)));
                Debug.Log("[TmzOpExt] DS-8 Server Rack: its Check tab knows the opcodes");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TmzOpExt] DS-8 Server Rack: couldn't patch ServerRack.Ops - did it change? {e}");
            }
        }

        static void Postfix(HashSet<string> __result)
        {
            foreach (string name in Ops.Names)
                __result?.Add(name);
        }
    }
}
