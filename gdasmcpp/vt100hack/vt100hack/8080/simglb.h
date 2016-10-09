/*
 * Z80SIM  -  a	Z80-CPU	simulator
 *
 * Copyright (C) 1987-2014 by Udo Munk
 *
 * History:
 * 28-SEP-87 Development on TARGON/35 with AT&T Unix System V.3
 * 11-JAN-89 Release 1.1
 * 08-FEB-89 Release 1.2
 * 13-MAR-89 Release 1.3
 * 09-FEB-90 Release 1.4  Ported to TARGON/31 M10/30
 * 20-DEC-90 Release 1.5  Ported to COHERENT 3.0
 * 10-JUN-92 Release 1.6  long casting problem solved with COHERENT 3.2
 *			  and some optimisation
 * 25-JUN-92 Release 1.7  comments in english and ported to COHERENT 4.0
 * 02-OCT-06 Release 1.8  modified to compile on modern POSIX OS's
 * 18-NOV-06 Release 1.9  modified to work with CP/M sources
 * 08-DEC-06 Release 1.10 modified MMU for working with CP/NET
 * 17-DEC-06 Release 1.11 TCP/IP sockets for CP/NET
 * 25-DEC-06 Release 1.12 CPU speed option
 * 19-FEB-07 Release 1.13 various improvements
 * 06-OCT-07 Release 1.14 bug fixes and improvements
 * 06-AUG-08 Release 1.15 many improvements and Windows support via Cygwin
 * 25-AUG-08 Release 1.16 console status I/O loop detection and line discipline
 * 20-OCT-08 Release 1.17 frontpanel integrated and Altair/IMSAI emulations
 * 24-JAN-14 Release 1.18 bug fixes and improvements
 * 02-MAR-14 Release 1.19 source cleanup and improvements
 * 14-MAR-14 Release 1.20 added Tarbell SD FDC and printer port to Altair
 * 29-MAR-14 Release 1.21 many improvements
 * 29-MAY-14 Release 1.22 improved networking and bugfixes
 * 04-JUN-14 Release 1.23 added 8080 emulation
 */

/*
 *	Declaration of variables in simglb.c
 */

extern int	cpu;

extern BYTE	A, B, C, D, E, H, L, A_, B_, C_, D_, E_, H_, L_,
		*PC, *STACK, I, IFF;
extern WORD	IX, IY;
extern int	F, F_;
extern long	R;
extern BYTE	io_port;
extern BYTE	mem_wp;

#ifdef BUS_8080
extern BYTE	cpu_bus;
#endif

extern BYTE	ram[],touched[],*wrk_ram, cpu_state, int_data;

extern int	s_flag, l_flag, m_flag, x_flag, break_flag, i_flag, f_flag,
		cpu_error, int_nmi, int_int, int_mode, cntl_c, cntl_bs,
		parity[], sb_next, int_protection;

#ifdef Z80_UNDOC
extern int	u_flag;
#endif

extern int	tmax;
extern int	busy_loop_cnt[];

extern char	xfn[];

#ifdef HISIZE
extern struct	history	his[];
extern int	h_next,	h_flag;
#endif

#ifdef SBSIZE
extern struct	softbreak soft[];
#endif

#ifdef WANT_TIM
extern long	t_states;
extern int	t_flag;
extern BYTE	*t_start, *t_end;
extern unsigned long long t_ticks;
#endif

#ifdef FRONTPANEL
extern unsigned long long fp_clock;
extern WORD fp_led_address;
extern BYTE fp_led_data;
extern WORD address_switch;
extern BYTE fp_led_output;
#endif
