using Chip8Sharp.Instructions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Chip8Sharp
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Registers
	{
		public byte V0;
		public byte V1;
		public byte V2;
		public byte V3;
		public byte V4;
		public byte V5;
		public byte V6;
		public byte V7;
		public byte V8;
		public byte V9;
		public byte VA;
		public byte VB;
		public byte VC;
		public byte VD;
		public byte VE;
		public byte VF;

		public static readonly FieldInfo[] Fields = typeof(Registers).GetFields();

		public Span<byte> AsSpan()
		{
			Span<Registers> valSpan = MemoryMarshal.CreateSpan(ref this, 1);
			return MemoryMarshal.Cast<Registers, byte>(valSpan);
		}
	}

	public class Chip8State
	{
		public bool Terminated = false;
		public Registers Registers = default;

		//General purpose registers


		public UInt16 I { get; set; }

		//Timer registers
		public byte DT { get; set; }
		public byte ST { get; set; }

		//Special registers
		public UInt16 PC { get; set; }
		public byte SP { get; set; }

		public UInt16 IR { get; set; }

		//Main memory
		public readonly Memory<byte> RAM = new byte[TotalRAM];

		public const int DisplayW = 64, DisplayH = 32;
		public readonly Memory<byte> VMEM = new byte[DisplayW * DisplayH / 8];
		public bool VMEMUpdated = true;

		//Memory regions
		public const UInt16 ProgramStart = 0x200, TotalRAM = 4096;
		public const UInt16 FontStart = 0x100, FontLenght = 0x100;
		public const UInt16 StackStart = 0x2, StackLength = 0x98;

		public readonly Memory<byte> ProgramRegion;
		//Interpreter regions:
		public readonly Memory<byte> InterpreterRegion;
		public readonly Memory<byte> FontRegion;
		public Span<UInt16> StackRegion => MemoryMarshal.Cast<byte, UInt16>(_StackRegion.Span);

		private readonly Memory<byte> _StackRegion;

		public Chip8State() 
		{
			ProgramRegion = RAM.Slice(ProgramStart);
			InterpreterRegion = RAM.Slice(0, 0x200);
			_StackRegion = InterpreterRegion.Slice(StackStart, StackLength);
			FontRegion = InterpreterRegion.Slice(FontStart, FontLenght);

			ClearState();
		}

		public void ClearState() 
		{
			Terminated = false;

			I = 0;
			DT = 0;
			ST = 0;
			PC = 0;
			IR = 0;
			SP = 0;

			Registers.AsSpan().Fill(0);
			VMEM.Span.Fill(0);
			RAM.Span.Fill(0);
			VMEMUpdated = true;

			Span<byte> Font = stackalloc byte[80] {
				0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
				0x20, 0x60, 0x20, 0x20, 0x70, // 1
				0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
				0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
				0x90, 0x90, 0xF0, 0x10, 0x10, // 4
				0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
				0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
				0xF0, 0x10, 0x20, 0x40, 0x40, // 7
				0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
				0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
				0xF0, 0x90, 0xF0, 0x90, 0x90, // A
				0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
				0xF0, 0x80, 0x80, 0x80, 0xF0, // C
				0xE0, 0x90, 0x90, 0x90, 0xE0, // D
				0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
				0xF0, 0x80, 0xF0, 0x80, 0x80  // F
			};

			Font.CopyTo(FontRegion.Span);
		}

		public void LoadBinary(Span<byte> ROM)
		{
			ROM.CopyTo(ProgramRegion.Span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref byte Register(byte id) => ref Registers.AsSpan()[id];

		public UInt16 ReadInstruction()
		{
			IR = (UInt16)((RAM.Span[PC] << 8) | RAM.Span[PC + 1]);
			return IR;
		}

		bool SkipClock = false;
		public void Jump(UInt16 addr)
		{
			PC = addr;
			SkipClock = true;
		}

		public void StepPC() 
		{
			if (SkipClock)
				SkipClock = false;
			else 
				PC += 2;
		}

		public void ClearVMEM() => VMEM.Span.Fill(0);
	}

	public interface Chip8VM
	{
		Chip8State State {get; set;}

		public void LoadBinary(Span<byte> binary);
		public void Run(uint instructions = uint.MaxValue);

		public bool CheckVMEMUpdate()
		{
			if (!State.VMEMUpdated)
				return false;
			State.VMEMUpdated = false;
			return true;
		}

		public void Reset();

		public bool IsDebuggingSupported { get; }
		public bool BreakOnJump { get; set; }
		public void StepInto();
		public bool IsBreakpoint(UInt16 bp);
		public void AddBreakPoint(UInt16 bp);
		public void RemovBreakPoint(UInt16 bp);
	}

	public class Chip8InterpreterDBG : Chip8VM
	{
		public Chip8State State { get; set; } = new Chip8State();
		byte[] ROM = null;

		public void LoadBinary(Span<byte> binary)
		{
			if (binary != null)
				ROM = binary.ToArray();
			else
				ROM = null;
			Reset();			
		}

		public void Reset()
		{
			State.ClearState();
			if (ROM != null)
			{
				State.LoadBinary(ROM);
				State.PC = Chip8State.ProgramStart;
			}
		}

		readonly Instructions.Decoder dec = new Instructions.Decoder();
		readonly Instructions.Disassembler dasm = new Instructions.Disassembler();
		readonly Interpreter.Interpreter intr = new Interpreter.Interpreter();
		Instructions.ParsedInstruction IR;

		public SortedSet<UInt16> BreakPoints = new SortedSet<ushort>();

		public bool IsBreakpoint(UInt16 bp) => BreakPoints.Contains(bp);
		public void AddBreakPoint(UInt16 bp) => BreakPoints.Add(bp);
		public void RemovBreakPoint(UInt16 bp) => BreakPoints.Remove(bp);

		private UInt16 LastBreak = 0;
		private bool Step() 
		{
			bool BreakIf = BreakPoints.Contains(State.PC);

			if (BreakIf && LastBreak != State.PC)
			{
				LastBreak = State.PC;
				return true;
			}

			IR = dec.Decode(State.ReadInstruction());
			if (BreakOnJump && IR.IsControlFlow()) 
			{
				BreakOnJump = false;
				return true;
			}
			intr.Execute(State, IR);
			State.StepPC();
			return false;
		}

		public void Run(uint instructions = uint.MaxValue)
		{			
			for (uint i = 0; i < instructions && !State.Terminated; i++)
			{
				if (Step()) break;	
			}
		}

		public bool IsDebuggingSupported => true;
		public bool BreakOnJump { get; set; }

		public void StepInto()
		{
			if (!State.Terminated)
				Step();
		}
	}

	public class Chip8Interpreter : Chip8VM
	{
		public Chip8State State { get; set; } = new Chip8State();
		byte[] ROM = null;

		public void LoadBinary(Span<byte> binary)
		{
			if (binary != null)
				ROM = binary.ToArray();
			else
				ROM = null;
			Reset();
		}

		public void Reset()
		{
			State.ClearState();
			if (ROM != null)
			{
				State.LoadBinary(ROM);
				State.PC = Chip8State.ProgramStart;
			}
		}

		readonly Instructions.Decoder dec = new Instructions.Decoder();
		readonly Instructions.Disassembler dasm = new Instructions.Disassembler();
		readonly Interpreter.Interpreter intr = new Interpreter.Interpreter();
		public void Run(uint instructions = uint.MaxValue)
		{
			for (uint i = 0; i < instructions && !State.Terminated; i++)
			{
				var inst = dec.Decode(State.ReadInstruction());
				intr.Execute(State, inst);
				State.StepPC();
			}
		}

		public bool IsDebuggingSupported => true;
		public bool BreakOnJump { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public void StepInto()
		{
			throw new NotImplementedException();
		}

		public bool IsBreakpoint(UInt16 bp) => false;
		public void AddBreakPoint(UInt16 bp) { }
		public void RemovBreakPoint(UInt16 bp) { }
	}

	public class Chip8JIT : Chip8VM
	{
		public Chip8State State { get; set; } = new Chip8State();

		JIT.JIT.JITROMDelegate compiledFunction;
		byte[] ROM = null;

		public void LoadBinary(Span<byte> binary)
		{
			if (binary != null && (ROM == null || !binary.SequenceEqual(ROM)))
			{
				compiledFunction = null;
				ROM = binary.ToArray();
			}
			else
				ROM = null;
			Reset();
		}

		public void Reset()
		{
			State.ClearState();
			if (ROM != null)
			{
				State.LoadBinary(ROM);
				State.PC = Chip8State.ProgramStart;
				if (compiledFunction == null)
					compiledFunction = new Chip8Sharp.JIT.JIT().JITROM(ROM, BP);
			}
		}

		private SortedSet<UInt16> BP = new SortedSet<ushort>();
		public void Run(uint instructions = uint.MaxValue)
		{
			if (instructions != uint.MaxValue)
				throw new Exception("Stepping is not supported with JIT compiler");

			compiledFunction(State, ref State.Registers);
		}
	
		public bool IsDebuggingSupported => false;
		public bool BreakOnJump { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
	
		public void StepInto()
		{
			throw new NotImplementedException();
		}
	
		public bool IsBreakpoint(UInt16 bp) => BP.Contains(bp);
		public void AddBreakPoint(UInt16 bp) => BP.Add(bp);
		public void RemovBreakPoint(UInt16 bp) => BP.Remove(bp);
	}
}
