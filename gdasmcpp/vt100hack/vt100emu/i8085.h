#pragma once
#include "cpu.h"
enum
{
	I8085_PC, I8085_SP, I8085_AF, I8085_BC, I8085_DE, I8085_HL,
	I8085_A, I8085_B, I8085_C, I8085_D, I8085_E, I8085_F, I8085_H, I8085_L,
	I8085_STATUS, I8085_SOD, I8085_SID, I8085_INTE,
	I8085_HALT, I8085_IM,

	I8085_GENPC ,//= STATE_GENPC,
	I8085_GENSP ,//= STATE_GENSP,
	I8085_GENPCBASE //= STATE_GENPCBASE
};

#define I8085_INTR_LINE     0
#define I8085_RST55_LINE    1
#define I8085_RST65_LINE    2
#define I8085_RST75_LINE    3

#define I8085_STATUS_INTA   0x01
#define I8085_STATUS_WO     0x02
#define I8085_STATUS_STACK  0x04
#define I8085_STATUS_HLTA   0x08
#define I8085_STATUS_OUT    0x10
#define I8085_STATUS_M1     0x20
#define I8085_STATUS_INP    0x40
#define I8085_STATUS_MEMR   0x80


class i8085_base : cpu_device{
protected:
	i8085_base(bool is_8085);
	virtual void reset() override;
	int                 m_cputype;        /* 0 8080, 1 8085A */
	PAIR16              m_PC, m_SP, m_AF, m_BC, m_DE, m_HL, m_WZ;
	UINT8               m_HALT;
	UINT8               m_IM;             /* interrupt mask (8085A only) */
	UINT8               m_STATUS;         /* status word */

	UINT8               m_after_ei;       /* post-EI processing; starts at 2, check for ints at 0 */
	UINT8               m_nmi_state;      /* raw NMI line state */
	UINT8               m_irq_state[4];   /* raw IRQ line states */
	UINT8               m_trap_pending;   /* TRAP interrupt latched? */
	UINT8               m_trap_im_copy;   /* copy of IM register when TRAP was taken */
	UINT8               m_sod_state;      /* state of the SOD line */

	bool                m_ietemp;         /* import/export temp space */

										  /* cycles lookup */
//	const UINT8* lut_cycles; [256];
	/* flags lookup */
	//UINT8 ZS[256];
	//UINT8 ZSP[256];
	void execute_set_input(int irqline, int state);


	void set_sod(int state);
	void set_inte(int state);
	void set_status(UINT8 status);
	UINT8 get_rim_value();
	void break_halt_for_interrupt();
	UINT8 ROP();
	UINT8 ARG();
	UINT16 ARG16();
	UINT8 RM(UINT32 a);
	void WM(UINT32 a, UINT8 v);
	void check_for_interrupts();
	void execute_one(int opcode);
	void init_tables();
};