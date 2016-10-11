#include "cpu.h"
#include "i8085cpu.h"

#define VERBOSE 0

#define LOG(x) do { if (VERBOSE) logerror x; } while (0)

#define CPUTYPE_8080    0
#define CPUTYPE_8085    1



/***************************************************************************
MACROS
***************************************************************************/

#define IS_8080()          (m_cputype == CPUTYPE_8080)
#define IS_8085()          (m_cputype == CPUTYPE_8085)


namespace {

	/***************************************************************************
	STATIC TABLES
	***************************************************************************/
	/* cycles lookup */
	static const UINT8 lut_cycles_8080[256] = {
		/*      0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F  */
		/* 0 */ 4, 10,7, 5, 5, 5, 7, 4, 4, 10,7, 5, 5, 5, 7, 4,
		/* 1 */ 4, 10,7, 5, 5, 5, 7, 4, 4, 10,7, 5, 5, 5, 7, 4,
		/* 2 */ 4, 10,16,5, 5, 5, 7, 4, 4, 10,16,5, 5, 5, 7, 4,
		/* 3 */ 4, 10,13,5, 10,10,10,4, 4, 10,13,5, 5, 5, 7, 4,
		/* 4 */ 5, 5, 5, 5, 5, 5, 7, 5, 5, 5, 5, 5, 5, 5, 7, 5,
		/* 5 */ 5, 5, 5, 5, 5, 5, 7, 5, 5, 5, 5, 5, 5, 5, 7, 5,
		/* 6 */ 5, 5, 5, 5, 5, 5, 7, 5, 5, 5, 5, 5, 5, 5, 7, 5,
		/* 7 */ 7, 7, 7, 7, 7, 7, 7, 7, 5, 5, 5, 5, 5, 5, 7, 5,
		/* 8 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* 9 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* A */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* B */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* C */ 5, 10,10,10,11,11,7, 11,5, 10,10,10,11,11,7, 11,
		/* D */ 5, 10,10,10,11,11,7, 11,5, 10,10,10,11,11,7, 11,
		/* E */ 5, 10,10,18,11,11,7, 11,5, 5, 10,5, 11,11,7, 11,
		/* F */ 5, 10,10,4, 11,11,7, 11,5, 5, 10,4, 11,11,7, 11
	};

	static const UINT8 lut_cycles_8085[256] = {
		/*      0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F  */
		/* 0 */ 4, 10,7, 6, 4, 4, 7, 4, 10,10,7, 6, 4, 4, 7, 4,
		/* 1 */ 7, 10,7, 6, 4, 4, 7, 4, 10,10,7, 6, 4, 4, 7, 4,
		/* 2 */ 7, 10,16,6, 4, 4, 7, 4, 10,10,16,6, 4, 4, 7, 4,
		/* 3 */ 7, 10,13,6, 10,10,10,4, 10,10,13,6, 4, 4, 7, 4,
		/* 4 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* 5 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* 6 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* 7 */ 7, 7, 7, 7, 7, 7, 5, 7, 4, 4, 4, 4, 4, 4, 7, 4,
		/* 8 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* 9 */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* A */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* B */ 4, 4, 4, 4, 4, 4, 7, 4, 4, 4, 4, 4, 4, 4, 7, 4,
		/* C */ 6, 10,10,10,11,12,7, 12,6, 10,10,12,11,11,7, 12,
		/* D */ 6, 10,10,10,11,12,7, 12,6, 10,10,10,11,10,7, 12,
		/* E */ 6, 10,10,16,11,12,7, 12,6, 6, 10,5, 11,10,7, 12,
		/* F */ 6, 10,10,4, 11,12,7, 12,6, 6, 10,4, 11,10,7, 12
	};
	namespace priv {
		template<class Function, std::size_t... Indices>
		constexpr auto make_array_helper(Function f, std::index_sequence<Indices...>)
			->std::array<typename std::result_of<Function(std::size_t)>::type, sizeof...(Indices)>
		{
			return{ { f(Indices)... } };
		}

		template<int N, class Function>
		constexpr auto make_array(Function f)
			->std::array<typename std::result_of<Function(std::size_t)>::type, N>
		{
			return make_array_helper(f, std::make_index_sequence<N>{});
		}
		template<typename T>
		constexpr size_t simple_bit_count(T x) { return x ? size_t(x & 1) + simple_bit_count(x >> 1) : 0; }
		constexpr uint8_t zs_caculate(uint8_t i) { return ((i == 0) ? ZF : 0) | ((i & 128) ? SF : 0); }
		constexpr uint8_t zsp_caculate(uint8_t i) { return zs_caculate(i) | ((simple_bit_count(i) & 1) ? 0 : PF); }
	};
	constexpr auto zs_flags = priv::make_array<256>(priv::zs_caculate);
	constexpr auto zsp_flags = priv::make_array<256>(priv::zsp_caculate);
};


/*
UINT8 zs;
int i, p;
for (i = 0; i < 256; i++)
{
	/* cycles 
	lut_cycles[i] = m_cputype ? lut_cycles_8085[i] : lut_cycles_8080[i];

	/* flags 
	zs = 0;
	if (i == 0) zs |= ZF;
	if (i & 128) zs |= SF;
	p = 0;
	if (i & 1) ++p;
	if (i & 2) ++p;
	if (i & 4) ++p;
	if (i & 8) ++p;
	if (i & 16) ++p;
	if (i & 32) ++p;
	if (i & 64) ++p;
	if (i & 128) ++p;
	ZS[i] = zs;
	ZSP[i] = zs | ((p & 1) ? 0 : PF);
}


void i8085_cpu_t::set_status(UINT8 status)
{
	if (status != m_STATUS)
		m_out_status_func(status);

	m_STATUS = status;
}
*/