#pragma once
#include <stdint.h>
#include <stdbool.h>

#define ProgramStart 0x200
#define TotalRAM 4096
#define FontStart 0x100
#define FontLenght 0x100
#define StackStart 0x2
#define StackLength 0x98

#define DisplayW 64
#define DisplayH 32

typedef struct {
	uint8_t Registers[0x10];
	uint16_t I;
	uint8_t DT, ST;

	uint16_t PC, IR;
	uint8_t SP;

	uint8_t RAM[TotalRAM];
	uint8_t VRAM[DisplayW * DisplayH / 8];
	uint16_t* Stack;

	bool Terminated;
	bool VmemUpdated;
} Chip8State;

struct Chip8Instruction_s;
typedef struct Chip8Instruction_s Chip8Instruction;

typedef void (*InstructionImpl)(Chip8State* state, Chip8Instruction* inst);

typedef struct {
	const char* Name;
	uint8_t A;
	uint8_t B;
	uint8_t C;
	uint8_t D;
	InstructionImpl Implementation;
} InstructionDefinition;

struct Chip8Instruction_s{
	const InstructionDefinition* Def;
	uint16_t Immediate16;
	uint8_t regx;
	uint8_t regy;
};

#define N_REGX 0xFF
#define N_REGY 0xFE
#define N_IMM3 0xFD
#define N_IMM2 0xFC
#define N_IMM1 0xFB

void SYS(Chip8State* state, Chip8Instruction* inst);
void CLS(Chip8State* state, Chip8Instruction* inst);
void RET(Chip8State* state, Chip8Instruction* inst);
void JMP(Chip8State* state, Chip8Instruction* inst);
void CALL(Chip8State* state, Chip8Instruction* inst);
void SEI(Chip8State* state, Chip8Instruction* inst);
void SNEI(Chip8State* state, Chip8Instruction* inst);
void SER(Chip8State* state, Chip8Instruction* inst);
void SNER(Chip8State* state, Chip8Instruction* inst);
void LDI(Chip8State* state, Chip8Instruction* inst);
void ADDI(Chip8State* state, Chip8Instruction* inst);
void LD(Chip8State* state, Chip8Instruction* inst);
void OR(Chip8State* state, Chip8Instruction* inst);
void AND(Chip8State* state, Chip8Instruction* inst);
void XOR(Chip8State* state, Chip8Instruction* inst);
void ADD(Chip8State* state, Chip8Instruction* inst);
void SUB(Chip8State* state, Chip8Instruction* inst);
void SHR(Chip8State* state, Chip8Instruction* inst);
void SHL(Chip8State* state, Chip8Instruction* inst);
void SUBN(Chip8State* state, Chip8Instruction* inst);
void LII(Chip8State* state, Chip8Instruction* inst);
void JMP0(Chip8State* state, Chip8Instruction* inst);
void RND(Chip8State* state, Chip8Instruction* inst);
void DRW(Chip8State* state, Chip8Instruction* inst);
void SKP(Chip8State* state, Chip8Instruction* inst);
void SKNP(Chip8State* state, Chip8Instruction* inst);
void SDT(Chip8State* state, Chip8Instruction* inst);
void IN(Chip8State* state, Chip8Instruction* inst);
void LDT(Chip8State* state, Chip8Instruction* inst);
void LST(Chip8State* state, Chip8Instruction* inst);
void ADDII(Chip8State* state, Chip8Instruction* inst);
void LIFNT(Chip8State* state, Chip8Instruction* inst);
void STBCD(Chip8State* state, Chip8Instruction* inst);
void STREG(Chip8State* state, Chip8Instruction* inst);
void LDREG(Chip8State* state, Chip8Instruction* inst);