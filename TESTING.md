# In-game checks

The same checks, to answer on a page that saves as you go:
https://claude.ai/artifact/8cSJV5XbKFJUzetLwp8ebV (keep the two in step when a check changes).

Build (`dotnet build -c Release`), start the game and enable **TMZ Opcode Extensions** in the
LaunchPad mod list. A creative world is quickest: an IC Housing with a chip, and the IC10 Inspector
(or a second chip with `get`) to read registers and the stack. Line numbers count from 0, as the
rack's debugger does. Fill in **Got**.

**Results: 0.1.0, 2026-10-25,** game 0.2.6428.27798 with StationeersLaunchPad. All pass except 18, which
was rewritten after its first run (that run saved without showing the call was still open) and
hasn't been run again.

## Loading (the spike: do these first)

If check 2 or 3 fails, the mod's approach (subclassing the game's private operation class) doesn't
load in this game, and the mod needs its fallback; stop there and note what the log says.

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 1 | The game's own log, `%USERPROFILE%\AppData\LocalLow\Rocketwerkz\rocketstation\Player.log` (BepInEx's `LogOutput.log` leaves the mod's messages out unless `WriteUnityLog` is on) | `[TmzOpExt] 0.1.0 loaded`. No errors naming TmzOpExt or Harmony, no `TypeLoadException`. | Pass: both lines are in Player.log. (The first look was in LogOutput.log, where they never appear.) A HarmonyX warning there about `ProgrammableChip._ErrorLineNumber` comes from another mod (SDS FieldPad), not this one. |
| 2 | Put `tmsi 511 16` on line 0 of a housing's chip, `yield` on line 1, `j 0` on line 2 | No error light; the housing runs. | Pass. The IC10 Editor mod showed the opcodes red (fixed since: checks 26–28); the game's editor showed the hints. |
| 3 | Read `ra`, and stack addresses 511 and 510 | `ra` 509; 511 holds 16; 510 holds 511. | Pass: `ra` 509, 511 = 16, 510 = 511 |

## The call demo

```
tmsi 511 16
move r0 42
move r1 43
move r2 44
move r3 45
move r13 2
move r14 8
move r15 80
tmjc r0 fooCalc r1 r15 r14 r13
s db Setting r0
end:
yield
j end
fooCalc:
add r15 r1 r2
mul r15 r15 r3
move r2 999
move r3 998
tmjr r15
```

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 4 | The housing's Setting | 176 | Pass |
| 5 | Registers | r0 176, r1 43, r2 44, r3 45, r13 2, r14 8, r15 80, `ra` 509 | Pass |
| 6 | Stack 509, 508, 507, 494, 492, 491 | 9 (the line after tmjc), 0 (r0), 42 (r0 before the call), 2 (r13), 80 (r15), 511. (chipdata-workspace's `ic10/Tests/Call Demo.ic10`, sent by ic10-link, has comment lines and a loop: 509 is 12 there.) | Pass |

## Recursion, swapping, count 0

Factorial, with `tmsi 511 2`:

```
tmsi 511 2
move r0 5
tmjc r1 fact r0
s db Setting r1
end:
yield
j end
fact:
ble r0 1 one
sub r1 r0 1
tmjc r1 fact r1
mul r1 r1 r0
tmjr r1
one:
tmjr 1
```

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 7 | Setting, r0, r1, `ra` | 120, 5, 120, 509 | Pass |
| 8 | Swap: replace lines 1–14 with `move r0 1` / `move r1 2` / `tmjc r2 swap r1 r0` / `s db Setting r2` / `end:` / `yield` / `j end` / `swap:` / `mul r2 r0 10` / `add r2 r2 r1` / `tmjr r2` | Setting 21 (the function saw r0 2, r1 1); after it, r0 1, r1 2 | Pass |
| 9 | Count 0: `tmsi 511 0` / `move r0 5` / `move r1 9` / `tmjc r1 f` / `s db Setting r0` / `end:` / `yield` / `j end` / `f:` / `move r0 7` / `tmjr` | Setting 7 (r0 isn't saved), r1 still 9 (tmjr without a value), `ra` 509 | Pass |

## Errors

Each is a whole program. The chip should stop with the game's error on that line, as for any other
opcode's error.

| # | Program | Expected | Got |
| --- | --- | --- | --- |
| 10 | `tmsi 511` | IncorrectArgumentCount, line 0, when the code is set | Pass |
| 11 | `tmsi 511 17` | OutOfRegisterBounds, line 0 | Pass |
| 12 | `tmsi 512 4`, then `tmsi 1 4` | StackOverFlow, then StackUnderFlow, line 0 | Pass |
| 13 | `tmsi 511 4` / `tmjr` | StackUnderFlow, line 1 (no call open) | Pass |
| 14 | `clr db` / `tmjc r0 0` | IndexOutOfRange, line 1 (no tmsi) | Pass |
| 15 | `move sp 15` / `tmsi 20 4` / `tmjc r0 0` | StackOverFlow, line 2 (the frame would reach `sp`) | Pass |
| 16 | `tmsi 511 4` / `tmjc ra 0` | OutOfRegisterBounds, line 1 | Pass |
| 17 | `clr db` / `tmsi 511 4` / `jal 3` / `tmjr` | IndexOutOfRange, line 3 (`jal` moved `ra`) | Pass |

## Saving mid-call

The function waits about 5 minutes (600 ticks) before it returns.

```
tmsi 511 4
s db Setting 0
move r0 7
tmjc r1 slow r0
s db Setting r1
end:
yield
j end
slow:
move r2 0
wait:
add r2 r2 1
yield
blt r2 600 wait
add r0 r0 r0
tmjr r0
```

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 18 | Run it. Before saving, read Setting, r2 and `ra`; save, quit to the menu, load | Before saving: Setting 0, r2 below 600, `ra` 502 (the call is open). After loading, r2 carries on from about where it was; when it reaches 600, Setting 14 and `ra` 509. | Not run yet in this form |

## Editor and help

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 19 | Type `tmsi ` in the chip editor | `tmsi` yellow, then a grey `base(r?\|int) count(r?\|int)` | Pass: `tmsi` yellow; the names (`base`) in the editor's text colour and the types grey, as in the game's own hints (`put `'s `device`) |
| 20 | Type `tmjc r0 ` | grey `int a(r?\|num) b(r?\|num) …` after it | Pass: `int` and the types grey, the names (`a`) in the text colour, as for 19 |
| 21 | The editor's help, instruction list | tmjc, tmjr, tmsi listed in order among the others, with their operands and descriptions; searching `tmj` finds them. Player.log: `[TmzOpExt] help window: added tmsi, tmjc, tmjr` | Pass |

## DS-8 Server Rack

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 22 | Player.log, with the rack mod enabled; then with it disabled | `[TmzOpExt] DS-8 Server Rack: its Check tab knows the opcodes`; then `[TmzOpExt] DS-8 Server Rack isn't loaded: nothing to patch` | Pass |
| 23 | The call demo (without `s db Setting r0`) on a socket's chip, in the Debugger tab | Registers as check 5; stack 509 = 9 | Pass |
| 24 | The rack's Check tab on that chip | No "Unknown instruction" | Pass. It warns "Program ends after last line and then stops (no loop)": the program's last line is `tmjr`, which the Check tab doesn't know jumps back. Harmless. |

## IC10 Editor mod

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 26 | Player.log, with the IC10 Editor mod enabled; then with it disabled | `[TmzOpExt] IC10 Editor: it knows the opcodes`; then `[TmzOpExt] IC10 Editor isn't loaded: nothing to patch` | Pass |
| 27 | The call demo in the IC10 Editor; hover over `tmjc` | `tmsi`, `tmjc` and `tmjr` coloured like other instructions, not red, and no error on their operands. The tooltip shows tmjc's operands and description. | Pass |
| 28 | Add a line `tmjr r0 r1` | "Too many arguments" on `r1` | Pass |

## Multiplayer (if you can)

| # | Check | Expected | Got |
| --- | --- | --- | --- |
| 25 | Join a modded host without the mod | LaunchPad refuses: the mod is required | Pass |
