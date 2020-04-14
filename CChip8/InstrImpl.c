#include "InstrImpl.h"
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

#undef NDEBUG
#include <assert.h>

#define REGX state->Registers[inst->regx]
#define REGY state->Registers[inst->regy]
#define IMM16 inst->Immediate16
#define IMM8 ((uint8_t)(inst->Immediate16 & 0x00FF))
#define IMM4 ((uint8_t)(inst->Immediate16 & 0x000F))

#define VF Registers[0xF]

void SYS(Chip8State* state, Chip8Instruction* inst)
{
	printf("WARN: Sys called\n");
}

void CLS(Chip8State* state, Chip8Instruction* inst)
{
	memset(state->VRAM, 0, sizeof(state->VRAM));
}

void RET(Chip8State* state, Chip8Instruction* inst)
{
	state->PC = state->Stack[state->SP] - 2;
	state->SP--;
}

void JMP(Chip8State* state, Chip8Instruction* inst)
{
	if (state->PC == IMM16)
		state->Terminated = true;

	state->PC = IMM16 - 2;
}

void CALL(Chip8State* state, Chip8Instruction* inst)
{
	state->SP++;
	state->Stack[state->SP] = (uint16_t)(state->PC + 2);
	state->PC = IMM16 - 2;
}

void SEI(Chip8State* state, Chip8Instruction* inst)
{
	if (REGX == IMM8)
		state->PC += 2;
}

void SNEI(Chip8State* state, Chip8Instruction* inst)
{
	if (REGX != IMM8)
		state->PC += 2;
}

void SER(Chip8State* state, Chip8Instruction* inst)
{
	if (REGX == REGY)
		state->PC += 2;
}

void SNER(Chip8State* state, Chip8Instruction* inst)
{
	if (REGX != REGY)
		state->PC += 2;
}

void LDI(Chip8State* state, Chip8Instruction* inst)
{
	REGX = IMM8;
}

void ADDI(Chip8State* state, Chip8Instruction* inst)
{
	REGX += IMM8;
}

void LD(Chip8State* state, Chip8Instruction* inst)
{
	REGX = REGY;
}

void OR(Chip8State* state, Chip8Instruction* inst)
{
	REGX |= REGY;
}

void AND(Chip8State* state, Chip8Instruction* inst)
{
	REGX &= REGY;
}

void XOR(Chip8State* state, Chip8Instruction* inst)
{
	REGX ^= REGY;
}

void ADD(Chip8State* state, Chip8Instruction* inst)
{
	state->VF = (REGX + REGY > 0xFF ? 1 : 0);

	REGX += REGY;
}

void SUB(Chip8State* state, Chip8Instruction* inst)
{
	state->VF = (REGX > REGY ? 1 : 0);

	REGX -= REGY;
}

void SHR(Chip8State* state, Chip8Instruction* inst)
{
	state->VF = (REGX & 1);
	REGX >>= 1;
}

void SHL(Chip8State* state, Chip8Instruction* inst)
{
	state->VF = ((REGX & 0x80) != 0 ? 1 : 0);
	REGX <<= 1;
}

void SUBN(Chip8State* state, Chip8Instruction* inst)
{
	state->VF = (REGX < REGY ? 1 : 0);

	REGX = (REGY - REGX);
}

void LII(Chip8State* state, Chip8Instruction* inst)
{
	state->I = IMM16;
}

void JMP0(Chip8State* state, Chip8Instruction* inst)
{
	state->PC = state->Registers[0] + IMM16 - 2;
}

void RND(Chip8State* state, Chip8Instruction* inst)
{
	REGX = (rand() % 256) & IMM8;
}

void DRW(Chip8State* state, Chip8Instruction* inst)
{
	assert(state->I + REGY + IMM4 < TotalRAM);

	uint8_t* sprite = state->RAM + state->I;
	uint8_t* vmem = state->VRAM;

	state->VF = 0;

	bool unset = false;
	
	for (int i = 0; i < IMM4; i++)
	{
		uint8_t sprSrc = sprite[i];

		int y = REGY + i;
		int actualY = (y >= DisplayH) ? (y - DisplayH) : y;
		int offset = REGX + actualY * DisplayW;

		int byteoffset = offset / 8;
		int bitoffset = offset % 8;

		uint8_t* block0 = vmem + byteoffset;
		uint8_t* block1 = vmem + byteoffset + 1;

		uint8_t newblock0 = *block0 ^ (sprSrc >> bitoffset);
		uint8_t newblock1 = *block1 ^ (sprSrc << (8 - bitoffset));

		if (!unset)
		{
			if ((*block0 ^ (*block0 & newblock0)) != 0 ||
				(*block1 ^ (*block1 & newblock1)) != 0)
			{
				unset = true;
			}
		}

		*block0 = newblock0;
		*block1 = newblock1;
	}
	
	if (unset)
		state->VF = 1;

	state->VmemUpdated = true;	
}

void SKP(Chip8State* state, Chip8Instruction* inst)
{
	printf("WARN: Unimplemented input instruction");

	//Skip next instruction if key with the value of Vx is pressed.
}

void SKNP(Chip8State* state, Chip8Instruction* inst)
{
	printf("WARN: Unimplemented input instruction");

	//Skip next instruction if key with the value of Vx is pressed.
}

void SDT(Chip8State* state, Chip8Instruction* inst)
{
	REGX = state->DT;
}

void IN(Chip8State* state, Chip8Instruction* inst)
{
	printf("WARN: Unimplemented input instruction");
	//Wait for a key press, store the value of the key in Vx.
	REGX = 0;
}

void LDT(Chip8State* state, Chip8Instruction* inst)
{
	state->DT = REGX;
}

void LST(Chip8State* state, Chip8Instruction* inst)
{
	state->ST = REGX;
}

void ADDII(Chip8State* state, Chip8Instruction* inst)
{
	state->I += REGX;
}

void LIFNT(Chip8State* state, Chip8Instruction* inst)
{
	state->I = (uint16_t)(FontStart + REGX * 5);
}

void STBCD(Chip8State* state, Chip8Instruction* inst)
{
	int hundreds = (REGX / 100) % 10;
	int dec = (REGX / 10) % 10;
	int unit = REGX % 10;

	state->RAM[state->I] = hundreds;
	state->RAM[state->I + 1] = dec;
	state->RAM[state->I + 2] = unit;
}

void STREG(Chip8State* state, Chip8Instruction* inst)
{
	memcpy(state->RAM + state->I, state->Registers, inst->regx + 1);
}

void LDREG(Chip8State* state, Chip8Instruction* inst)
{
	memcpy(state->Registers, state->RAM + state->I, inst->regx + 1);
}