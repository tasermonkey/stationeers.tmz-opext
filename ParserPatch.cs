using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Assets.Scripts;
using Assets.Scripts.Objects.Electrical;
using HarmonyLib;
using UnityEngine;
using static Assets.Scripts.Objects.Electrical.ProgrammableChip;
using static Assets.Scripts.Objects.Electrical.ProgrammableChipException.ICExceptionType;

namespace TmzOpExtMod
{
    // Compiles a line if it's one of the mod's opcodes; null for any other line.
    public static class Parser
    {
        public static _Operation TryCreate(ProgrammableChip chip, string lineOfCode, int lineNumber)
        {
            int hash = lineOfCode.IndexOf('#');
            string code = hash < 0 ? lineOfCode : lineOfCode.Substring(0, hash);
            OpInfo op = Ops.Find(FirstWord(code));
            if (op == null)
                return null;
            string[] words = Preprocess(code, lineNumber).Split().Where(word => word.Length > 0).ToArray();
            int operands = words.Length - 1;
            if (operands < op.MinOperands || operands > op.MaxOperands)
                throw new ProgrammableChipException(IncorrectArgumentCount, lineNumber);
            return op.Create(chip, lineNumber, words.Skip(1).ToArray());
        }

        static string FirstWord(string code)
        {
            int start = 0;
            while (start < code.Length && char.IsWhiteSpace(code[start]))
                start++;
            int end = start;
            while (end < code.Length && !char.IsWhiteSpace(code[end]))
                end++;
            return code.Substring(start, end - start);
        }

        // STR, HASH, %binary and $hex, in the order and with the errors of _LineOfCode's constructor.
        static string Preprocess(string code, int lineNumber)
        {
            Localization.RegexResult strings = Localization.GetMatchesForStringPreprocessing(ref code);
            for (int i = 0; i < strings.Count(); i++)
                code = code.Replace(strings.GetFull(i), PackAscii6(strings.GetName(i), lineNumber).ToString(CultureInfo.InvariantCulture));
            code = Replace(code, InvalidPreprocessHash, lineNumber, Localization.GetMatchesForHashPreprocessing,
                name => Animator.StringToHash(name).ToString());
            code = Replace(code, InvalidProcessBinary, lineNumber, Localization.GetMatchesForBinaryPreprocessing,
                name => Convert.ToInt64(name.Replace("_", ""), 2).ToString());
            code = Replace(code, InvalidPreprocessHex, lineNumber, Localization.GetMatchesForHexPreprocessing,
                name => Convert.ToInt64(name.Replace("_", ""), 16).ToString());
            return code;
        }

        delegate Localization.RegexResult Matcher(ref string code);

        static string Replace(string code, ProgrammableChipException.ICExceptionType error, int lineNumber, Matcher matcher,
            Func<string, string> value)
        {
            try
            {
                Localization.RegexResult matches = matcher(ref code);
                for (int i = 0; i < matches.Count(); i++)
                    code = code.Replace(matches.GetFull(i), value(matches.GetName(i)));
                return code;
            }
            catch (Exception)
            {
                throw new ProgrammableChipException(error, lineNumber);
            }
        }
    }

    // The game compiles each line in _LineOfCode's constructor and knows only its own opcodes. For
    // ours, the prefix compiles the line and hands vanilla an empty one; the postfix swaps ours in.
    [HarmonyPatch]
    static class ParserPatch
    {
        static readonly FieldInfo Operation = AccessTools.Field(typeof(_LineOfCode), nameof(_LineOfCode.Operation));
        static readonly FieldInfo Text = AccessTools.Field(typeof(_LineOfCode), nameof(_LineOfCode.LineOfCode));

        static MethodBase TargetMethod() =>
            AccessTools.Constructor(typeof(_LineOfCode), new[] { typeof(ProgrammableChip), typeof(string), typeof(int) });

        sealed class Compiled
        {
            public string Text;
            public _Operation Operation;
        }

        static void Prefix(ProgrammableChip chip, ref string lineOfCode, int lineNumber, out Compiled __state)
        {
            __state = null;
            _Operation operation = Parser.TryCreate(chip, lineOfCode, lineNumber);
            if (operation == null)
                return;
            __state = new Compiled { Text = lineOfCode, Operation = operation };
            lineOfCode = string.Empty;
        }

        static void Postfix(_LineOfCode __instance, Compiled __state)
        {
            if (__state == null)
                return;
            Operation.SetValue(__instance, __state.Operation);
            Text.SetValue(__instance, __state.Text);
        }
    }
}
