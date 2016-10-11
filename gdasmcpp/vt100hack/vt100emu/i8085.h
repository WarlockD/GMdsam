#pragma once
#include "cpu.h"

class i8085_cpu_t {


protected:
	devcb_write8 m_mem_write;
	devcb_read8 m_mem_read;
	devcb_write8 m_io_write;
	devcb_read8 m_io_read;

	using devcb_write = std::function<void(int, int)>;
	using devcb_read = std::function<int(int)>;
	using devcb_write8 = std::function<void(int, UINT8)>;
	using devcb_read8 = std::function<UINT8(int)>;
	using devcb_read_line = std::function<int()>;
	using devcb_write_line = std::function<void(int)>;
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
	UINT8 lut_cycles[256];
	/* flags lookup */
	UINT8 ZS[256];
	UINT8 ZSP[256];
	/*

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
	*/
};