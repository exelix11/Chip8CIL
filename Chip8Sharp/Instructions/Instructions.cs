using System;
using System.Collections.Generic;
using System.Text;

namespace Chip8Sharp.Instructions
{
	/*
		Not all names are canonical, "overloaded" instructions are different entries here
		INSTI : uses immediate value
		INST : uses registers 
	*/
	public enum Instruction
	{
		[TypeA(0x00E0)] CLS,
		[TypeA(0x00EE)] RET,
		[TypeB(0)] SYS,
		[TypeB(1)] JMP,
		[TypeB(2)] CALL,
		[TypeC(3)] SEI,
		[TypeC(4)] SNEI,
		[TypeD(5,0)] SER,
		[TypeC(6)] LDI,
		[TypeC(7)] ADDI,

		[TypeD(8, 0)] LD,
		[TypeD(8, 1)] OR,
		[TypeD(8, 2)] AND,
		[TypeD(8, 3)] XOR,
		[TypeD(8, 4)] ADD,
		[TypeD(8, 5)] SUB,
		[TypeD(8, 6)] SHR,
		[TypeD(8, 7)] SUBN,
		[TypeD(8, 0xE)] SHL,
		[TypeD(9, 0)] SNER,

		[TypeB(0xA)] LII, //Load register I immediate
		[TypeB(0xB)] JMP0,

		[TypeC(0xC)] RND,
		[TypeE(0xD)] DRW,
		[TypeF(0xE, 0x9E)] SKP,
		[TypeF(0xE, 0xA1)] SKNP,

		[TypeF(0xF, 0x07)] SDT, //Store DT to Register
		[TypeF(0xF, 0x0A)] IN,

		[TypeF(0xF, 0x15)] LDT, //Load DT from immediate
		[TypeF(0xF, 0x18)] LST,
		[TypeF(0xF, 0x1E)] ADDII, //Add register I immediate
		[TypeF(0xF, 0x29)] LIFNT, //Load font addr to I
		[TypeF(0xF, 0x33)] STBCD,
		[TypeF(0xF, 0x55, true)] STREG,
		[TypeF(0xF, 0x65, true)] LDREG,
	}

	/*
		Instruction types:
		____ A
		_nnn B
		_xkk C
		_xy_ D 
		_xyn E 
		_x__ F
		
		x, y : register num
		KK : immediate byte value
		nnn : immediate 12-bit value
		n : immediate 4-bit value
	 */

	public readonly struct ParsedInstruction 
	{
		public readonly Instruction Instruction;

		public readonly UInt16 Immediate16;
		public byte Immediate8 => (byte)(Immediate16 & 0xFF);
		public byte Immediate4 => (byte)(Immediate16 & 0xF);

		public readonly byte Reg0, Reg1;

		public ParsedInstruction(Instruction inst, UInt16 imm, byte r0, byte r1)
		{
			Instruction = inst;
			Immediate16 = imm;
			Reg0 = r0;
			Reg1 = r1;
		}
	}

	public static class InstrExten 
	{
		public static bool GeneratesLabel(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.JMP:
				case Instruction.CALL:
				case Instruction.JMP0:
				case Instruction.LII:
					return true;
				default:
					return false;
			}
		}

		public static UInt16 GetLabelTarget(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.JMP:
				case Instruction.CALL:
				case Instruction.JMP0:
				case Instruction.LII:
					return instr.Immediate16;
				default:
					throw new Exception("Not a jump instruction");
			}
		}

		public static bool IsControlFlow(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.JMP:
				case Instruction.CALL:
				case Instruction.JMP0:
				case Instruction.RET:
				case Instruction.SER:
				case Instruction.SNER:
				case Instruction.SNEI:
				case Instruction.SEI:
				case Instruction.SKP:
				case Instruction.SKNP:
					return true;
				default:
					return false;
			}
		}

		public static bool IsSkipNext(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.SER:
				case Instruction.SNER:
				case Instruction.SNEI:
				case Instruction.SEI:
				case Instruction.SKP:
				case Instruction.SKNP:
					return true;
				default:
					return false;
			}
		}

		public static bool IsJump(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.JMP:
				case Instruction.CALL:
				case Instruction.JMP0:
					return true;
				default:
					return false;
			}
		}

		public static bool IsJumpStatic(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.JMP:
				case Instruction.CALL:
					return true;
				default:
					return false;
			}
		}

		public static bool IsJumpVariable(this ParsedInstruction instr)
		{
			switch (instr.Instruction)
			{
				case Instruction.JMP0:
					return true;
				default:
					return false;
			}
		}
	}

	abstract class InstructionType : Attribute 
	{
		public readonly byte Class;
		
		protected InstructionType(byte Class) => this.Class = Class;
		public virtual bool Matches(UInt16 val) => (val >> 12) == Class;

		public abstract ParsedInstruction Parse(UInt16 val, Instruction inst);
	}

	/// <summary>
	/// Attribute for instruction of type ____
	/// </summary>
	class TypeA : InstructionType
	{
		public readonly UInt16 Instr;

		public TypeA(UInt16 instr) : base((byte)(instr >> 8))
		{
			Instr = instr;
		}

		public override bool Matches(ushort val) => val == Instr;

		public override ParsedInstruction Parse(ushort val, Instruction inst) =>
			new ParsedInstruction(inst, 0, 0, 0);
	}

	/// <summary>
	/// Attribute for instruction of type _nnn
	/// </summary>
	class TypeB : InstructionType
	{
		public TypeB(byte Class) : base(Class) { }

		public override ParsedInstruction Parse(ushort val, Instruction inst) =>
			new ParsedInstruction(inst, (UInt16)(val & 0x0FFF), 0, 0);
	}

	/// <summary>
	/// Attribute for instruction of type _xkk
	/// </summary>
	class TypeC : InstructionType
	{
		public TypeC(byte Class) : base(Class) { }

		public override ParsedInstruction Parse(ushort val, Instruction inst) =>
			new ParsedInstruction(inst, (UInt16)(val & 0x00FF), (byte)((val >> 8) & 0xF), 0);
	}

	/// <summary>
	/// Attribute for instruction of type _xy_
	/// </summary>
	class TypeD : InstructionType
	{
		public readonly byte Sub;

		public TypeD(byte Class, byte Sub) : base(Class) 
		{
			this.Sub = Sub;
		}

		public override bool Matches(ushort val)
		{
			if (!base.Matches(val)) return false;
			return (val & 0x000F) == Sub;
		}

		public override ParsedInstruction Parse(ushort val, Instruction inst) =>
			new ParsedInstruction(inst, 0, (byte)((val >> 8) & 0xF), (byte)((val >> 4) & 0xF));
	}

	/// <summary>
	/// Attribute for instruction of type _xyn
	/// </summary>
	class TypeE : InstructionType
	{
		public TypeE(byte Class) : base(Class) { }

		public override ParsedInstruction Parse(ushort val, Instruction inst) =>
			new ParsedInstruction(inst, (byte)(val & 0xF), (byte)((val >> 8) & 0xF), (byte)((val >> 4) & 0xF));
	}

	/// <summary>
	/// Attribute for instruction of type _x__
	/// </summary>
	class TypeF : InstructionType
	{
		public readonly byte Sub;
		public readonly bool PassImmediate;

		public TypeF(byte Class, byte Sub, bool passIm = false) : base(Class)
		{
			this.Sub = Sub;
			PassImmediate = passIm;
		}

		public override bool Matches(ushort val)
		{
			if (!base.Matches(val)) return false;
			return (val & 0x00FF) == Sub;
		}

		public override ParsedInstruction Parse(ushort val, Instruction inst) =>
			new ParsedInstruction(inst, 0, (byte)((val >> 8) & 0xF), 0);
	}
}
