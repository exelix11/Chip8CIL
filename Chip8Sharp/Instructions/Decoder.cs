using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Chip8Sharp.Instructions
{
	public class Decoder
	{
		private Dictionary<InstructionType, Instruction> Instructions;	

		public Decoder() 
		{
			Instructions = new Dictionary<InstructionType, Instruction>();

			foreach (var m in typeof(Instruction).GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				var att = m.GetCustomAttribute<InstructionType>();
				Debug.Assert(att != null);
				Instructions.Add(att, (Instruction)m.GetValue(null));
			}
		}

		public bool TryDecode(UInt16 val, out ParsedInstruction inst)
		{
			var m = Instructions.Keys.FirstOrDefault(x => x.Matches(val));
			if (m == null) 
			{
				inst = new ParsedInstruction(0, 0, 0, 0);
				return false;
			}
			inst = m.Parse(val, Instructions[m]);
			return true;
		}

		public ParsedInstruction Decode(UInt16 val)
		{
			if (TryDecode(val, out ParsedInstruction res))
				return res;
			throw new Exception("Unknown instruction");
		}
	}
}
