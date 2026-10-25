using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Assets.Scripts.Objects.Electrical;
using static Assets.Scripts.Objects.Electrical.ProgrammableChip;

namespace TmzOpExtMod
{
    // One of the mod's opcodes: its operands, how it's built, and its help.
    public sealed class OpInfo
    {
        public readonly string Name;
        public readonly int MinOperands;
        public readonly int MaxOperands;
        public readonly Func<ProgrammableChip, int, string[], _Operation> Create;
        // Each operand as the game's help writes it, e.g. "a(r?|num)", with its colours.
        public readonly string[] Params;
        public readonly string Description;

        public OpInfo(string name, int minOperands, int maxOperands, Func<ProgrammableChip, int, string[], _Operation> create,
            HelpString[] @params, string description)
        {
            Name = name;
            MinOperands = minOperands;
            MaxOperands = maxOperands;
            Create = create;
            Params = @params.Select(p => p.ToString()).ToArray();
            Description = description;
        }
    }

    public static class Ops
    {
        static readonly HelpString Value = REGISTER + NUMBER;

        public static readonly OpInfo[] All =
        {
            new OpInfo("tmsi", 2, 2, (chip, line, args) => new TmsiOperation(chip, line, args),
                new[] { (REGISTER + INTEGER).Var("base"), (REGISTER + INTEGER).Var("count") },
                "Starts a call stack for tmjc and tmjr at stack address base (2 to 511), growing down. Each call saves r0 up to r(count-1), count 0 to 16, and the line to return to. Sets ra to the stack's top. Run it before the first tmjc, at the start of the program."),
            new OpInfo("tmjc", 2, 2 + CallStack.MaxArgs, (chip, line, args) => new TmjcOperation(chip, line, args),
                new[] { REGISTER, INTEGER, Value.Var("a"), Value.Var("b"), Value.Var("c"), Value.Var("d"), Value.Var("e") },
                "Calls a function: saves the line to return to and the registers tmsi chose on the call stack, puts up to five values a-e into r0-r4, and jumps to the line or label. tmjr's value goes into the register r? (r0 to r15)."),
            new OpInfo("tmjr", 0, 1, (chip, line, args) => new TmjrOperation(chip, line, args),
                new[] { Value.Var("value") },
                "Returns from the last tmjc: restores the registers it saved and ra, puts the value (if given) into tmjc's register, and jumps to the line after the tmjc."),
        };

        public static IEnumerable<string> Names => All.Select(op => op.Name);

        public static OpInfo Find(string name)
        {
            foreach (OpInfo op in All)
                if (string.Equals(op.Name, name, StringComparison.Ordinal))
                    return op;
            return null;
        }

        static readonly Regex ColorTag = new Regex("<color\\s*=\\s*['\"]?(?<color>[^'\">]+)['\"]?\\s*>(?<content>.*?)<\\/color>");

        // As ProgrammableChip.MakeString: with paramCount below 0, the opcode in color and every operand
        // in its own colours (the help window); otherwise the operands after the first paramCount, all in
        // color (the editor's hint for what's still to type).
        public static string Example(OpInfo op, string color, int paramCount = -1)
        {
            bool hint = paramCount >= 0;
            var text = new StringBuilder(hint ? string.Empty : $"<color={color}>{op.Name}</color>");
            for (int i = Math.Max(paramCount, 0); i < op.Params.Length; i++)
                text.Append(' ').Append(op.Params[i]);
            return hint ? ColorTag.Replace(text.ToString(), $"<color={color}>${{content}}</color>") : text.ToString();
        }
    }

    // The mod's operations run like the game's own: one Execute is one instruction, and its operands
    // are the game's operand classes, so registers, rr, aliases, defines, labels and constants all work.
    abstract class TmOperation : _Operation
    {
        protected TmOperation(ProgrammableChip chip, int lineNumber) : base(chip, lineNumber) { }

        protected IntValuedVariable Int(string code) =>
            new IntValuedVariable(_Chip, _LineNumber, code, InstructionInclude.MaskDoubleValue, throwException: false);

        protected DoubleValueVariable Number(string code) =>
            new DoubleValueVariable(_Chip, _LineNumber, code, InstructionInclude.MaskDoubleValue, throwException: false);

        protected IndexVariable Register(string code) =>
            new IndexVariable(_Chip, _LineNumber, code, InstructionInclude.MaskStoreIndex, throwException: false);

        protected LineNumberVariable Line(string code) =>
            new LineNumberVariable(_Chip, _LineNumber, code, InstructionInclude.MaskDoubleValue, throwException: false);

        // The housing's memory light, as push and pop flash it.
        protected void Flash(LogicMemoryState state)
        {
            if (_Chip.CircuitHousing is CircuitHousing housing)
                housing._MemoryLight?.Flash(state);
        }
    }

    // tmsi base count
    class TmsiOperation : TmOperation
    {
        readonly IntValuedVariable _base;
        readonly IntValuedVariable _count;

        public TmsiOperation(ProgrammableChip chip, int lineNumber, string[] args) : base(chip, lineNumber)
        {
            _base = Int(args[0]);
            _count = Int(args[1]);
        }

        public override int Execute(int index)
        {
            int @base = _base.GetVariableValue(_AliasTarget.Register);
            int count = _count.GetVariableValue(_AliasTarget.Register);
            CallStack.Init(_Chip._Registers, _Chip._Stack, @base, count, _LineNumber);
            Flash(LogicMemoryState.Write);
            return index + 1;
        }
    }

    // tmjc dest target [a [b [c [d [e]]]]]
    class TmjcOperation : TmOperation
    {
        readonly IndexVariable _dest;
        readonly LineNumberVariable _target;
        readonly DoubleValueVariable[] _args;
        readonly double[] _values;

        public TmjcOperation(ProgrammableChip chip, int lineNumber, string[] args) : base(chip, lineNumber)
        {
            _dest = Register(args[0]);
            _target = Line(args[1]);
            _args = args.Skip(2).Select(Number).ToArray();
            _values = new double[_args.Length];
        }

        public override int Execute(int index)
        {
            int dest = _dest.GetVariableIndex(_AliasTarget.Register);
            int target = _target.GetVariableValue(_AliasTarget.Register);
            // Every value is read before any register changes, so tmjc r0 f r1 r0 swaps them.
            for (int i = 0; i < _args.Length; i++)
                _values[i] = _args[i].GetVariableValue(_AliasTarget.Register);
            CallStack.Call(_Chip._Registers, _Chip._Stack, dest, index + 1, _values, _LineNumber);
            Flash(LogicMemoryState.Write);
            return target;
        }
    }

    // tmjr [value]
    class TmjrOperation : TmOperation
    {
        readonly DoubleValueVariable _value;

        public TmjrOperation(ProgrammableChip chip, int lineNumber, string[] args) : base(chip, lineNumber)
        {
            _value = args.Length > 0 ? Number(args[0]) : null;
        }

        public override int Execute(int index)
        {
            double value = _value?.GetVariableValue(_AliasTarget.Register) ?? 0;
            int returnLine = CallStack.Return(_Chip._Registers, _Chip._Stack, _value != null, value, _LineNumber);
            Flash(LogicMemoryState.Read);
            return returnLine;
        }
    }
}
