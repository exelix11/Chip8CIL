# Chip8CIL
This is a very barebones chip8 emulator: audio, timers and input are not implemented.\
The objective was to benchmark a .NET interpeter, a C interpreter and a CIL dynarec emulator.\
Benchmarks are ran with the .NET core 3 runtime.

## Disclaimer
This is no way an example on how to make a dynamic recompiler in c#, it was made in ~3 days as an experiment and with no prior knowledge of JIT algorithms and CIL assembly.
~~But hey, it has unit tests.~~ 

## Benchmarks
The benchmark consists of running 5000 times a [470 bytes test ROM for developing emulators](https://github.com/corax89/chip8-test-rom)
```
Release mode:
00:00:01.5062592 C# Interperter with debug options
00:00:01.4495207 C# Interperter
00:00:00.0115539 C# JIT
00:00:00.0357570 C interpreter
```
In the best case the dynarec engine is ~100 times faster than the C# interpreter and slightly better than the C interperter, really an interesting result.

These benchmarks are run via Github Actions on an ubuntu conatiner and may vary depending on your software environment, you can see an history of benchmarks for each commit [here](https://github.com/exelix11/Chip8CIL/actions)

As @pamidur [noted](https://github.com/exelix11/Chip8CIL/pull/2#issuecomment-613612858) a lot of CPU time in these benchmarks is spent on syscalls, the sprite drawing one in particular. By stubbing these he obtained the following results:
```
8.724984s C# Debug interperter
8.402784s C# Interperter
0.011406s C# JIT
0.290313s C Interpreter
```
Which show a major speed difference between recompilation and the C interpreter.
While this is an impressive result i believe that the version with the syscalls represents a scenario closer to a real-world implementation, eg. an emulator, as parts implemented in C# are going to perform on average worse then native code.

Would love to benchmark a C JIT but this experiment already took enough time so i'll stick to this for the time being.
