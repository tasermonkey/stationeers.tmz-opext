using System;
using Assets.Scripts.Objects.Electrical;
using static Assets.Scripts.Objects.Electrical.ProgrammableChipException.ICExceptionType;

namespace TmzOpExtMod
{
    // The call stack in a chip's stack memory (REFERENCE.md has the layout). Every check comes before
    // any write, so an opcode that fails changes nothing. Errors are the game's own.
    public static class CallStack
    {
        public const int Sp = 16;
        public const int Ra = 17;
        public const int MinBase = 2;
        public const int MaxSaved = 16;
        public const int MaxArgs = 5;
        public const int MaxDest = 15;

        // tmsi: the header at base (registers saved per call) and base-1 (base itself, a marker), ra below it.
        public static void Init(double[] reg, double[] stack, int @base, int count, int line)
        {
            if (@base < MinBase)
                throw Error(StackUnderFlow, line);
            if (@base >= stack.Length)
                throw Error(StackOverFlow, line);
            if (count < 0 || count > MaxSaved)
                throw Error(OutOfRegisterBounds, line);
            // The header would sit in what push and pop are using.
            if (@base - 1 < Index(reg[Sp]))
                throw Error(StackOverFlow, line);
            stack[@base] = count;
            stack[@base - 1] = @base;
            reg[Ra] = @base - 2;
        }

        // tmjc: saves the return line, dest and r0.. in a frame below ra, then puts args in r0...
        public static void Call(double[] reg, double[] stack, int dest, int returnLine, double[] args, int line)
        {
            if (dest < 0 || dest > MaxDest)
                throw Error(OutOfRegisterBounds, line);
            Header(reg, stack, line, out int top, out int @base, out int count);
            int lowest = top - 2 - count;
            if (lowest < 0 || lowest < Index(reg[Sp]))
                throw Error(StackOverFlow, line);
            stack[top] = returnLine;
            stack[top - 1] = dest;
            for (int i = 0; i < count; i++)
                stack[top - 2 - i] = reg[i];
            stack[lowest] = @base;
            reg[Ra] = lowest - 1;
            Array.Copy(args, reg, args.Length);
        }

        // tmjr: restores the newest frame's registers and ra, then the value (if any) into its dest.
        // Returns the line to go back to.
        public static int Return(double[] reg, double[] stack, bool hasValue, double value, int line)
        {
            Header(reg, stack, line, out int top, out int @base, out int count);
            if (top + 1 == @base - 1)
                throw Error(StackUnderFlow, line);
            int frame = top + 3 + count;
            if (frame > @base - 2 || (@base - 2 - frame) % (count + 3) != 0
                || !Whole(stack[frame], 0, int.MaxValue, out int returnLine)
                || !Whole(stack[frame - 1], 0, MaxDest, out int dest))
                throw Error(IndexOutOfRange, line);
            for (int i = 0; i < count; i++)
                reg[i] = stack[frame - 2 - i];
            reg[Ra] = frame;
            if (hasValue)
                reg[dest] = value;
            return returnLine;
        }

        // ra must point just below a header or a frame, whose slot holds the base; tmsi not run, ra
        // changed (jal, move) or the stack overwritten all end here.
        static void Header(double[] reg, double[] stack, int line, out int top, out int @base, out int count)
        {
            top = Index(reg[Ra]);
            int link = top + 1;
            if (link < 0 || link >= stack.Length - 1
                || !Whole(stack[link], MinBase, stack.Length - 1, out @base)
                || link > @base - 1
                || stack[@base - 1] != @base
                || !Whole(stack[@base], 0, MaxSaved, out count))
                throw Error(IndexOutOfRange, line);
        }

        // A register as a stack index, rounded as push and pop do. NaN or out of range gives an index
        // far below the stack (-1 is a real value: ra once frames reach slot 0).
        static int Index(double value) =>
            value >= -1e6 && value <= 1e6 ? (int)Math.Round(value) : -1_000_000;

        static bool Whole(double value, int min, int max, out int result)
        {
            bool ok = value >= min && value <= max && value == Math.Floor(value);
            result = ok ? (int)value : 0;
            return ok;
        }

        static ProgrammableChipException Error(ProgrammableChipException.ICExceptionType type, int line) =>
            new ProgrammableChipException(type, line);
    }
}
