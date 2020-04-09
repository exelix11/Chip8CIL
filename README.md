# Chip8CIL
This is a very barebones chip8 emulator: audio, timers and input are not implemented.\
The objective was to benchmark a .NET interpeter, a C interpreter and a CIL dynarec emulator.\
Benchmarks are ran with the .NET core 3 runtime.

## Disclaimer
This is no way an example on how to make a dynamic recompiler in c#, it was made in ~3 days as an experiment and with no prior knowledge of JIT algorithms and CIL assembly.
~~But hey, it has unit tests.~~ 

## Benchmarks
The benchmark consists of running 5000 timess a 470 bytes test ROM for developing emulators
```
RELEASE:
00:00:01.4460532 interperter with debug options
00:00:01.2926303 Interperter
00:00:00.0326472 Recompilation
00:00:00.054255  C interperter (manually formatted)

DEBUG:
00:00:02.2029128 interperter with debug options
00:00:01.8452143 Interperter
00:00:00.1679940 Recompilation
00:00:00.177473  C interperter (manually formatted)
```
In the best case the dynarec engine is ~100 times faster than the C# interpreter and slightly better than the C interperter, really an interesting result.

Would love to benchmark a C JIT but this experiment already took enough time so i'll stick to this for the time being.
