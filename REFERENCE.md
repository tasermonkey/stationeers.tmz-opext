# tmz-opext reference

What the opcodes do, exactly. [README.md](README.md) has the overview. IC10 Bench's
`@tasermonkey/ic10-bench-opext` follows this page, so tests on the bench match the game.

[ic10-instructions.json](ic10-instructions.json) describes the opcodes for editors (their operands,
which one is written, which is the jump target, and the descriptions from `Ops.cs`). The build copies
it into the mod's folder, where the IC10 extension for VS Code finds it while the mod is enabled.
ic10-bench-opext has the same file: change all three together.

## Opcodes

| Opcode | Operands | Does |
| --- | --- | --- |
| `tmsi base count` | base: 2–511. count: 0–16. Registers, defines and aliases work; register values are rounded. | Writes the header at `base` and `base-1` and sets `ra` to `base-2`. Each call saves r0…r(count-1); the return line is always saved. |
| `tmjc r? target [a [b [c [d [e]]]]]` | r?: r0–r15, where tmjr's value goes. target: a line number, label, define or register, as for `j`. a–e: numbers or registers. | Reads every operand first. Saves a frame below `ra`, puts a–e in r0–r4, and jumps to target. |
| `tmjr [value]` | value: a number or register (the callee's). | Reads value first. Restores the newest frame's registers and `ra`, puts value in that frame's r? (if a value was given), and jumps to the line after its tmjc. |

Each is one instruction, however much it copies.

## The stack

Frames grow down from `base`; push and pop still use the stack from 0 up, with `sp`. For
`tmsi 511 16`:

| Address | Holds |
| --- | --- |
| 511 (`base`) | 16: the registers each call saves |
| 510 (`base-1`) | 511: `base` itself, a marker that this is a header |
| 509 | Frame 1: the line to return to (the tmjc's line + 1, lines counted from 0) |
| 508 | Frame 1: r?'s number (0–15) |
| 507 … 492 | Frame 1: r0 … r15 |
| 491 | Frame 1: `base` (511), so a nested tmjc finds the header |
| 490 … 472 | Frame 2, and so on |

- `ra` is always the next free address below the newest frame (509 with no call open, 490 with one).
  The address above it, `ra+1`, always holds `base`.
- A frame takes count + 3 addresses. From 511 with count 16 and nothing pushed, calls nest 26 deep.
- `sp` isn't saved: what a function pushes is still there when it returns.

## Steps

| Opcode | In order |
| --- | --- |
| `tmsi` | Checks base, count, and that `base-1` is at or above `sp`. Writes `stack[base] = count`, `stack[base-1] = base`, `ra = base-2`. |
| `tmjc` | Reads r?, target and a–e. Checks r? is r0–r15 and the header (below). The frame's lowest address, `ra-2-count`, must be at or above `sp` and 0. Writes `stack[ra]` = return line, `stack[ra-1]` = r?'s number, `stack[ra-2-i]` = r*i* for i < count, `stack[ra-2-count]` = base; then `ra` = `ra-3-count`, a–e into r0–r4, and jumps. |
| `tmjr` | Reads value. Checks the header; `ra+1 = base-1` means no call is open. The frame starts at `P = ra+3+count`, which must be at most `base-2` and a whole number of frames below it; `stack[P]` must be a line (≥ 0) and `stack[P-1]` 0–15. Restores r*i* = `stack[P-2-i]`, then `ra = P`, then r? = value if given, and jumps to `stack[P]`. |

The header check: `ra+1` is from 0 to 510, `stack[ra+1]` is a whole number `base` from 2 to 511 with
`ra+1 ≤ base-1`, `stack[base-1] = base`, and `stack[base]` is a whole number 0–16.

All checks come before any write: an opcode that stops the chip changes nothing.

## Errors

The game's own error types, so the housing shows them as it does any other.

| Error | When |
| --- | --- |
| IncorrectArgumentCount | Wrong number of operands. When the code compiles, like any opcode. |
| InvalidInteger, IncorrectVariable, OutOfRegisterBounds | An operand the game's own operand checks reject (e.g. `tmsi 1.5 4`, `tmjc r20 …`). |
| StackUnderFlow | tmsi base below 2. tmjr with no call open. |
| StackOverFlow | tmsi base above 511, or its header would reach `sp`. tmjc's frame would reach `sp` or go below 0. |
| OutOfRegisterBounds | tmsi count outside 0–16. tmjc r? outside r0–r15 (`sp`, `ra`, or an alias to them). |
| IndexOutOfRange | No header at `ra` (tmsi not run, `ra` changed, the stack cleared or overwritten), or tmjr finds a damaged frame. |

## Hazards (not checked)

| What | Why |
| --- | --- |
| `jal`, `b*al`, `j ra` | They use `ra`, the call stack's pointer. Don't use them while a call is open. |
| Changing the code mid-call | The game resets `sp` and the line, but not `ra` or the stack. Run tmsi at the program's start. |
| An argument in a register the frame doesn't save | tmjc r0 f 1 2 3 with count 2: r2 = 3 stays after the return. |
| push after tmjc | push can grow into the frames; tmjc only checks `sp` when it runs. |
| `poke`, `put`, `clr` | Can overwrite or clear frames and the header. |
| target out of range | As `j`: past the last line stops the chip. |

Calls survive saving and loading: the game saves registers and stack, and the opcodes keep nothing
else.

## Game hooks

| Patch | What |
| --- | --- |
| `ProgrammableChip._LineOfCode` constructor, prefix and postfix | Compiles our lines; vanilla gets an empty line, then ours is swapped in. |
| `Localization.ReplaceCommands`, postfix | Colours our opcodes in the editor and adds the grey operand hint. |
| `ScriptHelpWindow.Initialize`, postfix | Adds our opcodes to the help window's instruction list. |
| DS-8 `ServerRack.Ops`, postfix | Adds our opcodes to the rack's Check tab, once that mod is loaded. |
| IC10 Editor `IC10Utils._initInstructions`, postfix | Adds our opcodes to that editor's instruction table (an `IC10OpCode` each, from the same help string as the game's help window), so it colours them, checks their operands and shows their tooltips. Added at once if the table is already filled. |
