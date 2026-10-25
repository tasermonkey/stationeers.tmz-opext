# tmz-opext (TMZ Opcode Extensions)

A Stationeers mod that adds IC10 opcodes for calling functions with a real call stack. A call saves
the registers you choose and the line to return to, passes up to five values, and comes back with
one: one instruction each way.

| Opcode | Does |
| --- | --- |
| `tmsi base count` | Starts a call stack at stack address `base`, growing down. Each call saves r0…r(count-1) (0–16 registers). Run it first. |
| `tmjc r? target a b c d e` | Calls `target` (a line or label) with up to five values in r0–r4. The return value lands in `r?`. |
| `tmjr value` | Returns from the last call: restores the saved registers, puts `value` in the caller's `r?`. |

```
tmsi 511 16
move r1 43
tmjc r0 double r1
s db Setting r0   # 86
end:
yield
j end
double:
mul r0 r0 2
tmjr r0
```

- Calls nest, and recurse.
- `ra` is the call stack's pointer, so don't use `jal` while a call is open. `push` and `pop` still
  work, from address 0 up.
- The opcodes are coloured in the editor (the game's, or the IC10 Editor mod's) and listed in its
  help, and the DS-8 Server Rack's chips run them too.

Needs BepInEx with [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad).
In multiplayer every player needs the mod.

[REFERENCE.md](REFERENCE.md) has the stack layout, the steps and the errors.

## Building

`dotnet build -c Release` builds `TmzOpExt.dll` and copies it, `About/` and
`ic10-instructions.json` to `Documents\My Games\Stationeers\mods\TmzOpExt\`. The game is expected in
`C:\Program Files (x86)\Steam\steamapps\common\Stationeers\`; pass `-p:StationeersDirectory=...`
(with a trailing `\`) if it's elsewhere, or `-p:ModDirectory=...` to copy the mod somewhere else.

[TESTING.md](TESTING.md) lists the checks to run in game, and their results.

## License

[AGPL-3.0-or-later](LICENSE), as IC10 Bench.
