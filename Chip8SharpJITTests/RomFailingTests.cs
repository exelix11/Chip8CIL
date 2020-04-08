using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Chip8SharpJITTests
{
	[TestClass]
	public class RomFailingTests
	{
		[TestMethod]
		public void SEI_Fail()
		{
			byte[] ROM = new byte[] {
				0x65, 0xEE,	//0206 | LDI V5 0xEE
				0x35, 0xEE,	//0208 | SEI V5 0xEE
				0x12, 0x04, //020A | JMP off_0310
				0x64, 0x02	//020E | LDI V4 0x02
			};
			Helper.JITAndExecuteROM(ROM)
				.AssertReg(5, 0xEE)
				.AssertReg(0x4, 2);
			//Solved by adding gen.Emit(OpCodes.Conv_U1);
		}

		[TestMethod]
		public void SNER_Fail()
		{
			byte[] ROM = new byte[] {
				0x65, 0x2A, //0200 | LDI V5 0x2A
				0x87, 0x50, //0202 | LD V7 V5
				0x47, 0x2A, //0204 | SNEI V7 0x2A
				0xA9, 0x99	//0206 | LRI off_0999
			};
			Helper.JITAndExecuteROM(ROM).AssertI(0x999);
			//register address order int the .net stack was wrong for the LD instruction
		}
	}
}
