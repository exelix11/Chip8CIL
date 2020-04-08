using Chip8Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Chip8SharpJITTests
{
	public static class Helper 
	{
		public static Chip8State JITAndExecuteROM(Span<byte> ROM, Chip8State state = null)
		{
			if (state == null)
				state = new Chip8State();

			ROM.CopyTo(state.ProgramRegion.Span);
			new Chip8Sharp.JIT.JIT().JITROM(ROM)(state);
			return state;
		}
	}

	[TestClass]
	public class JITTests
	{
		[TestMethod]
		public void RegisterAssignment()
		{
			byte[] ROM = new byte[] { 0x69, 69 };
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0x9, 69)
				.AssertRegsZeroExcept(9);
		}

		[TestMethod]
		public void IAssignment()
		{
			byte[] ROM = new byte[] { 0xA6, 0x66 };
			Helper.JITAndExecuteROM(ROM)
				.AssertI(0x666)
				.AssertRegsZeroExcept();
		}

		[TestMethod]
		public void ClearScreen()
		{
			Chip8State state = new Chip8State();
			state.VMEM.Span.Fill(0xFF);
			
			byte[] ROM = new byte[] { 0x00, 0xE0 };
			
			Helper.JITAndExecuteROM(ROM, state)
				.AssertRegsZeroExcept();

			Assert.IsTrue(state.VMEM.Span.SequenceEqual(new byte[state.VMEM.Length]));
		}

		[TestMethod]
		public void AddImmediate()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x70, 20 };
			//R0 = 10
			//ADDI R0 20
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 30)
				.AssertRegsZeroExcept(0);
		}

		[TestMethod]
		public void LoadRegister()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x81, 0x00 };
			//R0 = 10
			//LD R1 R0
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 10)
				.AssertReg(1, 10)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void OrRegister()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x61, 22, 0x80, 0x11 };
			//R0 = 10
			//R1 = 22
			//OR R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 30)
				.AssertReg(1, 22)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void AndRegister()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x61, 22, 0x80, 0x12 };
			//R0 = 10
			//R1 = 22
			//AND R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 2)
				.AssertReg(1, 22)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void XorRegister()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x61, 22, 0x80, 0x13 };
			//R0 = 10
			//R1 = 22
			//XOR R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 28)
				.AssertReg(1, 22)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void AddRegister()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x61, 20, 0x80, 0x14 };
			//R0 = 10
			//R1 = 20
			//ADD R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 30)
				.AssertReg(1, 20)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void AddRegisterOverflow()
		{
			byte[] ROM = new byte[] { 0x60, 250, 0x61, 20, 0x80, 0x14 };
			//R0 = 250
			//R1 = 20
			//ADD R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 14)
				.AssertReg(1, 20)
				.AssertReg(0xF, 1)
				.AssertRegsZeroExcept(0, 1, 0xF);
		}

		[TestMethod]
		public void SubRegisterNotBorrow()
		{
			byte[] ROM = new byte[] { 0x60, 30, 0x61, 18, 0x80, 0x15 };
			//R0 = 30
			//R1 = 18
			//SUB R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 12)
				.AssertReg(1, 18)
				.AssertReg(0xF, 1)
				.AssertRegsZeroExcept(0, 1, 0xF);
		}

		[TestMethod]
		public void SubRegisterBorrow()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x61, 20, 0x80, 0x15 };
			//R0 = 10
			//R1 = 20
			//SUB R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 0xF6)
				.AssertReg(1, 20)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void SubNRegisterBorrow()
		{
			byte[] ROM = new byte[] { 0x60, 20, 0x61, 10, 0x80, 0x17 };
			//R0 = 20
			//R1 = 10
			//SUB R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 0xF6)
				.AssertReg(1, 10)
				.AssertRegsZeroExcept(0, 1);
		}

		[TestMethod]
		public void SubNRegisterNotBorrow()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x61, 20, 0x80, 0x17 };
			//R0 = 10
			//R1 = 20
			//SUB R0 R1
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 10)
				.AssertReg(1, 20)
				.AssertReg(0xF, 1)
				.AssertRegsZeroExcept(0, 1, 0xF);
		}

		[TestMethod]
		public void ShiftRight()
		{
			byte[] ROM = new byte[] { 0x60, 10, 0x80, 0x06 };
			//R0 = 10
			//SHR R0
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 5)
				.AssertRegsZeroExcept(0);

			ROM = new byte[] { 0x60, 11, 0x80, 0x06 };
			//R0 = 11
			//SHL R0
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 5)
				.AssertReg(0xF, 1)
				.AssertRegsZeroExcept(0, 0xF);
		}

		[TestMethod]
		public void ShiftLeft()
		{
			byte[] ROM = new byte[] { 0x60, 1, 0x80, 0x0E };
			//R0 = 1
			//SHL R0
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 2)
				.AssertRegsZeroExcept(0);

			ROM = new byte[] { 0x60, 0x81, 0x80, 0x0E };
			//R0 = 0x81
			//SHL R0
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 2)
				.AssertReg(0xF, 1)
				.AssertRegsZeroExcept(0, 0xF);
		}

		[TestMethod]
		public void SkipEqualsImmediate()
		{
			byte[] ROM = new byte[] { 
				0x60, 20, // R0=20
				0x30, 20, // SEI R0 20
				0x69, 69, // R9=69
				0x30, 21, // SEI R0 21
				0x68, 66  // R8=66
			};
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 20)
				.AssertReg(0x8, 66)
				.AssertRegsZeroExcept(0, 8);
		}

		[TestMethod]
		public void SkipNotEqualsImmediate()
		{
			byte[] ROM = new byte[] { 0x60, 20, 0x40, 20, 0x69, 69, 0x40, 21, 0x68, 66 };
			//R0 = 20
			//SNEI R0 20
			//R9 = 69
			//SNEI R0 21
			//R8 = 66
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 20)
				.AssertReg(0x9, 69)
				.AssertNReg(0x8, 66)
				.AssertRegsZeroExcept(0, 9);
		}

		[TestMethod]
		public void SkipEqualsRegister()
		{
			byte[] ROM = new byte[] { 0x60, 20, 0x50, 0x10, 0x69, 20, 0x50, 0x90, 0x68, 66 };
			//R0 = 20
			//SER R0 R1
			//R9 = 20
			//SER R0 R9
			//R8 = 66
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 20)
				.AssertReg(0x9, 20)
				.AssertNReg(0x8, 66)
				.AssertRegsZeroExcept(0, 9);
		}

		[TestMethod]
		public void SkipNotEqualsRegister()
		{
			byte[] ROM = new byte[] { 0x60, 20, 0x61, 20, 0x90, 0x20, 0x69, 20, 0x91, 0x00, 0x68, 66 };
			//R0 = 20
			//R1 = 20
			//SNER R0 R2
			//R9 = 20
			//SNER R1 R0
			//R8 = 66
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 20)
				.AssertReg(1, 20)
				.AssertNReg(0x9, 20)
				.AssertReg(0x8, 66)
				.AssertRegsZeroExcept(0, 1, 8);
		}

		[TestMethod]
		public void FunctionCall()
		{
			byte[] ROM = new byte[] { 0x22, 0x06, 0x12, 0x02, 0x63, 22, 0x69, 69, 0x00, 0xEE };
			//200 CALL 0x206
			//202 JMP 206
			//204 R3 = 22
			//206 R9 = 69
			//208 RET
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0x9, 69)
				.AssertNReg(0x3, 22)
				.AssertRegsZeroExcept(3, 9);
		}

		[TestMethod]
		public void Jump()
		{
			byte[] ROM = new byte[] {
				0x12, 0x08,	// 200	JMP 208
				0x60, 10,	// 202	R0 = 10
				0x61, 20,	// 204	R1 = 20
				0x12, 0x06,	// 206	JMP 206 (ret)
				0x62, 30, 	// 208	R2 = 30
				0x12, 0x04,	// 20A	JMP 204
			};

			Helper.JITAndExecuteROM(ROM)
				.AssertReg(2, 30)
				.AssertNReg(0, 10)
				.AssertReg(1, 20)
				.AssertRegsZeroExcept(0, 1, 2);
		}

		[TestMethod]
		public void Jump0()
		{
			byte[] ROM = new byte[] {
				0x60, 0x02,		// 200
				0xB2, 0x08,		// 202  JMP0 0x208
				0x00, 0x00,		// 204
				0x00, 0x00,		// 206
				0x22, 0x08,		// 208  JMP 0x208 to detect misjumps
				0x63, 0x66,		// 20A  R3 = 0x66
				0x60, 0x1C,		// 20C  R0 = 0x1C
				0xB2, 0x08,		// 20E  JMP0 0x208 should jump to 224
				0x00, 0x00,		// 210
				0x00, 0x00,		// 212
				0x00, 0x00,		// 214
				0x00, 0x00,		// 216
				0x61, 0x01,		// 218 R1 = 1
				0x62, 0x02,		// 21A R2 = 2
				0x63, 0x03,		// 21C ....
				0x64, 0x04,		// 21E
				0x65, 0x05,		// 220
				0x66, 0x06,		// 222
				0x67, 0x07,		// 224
				0x68, 0x08,		// 226
				0x69, 0x09,		// 228 R9 = 9
			};

			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 0x1C)
				.AssertReg(3, 0x66)
				.AssertReg(7, 7)
				.AssertReg(8, 8)
				.AssertReg(9, 9)
				.AssertRegsZeroExcept(0, 3, 7, 8, 9);
		}

		[TestMethod]
		public void Random()
		{
			byte[] ROM = new byte[] { 0xC0, 0x7F };
			var state = Helper.JITAndExecuteROM(ROM);
			Assert.IsTrue((state.V0 & 0x80) == 0);
			state.AssertRegsZeroExcept(0);
		}

		[TestMethod]
		public void DrawSprite()
		{
			byte[] ROM = new byte[] {
				0x61, 8,
				0x62, 8,
				0xA2, 0x0A, //LDI 0x20A
				0xD1, 0x22,
				0x12, 0x08, //terminate
				0xCC, 0xAA, //Sprite data
			};
			var state = Helper.JITAndExecuteROM(ROM);

			//Chip8State.DisplayW / 8 * actualY
			Assert.AreEqual(0xCC, state.VMEM.Span[64 + 1]);
			Assert.AreEqual(0xAA, state.VMEM.Span[72 + 1]);

			state.AssertReg(1, 8)
				.AssertReg(2, 8)
				.AssertRegsZeroExcept(1,2);
		}

		[TestMethod]
		public void ReadDT()
		{
			Chip8State state = new Chip8State();
			state.DT = 10;

			byte[] ROM = new byte[] {
				0xF0, 0x07,
			};
			Helper.JITAndExecuteROM(ROM, state);

			state.AssertReg(0, 10).AssertRegsZeroExcept(0);
		}

		[TestMethod]
		public void SetDT()
		{
			byte[] ROM = new byte[] {
				0x60, 0x69,
				0xF0, 0x15,
			};
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(0, 0x69)
				.AssertDT(0x69)
				.AssertRegsZeroExcept(0);
		}

		[TestMethod]
		public void Input()
		{
			//Not actually implemented, always returns 0
			byte[] ROM = new byte[] {
				0x60, 0x69,
				0xF0, 0x0A,
			};
			Helper.JITAndExecuteROM(ROM).AssertRegsZeroExcept();
		}

		[TestMethod]
		public void SetST()
		{
			byte[] ROM = new byte[] {
				0x60, 0x69,
				0xF0, 0x18,
			};
			Helper.JITAndExecuteROM(ROM)
				.AssertRegsZeroExcept(0)
				.AssertST(0x69);
		}

		[TestMethod]
		public void AddRegToI()
		{
			Chip8State state = new Chip8State();
			state.I = 10;

			byte[] ROM = new byte[] {
				0x60, 20,
				0xF0, 0x1E
			};

			Helper.JITAndExecuteROM(ROM, state)
				.AssertReg(0, 20)
				.AssertI(30)
				.AssertRegsZeroExcept(0);
		}

		[TestMethod]
		public void LoadFontOffset()
		{
			byte[] ROM = new byte[] {
				0x65, 3,
				0xF5, 0x29
			};

			Helper.JITAndExecuteROM(ROM)
				.AssertReg(5, 3)
				.AssertI(Chip8State.FontStart + 3 * 5)
				.AssertRegsZeroExcept(5);
		}

		[TestMethod]
		public void StoreBCD()
		{
			Chip8State state = new Chip8State();
			state.I = 0x300;

			byte[] ROM = new byte[] {
				0x60, 123,
				0xF0, 0x33
			};

			Helper.JITAndExecuteROM(ROM, state)
				.AssertReg(0, 123)
				.AssertRegsZeroExcept(0);

			Assert.AreEqual(1, state.RAM.Span[state.I + 0]);
			Assert.AreEqual(2, state.RAM.Span[state.I + 1]);
			Assert.AreEqual(3, state.RAM.Span[state.I + 2]);
		}

		[TestMethod]
		public void LoadRegs()
		{
			Chip8State state = new Chip8State();
			state.I = 0x300;
			state.RAM.Span[state.I] = 1;
			state.RAM.Span[state.I + 1] = 2;
			state.RAM.Span[state.I + 2] = 3;

			byte[] ROM = new byte[] {
				0xF2, 0x65
			};

			Helper.JITAndExecuteROM(ROM, state)
				.AssertReg(0, 1)
				.AssertReg(1, 2)
				.AssertReg(2, 3)
				.AssertRegsZeroExcept(0,1,2);
		}

		[TestMethod]
		public void StoreRegs()
		{
			Chip8State state = new Chip8State();
			state.I = 0x300;

			byte[] ROM = new byte[] {
				0x60, 1,
				0x61, 2,
				0x62, 3,
				0x63, 4,
				0xF3, 0x55
			};

			Helper.JITAndExecuteROM(ROM, state)
				.AssertReg(0, 1)
				.AssertReg(1, 2)
				.AssertReg(2, 3)
				.AssertReg(3, 4)
				.AssertRegsZeroExcept(0,1,2,3);

			Assert.AreEqual(1, state.RAM.Span[state.I + 0]);
			Assert.AreEqual(2, state.RAM.Span[state.I + 1]);
			Assert.AreEqual(3, state.RAM.Span[state.I + 2]);
			Assert.AreEqual(4, state.RAM.Span[state.I + 3]);
		}

	}

	static class Chip8StateExten 
	{
		public static Chip8State AssertReg(this Chip8State state, byte register, byte value)
		{
			Assert.AreEqual(value, state.Register(register));
			return state;
		}

		public static Chip8State AssertNReg(this Chip8State state, byte register, byte value)
		{
			Assert.AreNotEqual(value, state.Register(register));
			return state;
		}

		public static Chip8State AssertI(this Chip8State state, ushort value)
		{
			Assert.AreEqual(value, state.I);
			return state;
		}

		public static Chip8State AssertDT(this Chip8State state, ushort value)
		{
			Assert.AreEqual(value, state.DT);
			return state;
		}

		public static Chip8State AssertST(this Chip8State state, ushort value)
		{
			Assert.AreEqual(value, state.ST);
			return state;
		}

		public static Chip8State AssertRegsZeroExcept(this Chip8State state, params int[] values)
		{
			var regs = state.Registers.Span;
			for (int i = 0; i < regs.Length; i++)
				if (!values.Contains(i))	
					Assert.AreEqual(0, regs[i]);
			return state;
		}
	}
}
