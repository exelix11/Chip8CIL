using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chip8Sharp.Instructions
{
	public class Disassembler
	{
		Dictionary<Instruction, Func<ParsedInstruction, string>> InstructionTable;
		Decoder dec = new Decoder();

		public Disassembler() 
		{
			InstructionTable = typeof(Instruction)
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.ToDictionary(x => (Instruction)x.GetValue(null), y => MakeDisasmMethod(y.GetCustomAttribute<InstructionType>()));
		}

		private Func<ParsedInstruction, string> MakeDisasmMethod(InstructionType type) 
		{
			if (type is TypeA)
				return i => $"{i.Instruction}";
			else if (type is TypeB)
				return i =>
				{
					if (i.GeneratesLabel())
						return $"{i.Instruction} off_{i.GetLabelTarget().ToString("X4")}";
					else
						return $"{i.Instruction} 0x{i.Immediate16.ToString("X4")}";
				};
			else if (type is TypeC)
				return i => $"{i.Instruction} V{i.Reg0.ToString("X")} 0x{i.Immediate8.ToString("X2")}";
			else if (type is TypeD)
				return i => $"{i.Instruction} V{i.Reg0.ToString("X")} V{i.Reg1.ToString("X")}";
			else if (type is TypeE)
				return i => $"{i.Instruction} V{i.Reg0.ToString("X")} V{i.Reg1.ToString("X")} 0x0{i.Immediate4.ToString("X1")}";
			else if (type is TypeF)
				return i => $"{i.Instruction} V{i.Reg0.ToString("X")}";
			throw new Exception("Unknown instruction type");
		}

		public string DisassembleLine(ParsedInstruction inst) =>
			InstructionTable[inst.Instruction](inst);

		public string DisassembleLine(Span<byte> data)
		{
			UInt16 instr = (UInt16)((data[0] << 8) | data[1]);

			if (dec.TryDecode(instr, out ParsedInstruction inst))
				return DisassembleLine(inst);
			else return $".byte {data[0].ToString("X2")} {data[1].ToString("X2")}";
		}

		public (string, ParsedInstruction?) DisassembleLineInstr(Span<byte> data)
		{
			UInt16 instr = (UInt16)((data[0] << 8) | data[1]);

			if (dec.TryDecode(instr, out ParsedInstruction inst))
				return (DisassembleLine(inst), inst);
			else return ($".byte {data[0].ToString("X2")} {data[1].ToString("X2")}", null);
		}

		public struct DecompEntry
		{
			public UInt16 Offset;
			public bool IsData => !Instr.HasValue;
			public string Text;
			public ParsedInstruction? Instr;

			public DecompEntry(string data, int offset, ParsedInstruction? inst = null)
			{
				Offset = (UInt16)offset;
				Instr = inst;
				Text = data;
			}

			public Instruction Instruction => Instr.Value.Instruction;
			public ParsedInstruction Value => Instr.Value;
		}

		public struct Disassembly
		{
			public DecompEntry[] Entries;
			public SortedSet<UInt16> Labels;

			public override string ToString()
			{
				StringBuilder sbi = new StringBuilder();
				UInt16 CurOffset = Chip8State.ProgramStart;
				bool isInLabel = false;
				for (int i = 0; i < Entries.Length; i++)
				{
					if (Labels.Contains(CurOffset))
					{
						sbi.Append($"off_{CurOffset.ToString("X4")}:\n");
						isInLabel = true;
					}

					if (isInLabel)
						sbi.Append("\t");

					if (Entries[i].IsData)
						sbi.AppendLine(Entries[i].Text);
					else
					{
						sbi.AppendLine(Entries[i].Text);

						if (Entries[i].Instruction == Instruction.RET)
							isInLabel = false;
					}

					CurOffset += 2;
				}
				return sbi.ToString();
			}
		}

		public Disassembly DisassembleProgram(Span<byte> b)
		{
			List<DecompEntry> entries = new List<DecompEntry>();
			List<UInt16> labels = new List<UInt16>();

			UInt16 Offset = 0;

			while(b.Length >= 2)
			{
				var (text, inst) = DisassembleLineInstr(b);

				if (inst.HasValue)
				{
					var instr = inst.Value;

					entries.Add(new DecompEntry(text, Chip8State.ProgramStart + Offset, inst));
					if (instr.GeneratesLabel() && !labels.Contains(instr.GetLabelTarget()))
						labels.Add(instr.GetLabelTarget());
				}
				else
					entries.Add(new DecompEntry(text, Chip8State.ProgramStart + Offset));

				Offset += 2;
				b = b.Slice(2);
			}
			
			return new Disassembly { Entries = entries.ToArray(), Labels = new SortedSet<ushort>(labels) };
		}
	}
}
