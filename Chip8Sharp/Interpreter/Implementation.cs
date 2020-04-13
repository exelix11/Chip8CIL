using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Linq;
using Chip8Sharp.Instructions;

using InstructionCall = System.Action<Chip8Sharp.Chip8State, Chip8Sharp.Instructions.ParsedInstruction>;

namespace Chip8Sharp.Interpreter
{
	static class Exten 
	{
		static public T ToDelegate<T>(this MethodInfo info) where T : Delegate =>
			(T)Delegate.CreateDelegate(typeof(T), info);
	}
	
	public class Interpreter
	{
		private Dictionary<Instruction, InstructionCall> CallTable;

		public void Execute(Chip8State state, ParsedInstruction instruction) =>
			CallTable[instruction.Instruction](state, instruction);

		public Interpreter() 
		{
			CallTable = new Dictionary<Instruction, InstructionCall>();
			
			var Impls = typeof(Implementation)
				.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.ToDictionary(x => x.GetCustomAttribute<InstrImpl>().Instr);

			var Types = typeof(Instruction)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.ToDictionary(x => (Instruction)x.GetValue(null), y => y.GetCustomAttribute<InstructionType>());

			Debug.Assert(Types.Count == Impls.Count);

			foreach (var k in Impls.Keys)
				CallTable.Add(k, CreateGlueMethod(Types[k], Impls[k]));
		}

		private InstructionCall CreateGlueMethod(InstructionType type, MethodInfo method) 
		{
			if (type is TypeA)
			{
				var d = method.ToDelegate<Implementation.TypeADelegate>();
				return (s, i) => d(s);
			}
			else if (type is TypeB)
			{
				var d = method.ToDelegate<Implementation.TypeBDelegate>();
				return (s, i) => d(s, i.Immediate16);
			}
			else if (type is TypeC)
			{
				var d = method.ToDelegate<Implementation.TypeCDelegate>();
				return (s, i) => d(s, ref s.Register(i.Reg0), i.Immediate8);
			}
			else if (type is TypeD)
			{
				var d = method.ToDelegate<Implementation.TypeDDelegate>();
				return (s, i) => d(s, ref s.Register(i.Reg0), ref s.Register(i.Reg1));
			}
			else if (type is TypeE)
			{
				var d = method.ToDelegate<Implementation.TypeEDelegate>();
				return (s, i) => d(s, ref s.Register(i.Reg0), ref s.Register(i.Reg1), i.Immediate4);
			}
			else if (type is TypeF)
			{
				if (((TypeF)type).PassImmediate)
				{
					var d = method.ToDelegate<Implementation.TypeFImmediateDelegate>();
					return (s, i) => d(s, i.Reg0);
				}
				else
				{
					var d = method.ToDelegate<Implementation.TypeFDelegate>();
					return (s, i) => d(s, ref s.Register(i.Reg0));
				}
			}
			throw new Exception("Unknown instruction type");
		}
	}

	class InstrImpl : Attribute
	{
		public readonly Instruction Instr;
		public InstrImpl(Instruction i) => Instr = i;
	}

	static class Implementation
	{
		public delegate void TypeADelegate(Chip8State state);
		public delegate void TypeBDelegate(Chip8State state, UInt16 imm);
		public delegate void TypeCDelegate(Chip8State state, ref byte reg, byte imm);
		public delegate void TypeDDelegate(Chip8State state, ref byte regx, ref byte regy);
		public delegate void TypeEDelegate(Chip8State state, ref byte regx, ref byte regy, byte imm4);
		public delegate void TypeFDelegate(Chip8State state, ref byte regx);
		public delegate void TypeFImmediateDelegate(Chip8State state, byte regx);

		[InstrImpl(Instruction.SYS)]
		public static void SYS(Chip8State state, UInt16 addr)
		{
			Debug.WriteLine("WARN: Sys called");
		}

		[InstrImpl(Instruction.CLS)]
		public static void CLS(Chip8State state)
		{
			state.VMEM.Span.Fill(0);
		}

		[InstrImpl(Instruction.RET)]
		public static void RET(Chip8State state)
		{
			state.Jump(state.StackRegion[state.SP]);
			state.SP--;
		}

		[InstrImpl(Instruction.JMP)]
		public static void JMP(Chip8State state, UInt16 imm)
		{
			if (state.PC == imm)
				state.Terminated = true;
			
			state.Jump(imm);
		}

		[InstrImpl(Instruction.CALL)]
		public static void CALL(Chip8State state, UInt16 imm)
		{
			state.SP++;
			state.StackRegion[state.SP] = (UInt16)(state.PC + 2);
			state.Jump(imm);
		}

		[InstrImpl(Instruction.SEI)]
		public static void SEI(Chip8State state, ref byte reg, byte imm)
		{
			if (reg == imm)
				state.PC += 2;
		}

		[InstrImpl(Instruction.SNEI)]
		public static void SNEI(Chip8State state, ref byte reg, byte imm)
		{
			if (reg != imm)
				state.PC += 2;
		}

		[InstrImpl(Instruction.SER)]
		public static void SER(Chip8State state, ref byte regx, ref byte regy)
		{
			if (regx == regy)
				state.PC += 2;
		}

		[InstrImpl(Instruction.SNER)]
		public static void SNER(Chip8State state, ref byte regx, ref byte regy)
		{
			if (regx != regy)
				state.PC += 2;
		}

		[InstrImpl(Instruction.LDI)]
		public static void LDI(Chip8State state, ref byte reg, byte imm)
		{
			reg = imm;
		}

		[InstrImpl(Instruction.ADDI)]
		public static void ADDI(Chip8State state, ref byte reg, byte imm)
		{
			unchecked { reg += imm; }
		}

		[InstrImpl(Instruction.LD)]
		public static void LD(Chip8State state, ref byte regx, ref byte regy)
		{
			regx = regy;
		}

		[InstrImpl(Instruction.OR)]
		public static void OR(Chip8State state, ref byte regx, ref byte regy)
		{
			regx |= regy;
		}

		[InstrImpl(Instruction.AND)]
		public static void AND(Chip8State state, ref byte regx, ref byte regy)
		{
			regx &= regy;
		}

		[InstrImpl(Instruction.XOR)]
		public static void XOR(Chip8State state, ref byte regx, ref byte regy)
		{
			regx ^= regy;
		}

		[InstrImpl(Instruction.ADD)]
		public static void ADD(Chip8State state, ref byte regx, ref byte regy)
		{
			state.Registers.VF = (byte)(regx + regy > 0xFF ? 1 : 0);

			unchecked { regx += regy; }
		}

		[InstrImpl(Instruction.SUB)]
		public static void SUB(Chip8State state, ref byte regx, ref byte regy)
		{
			state.Registers.VF = (byte)(regx > regy ? 1 : 0);

			unchecked { regx -= regy; }
		}

		[InstrImpl(Instruction.SHR)]
		public static void SHR(Chip8State state, ref byte regx, ref byte regy)
		{
			state.Registers.VF = (byte)(regx & 1);
			regx >>= 1;
		}

		[InstrImpl(Instruction.SHL)]
		public static void SHL(Chip8State state, ref byte regx, ref byte regy)
		{
			state.Registers.VF = (byte)((regx & 0x80) != 0 ? 1 : 0);
			regx <<= 1;
		}

		[InstrImpl(Instruction.SUBN)]
		public static void SUBN(Chip8State state, ref byte regx, ref byte regy)
		{
			state.Registers.VF = (byte)(regx < regy ? 1 : 0);

			unchecked { regx = (byte)(regy - regx); }
		}

		[InstrImpl(Instruction.LII)]
		public static void LRI(Chip8State state, UInt16 imm)
		{
			state.I = imm;
		}

		[InstrImpl(Instruction.JMP0)]
		public static void JMP0(Chip8State state, UInt16 imm)
		{
			state.Jump((UInt16)(state.Registers.V0 + imm));
		}

		private static Random rnd = new Random();
		[InstrImpl(Instruction.RND)]
		public static void RND(Chip8State state, ref byte regx, byte imm)
		{
			regx = (byte)(rnd.Next(0, 256) & imm);
		}

		private const int _dwblock = Chip8State.DisplayW / 8;
		[InstrImpl(Instruction.DRW)]
		public static void DRW(Chip8State state, ref byte regx, ref byte regy, byte imm4)
		{
			Span<byte> sprite = state.RAM.Span.Slice(state.I, imm4);
			Span<byte> vmem = state.VMEM.Span;

			//Precaulculate X pixels masks
			Span<int> Offsets = stackalloc int[8];
			Span<byte> Masks = stackalloc byte[8];

			int x = regx;
			if (x + 8 >= Chip8State.DisplayW)
				x = 0;

			Offsets[0] = x / 8;
			Masks[0] = (byte)(0x80 >> (x % 8));
			for (int i = 1; i < 8; i++)
			{
				byte mask = (byte)(Masks[i - 1] >> 1);
				Masks[i] = mask;
				Offsets[i] = Offsets[i - 1];
				if (mask == 0)
				{
					Masks[i] = 0x80;
					Offsets[i]++;
				}
			}

			state.Registers.VF = 0;
			int sprIndex = 0;
			for (int y = regy; y < regy + imm4; y++)
			{
				int actualY = (y >= Chip8State.DisplayH) ? (y - Chip8State.DisplayH) : y;
				byte sprSrc = sprite[sprIndex++];
				int YOffset = _dwblock * actualY;

				for (int xs = 0; xs < 8; xs++)
				{
					if ((sprSrc & (128 >> xs)) != 0)
					{
						ref byte vmo = ref vmem[YOffset + Offsets[xs]];
						ref byte mask = ref Masks[xs];
						if ((vmo & mask) != 0)
						{
							state.Registers.VF = 1;
							vmo &= (byte)(~mask);
						}
						else vmo |= mask;
					}
				}
			}
			state.VMEMUpdated = true;
		}

		[InstrImpl(Instruction.SKP)]
		public static void SKP(Chip8State state, ref byte regx)
		{
			Debug.WriteLine("WARN: Unimplemented input instruction");

			//Skip next instruction if key with the value of Vx is pressed.
		}

		[InstrImpl(Instruction.SKNP)]
		public static void SKNP(Chip8State state, ref byte regx)
		{
			Debug.WriteLine("WARN: Unimplemented input instruction");

			//Skip next instruction if key with the value of Vx is pressed.
		}

		[InstrImpl(Instruction.SDT)]
		public static void SDT(Chip8State state, ref byte regx)
		{
			regx = state.DT;
		}

		[InstrImpl(Instruction.IN)]
		public static void IN(Chip8State state, ref byte regx)
		{
			Debug.WriteLine("WARN: Unimplemented input instruction");
			//Wait for a key press, store the value of the key in Vx.
			regx = 0;
		}

		[InstrImpl(Instruction.LDT)]
		public static void LDT(Chip8State state, ref byte regx)
		{
			state.DT = regx;
		}

		[InstrImpl(Instruction.LST)]
		public static void LST(Chip8State state, ref byte regx)
		{
			state.ST = regx;
		}

		[InstrImpl(Instruction.ADDII)]
		public static void ADDRI(Chip8State state, ref byte regx)
		{
			unchecked { state.I += regx; }
		}

		[InstrImpl(Instruction.LIFNT)]
		public static void LIFNT(Chip8State state, ref byte regx)
		{
			state.I = (UInt16)(Chip8State.FontStart + regx * 5);
		}

		[InstrImpl(Instruction.STBCD)]
		public static void STBCD(Chip8State state, ref byte regx)
		{
			var hundreds = (regx / 100) % 10;
			var dec = (regx / 10) % 10;
			var unit = regx % 10;

			state.RAM.Span[state.I] = (byte)hundreds;
			state.RAM.Span[state.I + 1] = (byte)dec;
			state.RAM.Span[state.I + 2] = (byte)unit;
		}

		[InstrImpl(Instruction.STREG)]
		public static void STREG(Chip8State state, byte regx)
		{
			state.Registers.AsSpan().Slice(0, regx + 1).CopyTo(state.RAM.Span.Slice(state.I));
		}

		[InstrImpl(Instruction.LDREG)]
		public static void LDREG(Chip8State state, byte regx)
		{
			state.RAM.Span.Slice(state.I, regx + 1).CopyTo(state.Registers.AsSpan());
		}
	}
}
