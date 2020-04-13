using Chip8Sharp.Instructions;
using Chip8Sharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Chip8Sharp.JIT
{
	static class Exten
	{
		/// <summary>
		/// Pushes the address of the N register on the stack
		/// </summary>
		internal static void EmitGetRegister(this ILGenerator gen, int N, JITContext ctx)
		{
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Ldflda, Registers.Fields[N]);
		}

		internal static void EmitLoadImmediate(this ILGenerator gen, Disassembler.DecompEntry inst) =>
			gen.Emit(OpCodes.Ldc_I4, (int)inst.Value.Immediate16);

		/// <summary>
		/// After the code emitted by this method the managed stack looks like:
		/// 
		/// <br>--bottom--</br>
		/// <br>target register address</br>
		/// <br>target value </br>
		/// <br>source value </br>
		/// 
		/// </summary>
		/// <param name="target">target register index</param>
		/// <param name="source">source register index</param>
		/// <param name="ctx">state ctx</param>
		internal static void EmitPushRegistersAndIndirectTarget(this ILGenerator gen, int target, int source, JITContext ctx)
		{
			gen.EmitGetRegister(target, ctx);
			gen.Emit(OpCodes.Dup);
			gen.Emit(OpCodes.Ldind_U1);
			gen.EmitGetRegister(source, ctx);
			gen.Emit(OpCodes.Ldind_U1);
		}
	}

	static class JITImplementation
	{
		[InstrImpl(Instruction.SYS)]
		public static void SYS(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Debug.WriteLine("WARN: Sys called");
		}

		[InstrImpl(Instruction.CLS)]
		public static void CLS(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			var method = typeof(Interpreter.Implementation).GetMethod(nameof(Implementation.CLS));
			//Args are: Chip8State state

			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Call, method);
		}

		[InstrImpl(Instruction.RET)]
		public static void RET(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ret);
		}

		[InstrImpl(Instruction.JMP)]
		public static void JMP(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			if (inst.Instr.Value.Immediate16 == inst.Offset)
				gen.Emit(OpCodes.Ret);
			else
				gen.Emit(OpCodes.Br, ctx.Labels[inst.Value.Immediate16]);
		}

		[InstrImpl(Instruction.CALL)]
		public static void CALL(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldarg_1);
			gen.Emit(OpCodes.Call, ctx.Functions[inst.Value.Immediate16].Method);
		}

		[InstrImpl(Instruction.SEI)]
		public static void SEI(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.EmitLoadImmediate(inst);
			gen.Emit(OpCodes.Beq, ctx.Label(inst.Offset + 4));
		}

		[InstrImpl(Instruction.SNEI)]
		public static void SNEI(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.EmitLoadImmediate(inst);
			gen.Emit(OpCodes.Bne_Un, ctx.Label(inst.Offset + 4));
		}

		[InstrImpl(Instruction.SER)]
		public static void SER(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.EmitGetRegister(inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Beq, ctx.Label(inst.Offset + 4));
		}

		[InstrImpl(Instruction.SNER)]
		public static void SNER(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.EmitGetRegister(inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Bne_Un, ctx.Label(inst.Offset + 4));
		}

		[InstrImpl(Instruction.LDI)]
		public static void LDI(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.EmitLoadImmediate(inst);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.ADDI)]
		public static void ADDI(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Dup);
			gen.Emit(OpCodes.Ldind_U1);
			gen.EmitLoadImmediate(inst);
			gen.Emit(OpCodes.Add);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.LD)]
		public static void LD(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx); //Push target address
			gen.EmitGetRegister(inst.Value.Reg1, ctx); //Push source address
			gen.Emit(OpCodes.Ldind_U1); //Pop source address, push value
			gen.Emit(OpCodes.Stind_I1); //assign value
		}

		[InstrImpl(Instruction.OR)]
		public static void OR(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitPushRegistersAndIndirectTarget(inst.Value.Reg0, inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Or);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.AND)]
		public static void AND(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitPushRegistersAndIndirectTarget(inst.Value.Reg0, inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.And);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.XOR)]
		public static void XOR(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitPushRegistersAndIndirectTarget(inst.Value.Reg0, inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Xor);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.ADD)]
		public static void ADD(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Label CaseNotOverflow = gen.DefineLabel();
			Label Continue = gen.DefineLabel();

			gen.EmitPushRegistersAndIndirectTarget(inst.Value.Reg0, inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Add);
			//now the stack contains the sum and the register address
			gen.Emit(OpCodes.Dup); //Duplicate the sum for comparison
			gen.Emit(OpCodes.Ldc_I4, 0xFF);
			gen.Emit(OpCodes.Ble, CaseNotOverflow); //If greater goto case overflow

			//else CaseOverflow:
			gen.EmitGetRegister(0xF, ctx); //Push VF register addres for storing the overflow bit
			gen.Emit(OpCodes.Ldc_I4, 1);
			gen.Emit(OpCodes.Br, Continue);

			//CaseNotOverflow:
			gen.MarkLabel(CaseNotOverflow);
			gen.EmitGetRegister(0xF, ctx); //Push VF register addres for storing the overflow bit
			gen.Emit(OpCodes.Ldc_I4, 0);

			//Assign value to VF and store sum
			gen.MarkLabel(Continue);
			gen.Emit(OpCodes.Stind_I1); //Store the VF value that's on the stack
			gen.Emit(OpCodes.Conv_U1); //Store the sum value we duplicated earlier
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.SUB)]
		public static void SUB(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Label CaseNotOverflow = gen.DefineLabel();
			Label Continue = gen.DefineLabel();

			gen.EmitPushRegistersAndIndirectTarget(inst.Value.Reg0, inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Sub);
			//now the stack contains the difference and the register address
			gen.Emit(OpCodes.Dup);
			gen.Emit(OpCodes.Ldc_I4_M1);
			gen.Emit(OpCodes.Bgt, CaseNotOverflow);

			//CaseOverflow
			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldc_I4, 0);
			gen.Emit(OpCodes.Br, Continue);

			gen.MarkLabel(CaseNotOverflow);
			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldc_I4, 1);

			//Assign value to VF and store sum
			gen.MarkLabel(Continue);
			gen.Emit(OpCodes.Stind_I1); //Store the VF value that's on the stack
			gen.Emit(OpCodes.Conv_U1);  //Store the difference
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.SHR)]
		public static void SHR(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Dup); //Duplicate address for loading
			gen.Emit(OpCodes.Ldind_U1); //Load value

			gen.Emit(OpCodes.Dup); //duplicate value for VF comparison
			gen.Emit(OpCodes.Stloc_0); //Store for VF comparison

			gen.Emit(OpCodes.Ldc_I4_1);
			gen.Emit(OpCodes.Shr_Un); //Shift and store
			gen.Emit(OpCodes.Stind_I1);

			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldloc_0);
			gen.Emit(OpCodes.Ldc_I4_1);
			gen.Emit(OpCodes.And);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.SHL)]
		public static void SHL(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Label Continue = gen.DefineLabel();
			Label HighBitNotSet = gen.DefineLabel();

			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Dup); //Duplicate address for loading
			gen.Emit(OpCodes.Ldind_U1); //Load value

			gen.Emit(OpCodes.Dup); //duplicate value for VF comparison
			gen.Emit(OpCodes.Stloc_0); //Store for VF comparison

			gen.Emit(OpCodes.Ldc_I4_1);
			gen.Emit(OpCodes.Shl); //Shift and store
			gen.Emit(OpCodes.Stind_I1);

			gen.Emit(OpCodes.Ldloc_0);
			gen.Emit(OpCodes.Ldc_I4, 0x80);
			gen.Emit(OpCodes.And);
			gen.Emit(OpCodes.Ldc_I4_0);
			gen.Emit(OpCodes.Beq, HighBitNotSet);

			//HighBitSet:
			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldc_I4_1);
			gen.Emit(OpCodes.Br, Continue);

			//Not set
			gen.MarkLabel(HighBitNotSet);
			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldc_I4_0);

			gen.MarkLabel(Continue);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.SUBN)]
		public static void SUBN(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Label CaseNotOverflow = gen.DefineLabel();
			Label Continue = gen.DefineLabel();

			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.EmitGetRegister(inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Sub);
			//now the stack contains the difference and the register address
			gen.Emit(OpCodes.Dup);
			gen.Emit(OpCodes.Ldc_I4_M1);
			gen.Emit(OpCodes.Bgt, CaseNotOverflow);

			//CaseOverflow
			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldc_I4, 0);
			gen.Emit(OpCodes.Br, Continue);

			gen.MarkLabel(CaseNotOverflow);
			gen.EmitGetRegister(0xF, ctx);
			gen.Emit(OpCodes.Ldc_I4, 1);

			//Assign value to VF and store sum
			gen.MarkLabel(Continue);
			gen.Emit(OpCodes.Stind_I1); //Store the VF value that's on the stack
			gen.Emit(OpCodes.Conv_U1);  //Store the difference
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.LII)]
		public static void LRI(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldc_I4, inst.Value.Immediate16);
			//gen.Emit(OpCodes.Conv_I2);
			gen.Emit(OpCodes.Call, ctx.RegisterI.SetMethod);
		}

		[InstrImpl(Instruction.JMP0)]
		public static void JMP0(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			List<Label> JumpTable = new List<Label>();

			for (int i = inst.Value.Immediate16; i < inst.Value.Immediate16 + 256; i++)
				JumpTable.Add(ctx.Label(i));

			gen.EmitGetRegister(0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Switch, JumpTable.ToArray());
		}

		//Call interpreter methods when it's easier:
		[InstrImpl(Instruction.RND)]
		public static void RND(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			var method = typeof(Interpreter.Implementation).GetMethod(nameof(Implementation.RND));
			//Args are: Chip8State state, ref byte regx, byte imm

			gen.Emit(OpCodes.Ldarg_0);
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldc_I4_S, inst.Value.Immediate8);
			gen.Emit(OpCodes.Call, method);
		}

		[InstrImpl(Instruction.DRW)]
		public static void DRW(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			var method = typeof(Interpreter.Implementation).GetMethod(nameof(Implementation.DRW));
			//Args are: Chip8State state, ref byte regx, ref byte regy, byte imm4

			gen.Emit(OpCodes.Ldarg_0);
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.EmitGetRegister(inst.Value.Reg1, ctx);
			gen.Emit(OpCodes.Ldc_I4_S, inst.Value.Immediate4);
			gen.Emit(OpCodes.Call, method);
		}

		[InstrImpl(Instruction.SKP)]
		public static void SKP(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Debug.WriteLine("WARN: Unimplemented input instruction");

			//Skip next instruction if key with the value of Vx is pressed.
		}

		[InstrImpl(Instruction.SKNP)]
		public static void SKNP(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Debug.WriteLine("WARN: Unimplemented input instruction");

			//Skip next instruction if key with the value of Vx is pressed.
		}

		[InstrImpl(Instruction.SDT)]
		public static void SDT(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Call, ctx.RegisterDT.GetMethod);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.IN)]
		public static void IN(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			Debug.WriteLine("WARN: Unimplemented input instruction");
			//Wait for a key press, store the value of the key in Vx.
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldc_I4_0);
			gen.Emit(OpCodes.Stind_I1);
		}

		[InstrImpl(Instruction.LDT)]
		public static void LDT(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0);
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Call, ctx.RegisterDT.SetMethod);
		}

		[InstrImpl(Instruction.LST)]
		public static void LST(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0);
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Call, ctx.RegisterST.SetMethod);
		}

		[InstrImpl(Instruction.ADDII)]
		public static void ADDRI(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0); //reference for the last store call
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Call, ctx.RegisterI.GetMethod);
			gen.Emit(OpCodes.Add);
			gen.Emit(OpCodes.Call, ctx.RegisterI.SetMethod);
		}

		[InstrImpl(Instruction.LIFNT)]
		public static void LIFNT(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			gen.Emit(OpCodes.Ldarg_0); //reference for the last store call
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Ldind_I1);
			gen.Emit(OpCodes.Conv_U1);
			gen.Emit(OpCodes.Ldc_I4_5);
			gen.Emit(OpCodes.Mul);
			gen.Emit(OpCodes.Ldc_I4, Chip8State.FontStart);
			gen.Emit(OpCodes.Add);
			gen.Emit(OpCodes.Call, ctx.RegisterI.SetMethod);
		}

		[InstrImpl(Instruction.STBCD)]
		public static void STBCD(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			var method = typeof(Interpreter.Implementation).GetMethod(nameof(Implementation.STBCD));
			//Args are: Chip8State state, ref byte regx

			gen.Emit(OpCodes.Ldarg_0);
			gen.EmitGetRegister(inst.Value.Reg0, ctx);
			gen.Emit(OpCodes.Call, method);
		}

		[InstrImpl(Instruction.STREG)]
		public static void STREG(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			var method = typeof(Interpreter.Implementation).GetMethod(nameof(Implementation.STREG));
			//Args are: Chip8State state, byte regx

			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldc_I4_S, inst.Value.Reg0);
			gen.Emit(OpCodes.Call, method);
		}

		[InstrImpl(Instruction.LDREG)]
		public static void LDREG(JITContext ctx, Disassembler.DecompEntry inst, ILGenerator gen)
		{
			var method = typeof(Interpreter.Implementation).GetMethod(nameof(Implementation.LDREG));
			//Args are: Chip8State state, byte regx

			gen.Emit(OpCodes.Ldarg_0);
			gen.Emit(OpCodes.Ldc_I4_S, inst.Value.Reg0);
			gen.Emit(OpCodes.Call, method);
		}
	}
}
