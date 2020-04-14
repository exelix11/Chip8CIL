#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include "InstrImpl.h"

#undef NDEBUG
#include <assert.h>

const uint8_t font[] = {
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

void Chip8_InitState(Chip8State* state, uint16_t* rom, int romsz)
{
	memset(state, 0, sizeof(Chip8State));
	memcpy(state->RAM + FontStart, font, sizeof(font));
	state->Stack = state->RAM + StackStart;
	if (rom)
	{
		assert(romsz < TotalRAM - ProgramStart);
		memcpy(state->RAM + ProgramStart, rom, romsz);
		state->PC = ProgramStart;
	}
}

const InstructionDefinition instructionTable[] = 
{
	{"CLS", 0x0, 0x0, 0xE, 0x0, CLS},
	{"RET", 0x0, 0x0, 0xE, 0xE, RET},
	{"SYS", 0x0, N_IMM3, N_IMM3, N_IMM3, SYS},
	{"JMP", 0x1, N_IMM3, N_IMM3, N_IMM3, JMP},
	{"CALL", 0x2, N_IMM3, N_IMM3, N_IMM3, CALL},
	{"SEI", 0x3, N_REGX, N_IMM2, N_IMM2, SEI},
	{"SNEI", 0x4, N_REGX, N_IMM2, N_IMM2, SNEI},
	{"SER", 0x5, N_REGX, N_REGY, 0, SER},
	{"LDI", 0x6, N_REGX, N_IMM2, N_IMM2, LDI},
	{"ADDI", 0x7, N_REGX, N_IMM2, N_IMM2, ADDI},
	
	{"LD", 0x8, N_REGX, N_REGY, 0, LD},
	{"OR", 0x8, N_REGX, N_REGY, 1, OR},
	{"AND", 0x8, N_REGX, N_REGY, 2, AND},
	{"XOR", 0x8, N_REGX, N_REGY, 3, XOR},
	{"ADD", 0x8, N_REGX, N_REGY, 4, ADD},
	{"SUB", 0x8, N_REGX, N_REGY, 5, SUB},
	{"SHR", 0x8, N_REGX, N_REGY, 6, SHR},
	{"SUBN", 0x8, N_REGX, N_REGY, 7, SUBN},
	{"SHL", 0x8, N_REGX, N_REGY, 0xE, SHL},
	{"SNER", 0x9, N_REGX, N_REGY, 0, SNER},
	
	{"LII", 0xA, N_IMM3, N_IMM3, N_IMM3, LII}, //Load register I immediate,
	{"JMP0", 0xB, N_IMM3, N_IMM3, N_IMM3, JMP0},
	
	{"RND", 0xC, N_REGX, N_IMM2, N_IMM2, RND},
	{"DRW", 0xD, N_REGX, N_REGY, N_IMM1, DRW},
	{"SKP", 0xE, N_REGX, 0x9, 0xE, SKP},
	{"SKNP", 0xE, N_REGX, 0xA, 0x1, SKNP},
	
	{"SDT", 0xF, N_REGX, 0x0, 0x7, SDT}, //Store DT to Register
	{"IN", 0xF, N_REGX, 0x0, 0xA, IN},
	
	{"LDT", 0xF, N_REGX, 0x1, 0x5, LDT}, //Load DT from immediate
	{"LST", 0xF, N_REGX, 0x1, 0x8, LST},
	{"ADDII", 0xF, N_REGX, 0x1, 0xE, ADDII}, //Add register I immediate
	{"LIFNT", 0xF, N_REGX, 0x2, 0x9, LIFNT}, //Load font addr to I
	{"STBCD", 0xF, N_REGX, 0x3, 0x3, STBCD},
	{"STREG", 0xF, N_REGX, 0x5, 0x5, STREG},
	{"LDREG", 0xF, N_REGX, 0x6, 0x5, LDREG},
	{NULL, NULL, NULL, NULL, NULL, NULL}
};

#define NIBBLE_A(x) ((x & 0xF000) >> 12)
#define NIBBLE_B(x) ((x & 0x0F00) >> 8)
#define NIBBLE_C(x) ((x & 0x00F0) >> 4)
#define NIBBLE_D(x) ((x & 0x000F))

#define IS_INSTRUCTION_NIBBLE(x) (x < 0x10)

bool ParseInstruction(Chip8Instruction* out, uint16_t instruction)
{
	memset(out, 0, sizeof(*out));
	const InstructionDefinition* cur = instructionTable;
	for (;cur->Name;cur++)
	{
		if (NIBBLE_A(instruction) != cur->A) continue;
		if (IS_INSTRUCTION_NIBBLE(cur->B) && NIBBLE_B(instruction) != cur->B) continue;
		if (IS_INSTRUCTION_NIBBLE(cur->C) && NIBBLE_C(instruction) != cur->C) continue;
		if (IS_INSTRUCTION_NIBBLE(cur->D) && NIBBLE_D(instruction) != cur->D) continue;

		if (!IS_INSTRUCTION_NIBBLE(cur->B))
		{
			if (cur->B == N_IMM3) out->Immediate16 = instruction & 0x0FFF;
			else if (cur->B == N_REGX) out->regx = NIBBLE_B(instruction);
			else assert(0);
		}

		if (!IS_INSTRUCTION_NIBBLE(cur->C))
		{
			if (cur->C == N_IMM3) {} //Assigned before
			else if (cur->C == N_IMM2) out->Immediate16 = instruction & 0x00FF;
			//else if (cur->C == N_REGX) out->regx = NIBBLE_C(instruction);
			else if (cur->C == N_REGY) out->regy = NIBBLE_C(instruction);
			else assert(0);
		}

		if (!IS_INSTRUCTION_NIBBLE(cur->D))
		{
			if (cur->D == N_IMM3) {} //Assigned before
			else if (cur->D == N_IMM2) {} 
			else if (cur->D == N_IMM1) out->Immediate16 = instruction & 0x000F;
			else assert(0);
		}
			
		out->Def = cur;
		return true;
	}
	return false;
}

void Chip8_Execute(Chip8State* state) 
{
	for (int i = 0; i < TotalRAM && !state->Terminated; i++)
	{
		uint16_t opCode = (state->RAM[state->PC] << 8) | state->RAM[state->PC + 1];
		Chip8Instruction inst;
		assert(ParseInstruction(&inst, opCode));
		inst.Def->Implementation(state, &inst);
		state->PC += 2;
	}
	//FILE* f = fopen("F:\\img.bin", "wb");
	//fwrite(state->VRAM, 1, sizeof(state->VRAM), f);
	//fclose(f);
}

static Chip8State vmState;

#include <time.h>

int main(int argc, const char** argv)
{
	if (argc != 2)
	{
		printf("Pass the file as argument\n");
		return;
	}
	
	FILE* f = fopen(argv[1], "rb");
	if (!f)
	{
		printf("File %s not found !\n", argv[1]);
		return;
	}	
	fseek(f, 0, SEEK_END);
	int size = ftell(f);
	fseek(f, 0, SEEK_SET);
	uint8_t* rom = malloc(size);
	fread(rom, 1, size, f);
	fclose(f);

	clock_t start, end;	
    start = clock();
    for (int i = 0; i < 5000; i++)
	{
		Chip8_InitState(&vmState, rom, size);
		Chip8_Execute(&vmState);
	}
	end = clock();
    double time = ((double) (end - start)) / CLOCKS_PER_SEC;
	
	//Cheat to have the output line up with the C# ver, we know it won't take more than 9 seconds anyway
	printf("00:00:0%.7lf C interpreter\n", time);
	
	free(rom);
}
