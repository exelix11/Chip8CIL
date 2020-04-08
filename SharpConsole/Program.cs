using System;
using System.IO;
using System.Text;
using System.Linq;
using ConsoleGame;
using DIsplayApp;
using Chip8Sharp;
using System.Diagnostics;
using Chip8Sharp.Instructions;

namespace SharpConsole
{
	class Program
	{
		class DisassemblyProvider 
		{
			public bool DisassembleRam = false;

			Disassembler.Disassembly bin;
			Chip8State state;

			Disassembler dism;
			Chip8Sharp.Instructions.Decoder dec = new Chip8Sharp.Instructions.Decoder();

			public DisassemblyProvider(byte[] ROM, Chip8State state)
			{
				dism = new Disassembler();
				bin = dism.DisassembleProgram(ROM);
				this.state = state;
			}

			public bool IsLabel(UInt16 offset) => bin.Labels.Contains(offset);

			public string DisassembleLine(UInt16 offset) 
			{
				if (offset >= Chip8State.TotalRAM || offset < 0)
					return "<INVALID ADDR>";

				int instructionIndex = (offset - Chip8State.ProgramStart) / 2;
				bool OOB = offset < Chip8State.ProgramStart || instructionIndex >= bin.Entries.Length;

				if (DisassembleRam || OOB)
					return dism.DisassembleLine(state.RAM.Span.Slice(offset));

				return bin.Entries[instructionIndex].Text;
			}
		}

		static void TestJIT() 
		{
			var (task, form) = DIsplayApp.Program.Launch(new System.Drawing.Bitmap(Chip8State.DisplayW, Chip8State.DisplayH));

			Chip8VM vm = new Chip8Sharp.Chip8JIT();
			var ROM = File.ReadAllBytes("F:/test_opcode.ch8");
			vm.LoadBinary(ROM);

			vm.Run();

			form.InvokeDraw(vm.State.VMEM);
			Console.ReadLine();
		}

		static void TimeVms(string romfile) 
		{
			var ROM = File.ReadAllBytes(romfile);

			{
				var a = new Chip8InterpreterDBG();
				a.LoadBinary(ROM);
				Benchmark.Run("DBG interperter", 5000, () => { a.Run(); a.Reset(); });
			}

			{
				var a = new Chip8Interpreter();
				a.LoadBinary(ROM);
				Benchmark.Run("Interperter", 5000, () => { a.Run(); a.Reset(); });
			}

			{
				var a = new Chip8JIT();
				a.LoadBinary(ROM);
				Benchmark.Run("JIT", 5000, () => { a.Run(); a.Reset(); });
			}

			Console.ReadLine();
		}

		[STAThread]
		static void Main(string[] args)
		{
			//TimeVms("F:/test_opcode.ch8");
			//return;

			ConsoleBuffer console = new ConsoleBuffer();
			var (task, form) = DIsplayApp.Program.Launch(new System.Drawing.Bitmap(Chip8State.DisplayW, Chip8State.DisplayH));

			Chip8VM inr = new Chip8Sharp.Chip8InterpreterDBG();
			var ROM = File.ReadAllBytes("F:/test_opcode.ch8");
			inr.LoadBinary(ROM);

			DisassemblyProvider disasm = new DisassemblyProvider(ROM, inr.State);		

			//inr.AddBreakPoint(0x268);

			byte[] OldRegisters = new byte[0x10];
			UInt16 OldI = 0;
			while (true)
			{
				console.Clear();

				int startOffset = inr.State.PC - Console.WindowHeight;
				if (startOffset % 2 != 0) startOffset += 1;

				for (int i = 0; i < Console.WindowHeight - 4; i++)
				{
					UInt16 offset = (UInt16)(startOffset + i * 2);

					if (inr.IsBreakpoint(offset))
						console.Write(offset.ToString("X4") + " | ", ConsoleColor.Red);
					else
						console.Write(offset.ToString("X4") + " | ", ConsoleColor.Gray);
								
					if (disasm.IsLabel(offset))
						console.Write("off_" + offset.ToString("X4") + ":  ", ConsoleColor.Yellow);

					var text = disasm.DisassembleLine(offset);

					if (offset == inr.State.PC)
					{
						console.Write("  > ", ConsoleColor.Green);
						console.WriteLine(text, ConsoleColor.Green);
					}
					else if (text.Contains("off_"))
					{
						var off = text.IndexOf("off_");
						console.Write(text.Substring(0, off));
						console.WriteLine(text.Substring(off), ConsoleColor.Yellow);
					}
					else console.WriteLine(text);

				}

				for (int i = 0; i < 16; i++)
					console.Write($"V{i.ToString("X")}: {inr.State.Register((byte)i).ToString("X2")}   | ", inr.State.Registers.Span[i] != OldRegisters[i] ? ConsoleColor.Red : ConsoleColor.White);
				console.Write($"I: {inr.State.I.ToString("X4")}   |", inr.State.I != OldI ? ConsoleColor.Red : ConsoleColor.White);
				console.Write($"PC: {inr.State.PC.ToString("X4")}   |");
				console.Write($"SP: {inr.State.SP.ToString("X4")}   |");
				console.Write($"DT: {inr.State.DT.ToString("X2")}   |");
				console.Write($"ST: {inr.State.ST.ToString("X2")}   |");

				console.WriteLine("");
				
				OldRegisters = inr.State.Registers.ToArray();
				OldI = inr.State.I;

				console.Write("Space: ", ConsoleColor.Cyan);
				console.Write("step  ");
				console.Write("Q: ", ConsoleColor.Cyan);
				console.Write("run  ");
				console.Write("Z: ", ConsoleColor.Cyan);
				console.Write("run 5  ");
				console.Write("X: ", ConsoleColor.Cyan);
				console.Write("run 10  ");
				console.Write("C: ", ConsoleColor.Cyan);
				console.Write("run 15  ");
				console.Write("V: ", ConsoleColor.Cyan);
				console.Write("run 30  ");
				console.Write("N: ", ConsoleColor.Cyan);
				console.Write("run to jump  ");
				console.Write("B: ", ConsoleColor.Cyan);
				console.Write("toggle BP  ");
				console.Write("P: ", ConsoleColor.Cyan);
				console.Write("Break in VS ");
				console.Write("M: ", ConsoleColor.Cyan);
				console.Write("Disasm RAM ", disasm.DisassembleRam ? ConsoleColor.Green : ConsoleColor.White);

				console.Display();
				if (inr.CheckVMEMUpdate())
					form.InvokeDraw(inr.State.VMEM);

ReadAgain:
				switch (Console.ReadKey(true).Key)
				{
					case ConsoleKey.P:
						Debugger.Break();
						inr.StepInto();
						break;
					case ConsoleKey.Spacebar:
						inr.StepInto();
						break;
					case ConsoleKey.Q:
						inr.Run();
						break;
					case ConsoleKey.Z:
						inr.Run(5);
						break;
					case ConsoleKey.X:
						inr.Run(10);
						break;
					case ConsoleKey.C:
						inr.Run(15);
						break;
					case ConsoleKey.V:
						inr.Run(30);
						break;
					case ConsoleKey.N:
						inr.BreakOnJump = true;
						inr.Run();
						break;
					case ConsoleKey.M:
						disasm.DisassembleRam = !disasm.DisassembleRam;
						continue;
					case ConsoleKey.B:
						var bp = ReadBreakPoint();
						if (inr.IsBreakpoint(bp))
							inr.RemovBreakPoint(bp);
						else
							inr.AddBreakPoint(bp);
						console.UnbufferedClear();
						continue;
					default:
						goto ReadAgain;
				}							
			}
		}

		static UInt16 ReadBreakPoint() 
		{
			Console.SetCursorPosition(0, Console.WindowHeight - 1);
			Console.Write("Break address (0 to cancel): ");
			try 
			{
				return UInt16.Parse(Console.ReadLine(), System.Globalization.NumberStyles.HexNumber);
			}
			catch 
			{
				return 0;
			}
		}
	}
}
