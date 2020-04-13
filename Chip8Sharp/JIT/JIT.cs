using Chip8Sharp.Instructions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Diagnostics;
using static Chip8Sharp.JIT.JITContext;

namespace Chip8Sharp.JIT
{
	class JITContext 
	{
		public readonly PropertyInfo RegisterI;
		public readonly PropertyInfo RegisterDT;
		public readonly PropertyInfo RegisterST;

		public JITContext()
		{
			var t = typeof(Chip8State);

			RegisterI = t.GetProperty(nameof(Chip8State.I));
			RegisterDT = t.GetProperty(nameof(Chip8State.DT));
			RegisterST = t.GetProperty(nameof(Chip8State.ST));
		}

		public class TranslatedFunction 
		{
			public MethodInfo Info;
			public ILGenerator Generator;
			public DynamicMethod Method;
		}

		public Disassembler.Disassembly Disasm;
		
		public Dictionary<UInt16, Label> Labels;
		public Label Label(int l) => Labels[(UInt16)l];

		public Dictionary<UInt16, TranslatedFunction> Functions;
		public SortedSet<UInt16> BreakPoints;
	}

	public class JIT
	{
		public delegate void JITROMDelegate(Chip8State state, ref Registers registers);
		internal delegate void EmitterFunction(JITContext info, Disassembler.DecompEntry inst, ILGenerator gen);

		Disassembler disasm = new Disassembler();

		private Dictionary<Instruction, EmitterFunction> LinkEmitters;

		public JIT()
		{			
			LinkEmitters = typeof(JITImplementation).GetMethods(BindingFlags.Static | BindingFlags.Public).ToDictionary(
				x => x.GetCustomAttribute<Interpreter.InstrImpl>().Instr,
				x => (EmitterFunction)x.CreateDelegate(typeof(EmitterFunction))
			);
		}

		private (DynamicMethod, ILGenerator) CreateMethod(string name, JITContext ctx)
		{
			DynamicMethod res = (DynamicMethod)(object)new DynamicMethod(name, typeof(void), new[]
			{
				typeof(Chip8State),
				typeof(Registers).MakeByRefType()
			});

			ILGenerator gen = res.GetILGenerator();
			gen.DeclareLocal(typeof(int));
			return (res, gen);
		}

		public JITROMDelegate JITROM(Span<byte> ROM, SortedSet<UInt16> BreakPoints = null)
		{
			if (BreakPoints == null)
				BreakPoints = new SortedSet<ushort>();

			var exe = disasm.DisassembleProgram(ROM);

			JITContext ctx = new JITContext
			{
				BreakPoints = BreakPoints,
				Disasm = exe
			};

			var (res, gen) = CreateMethod("JITROM", ctx);

			//Find all function calls
			Dictionary<UInt16, TranslatedFunction> Functions = exe.Entries.Where(x => !x.IsData && x.Instr.Value.Instruction == Instruction.CALL)
				.Select(x => x.Instr.Value.Immediate16)
				.Distinct()
				.ToDictionary(x => x, x => (TranslatedFunction)null);
			
			//Function calls will get translated to their own method to use the runtime call stack
			foreach (var f in Functions.Keys.ToArray())
			{
				var (tmpMethod, TmpGen) = CreateMethod("method_" + f.ToString("X4"), ctx);

				Functions[f] = new TranslatedFunction 
				{
					 Method = tmpMethod,
					 Generator = TmpGen,
					 Info = tmpMethod
				};
			}

			ctx.Functions = Functions;

			//As detecting where a funnction ends is not trivial we're just going to JIT all functions to the end of the rom, disgusting i know
			foreach (var f in Functions.Keys.Where(x => x >= Chip8State.ProgramStart))
				JITBlock(Functions[f].Generator, ctx, (f - Chip8State.ProgramStart) / 2, exe.Entries.Length);

			//Then JIT the whole ROM
			JITBlock(gen, ctx, 0, exe.Entries.Length);
			
			return (JITROMDelegate)res.CreateDelegate(typeof(JITROMDelegate));
		}

		void JITBlock(ILGenerator gen, JITContext ctx, int start, int end)
		{
			//Every function has its own set of labels
			ctx.Labels = new Dictionary<ushort, Label>();

			void AddLabel(int v)
			{
				//Is this offset outside of the ROM ? Sometimes data can be misinterpreted as code
				if (v < Chip8State.ProgramStart)
					return;
				//Are we trying to jump from a function to code outside of it ?
				if ((v - Chip8State.ProgramStart) / 2 < start)
					Debugger.Break();

				if (!ctx.Labels.ContainsKey((UInt16)v))
					ctx.Labels.Add((UInt16)v, gen.DefineLabel());
			}

			//Calculate all the labels we need beforehand
			for (int i = start; i < end; i++) 
			{
				var entry = ctx.Disasm.Entries[i];
				if (entry.IsData) continue;

				if (!entry.Value.IsControlFlow() || entry.Instruction == Instruction.RET)
					continue;

				if (entry.Value.IsSkipNext())
					AddLabel(entry.Offset + 4);
				else if (entry.Value.IsJumpStatic())
					AddLabel(entry.Value.Immediate16);
				else if (entry.Instr.Value.IsJumpVariable())
					/*
						The JMP0 instruction jumps to immediate value + V0
						The optimal way to implement this would be hooking jumps and JIT based on the address...
						...Or we can bruteforce all 256 cases by using a jumptable with the MSIL "switch" instruction
					 */
					for (int j = entry.Value.Immediate16; j < entry.Instr.Value.Immediate16 + 256; j++)
						AddLabel(j);
				else throw new Exception("Invalid jump instruction");
			}

			SortedSet<UInt16> MarkedLabels = new SortedSet<ushort>();
			for (int i = start; i < end; i++)
			{
				var entry = ctx.Disasm.Entries[i];

				if (entry.IsData)
					continue;

				var instr = entry.Instr.Value.Instruction;

				//This is not really useful as visual studio can't debug "Lightweight functions"
				if (ctx.BreakPoints.Contains(entry.Offset))
					gen.Emit(OpCodes.Break); 

				//Mark labels as we go
				if (ctx.Labels.ContainsKey(entry.Offset))
				{
					gen.MarkLabel(ctx.Labels[entry.Offset]);
					MarkedLabels.Add(entry.Offset);
				}

				LinkEmitters[instr](ctx, entry, gen);
			}

			//Set all other labels to end of the fnction
			foreach (var lbl in ctx.Labels)
				if (!MarkedLabels.Contains(lbl.Key))
					gen.MarkLabel(lbl.Value);
			
			gen.Emit(OpCodes.Ret);
		}
	}
}
