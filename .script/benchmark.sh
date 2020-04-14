#!/bin/sh
dotnet run -c release --project ../SharpConsole/SharpConsole.Linux.csproj time test_opcode.ch8
gcc -w ../CChip8/CChip8.c ../CChip8/InstrImpl.c -O3 -o CChip8
./CChip8 test_opcode.ch8
