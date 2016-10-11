#pragma once

#include <vector>
#include <iostream>
#include <fstream>
#include <string>
#include <sstream>
#include <map>
#include <unordered_map>
#include <thread>
#include <atomic>
#include <array>

using UINT8 = uint8_t;
using UINT16 = uint16_t;
using UINT32 = uint32_t;
using UINT64 = uint64_t;
using INT8 = int8_t;
using INT16 = int16_t;
using INT32 = int32_t;
using INT64 = int64_t;
// PAIR16 is a 16-bit extension of a PAIR

union PAIR16
{
#ifdef LSB_FIRST
	struct { UINT8 l, h; } b;
	struct { INT8 l, h; } sb;
#else
	struct { UINT8 h, l; } b;
	struct { INT8 h, l; } sb;
#endif
	UINT16 w;
	INT16 sw;
};

using devcb_write = std::function<void(int, int)>;
using devcb_read = std::function<int(int)>;
using devcb_write8 = std::function<void(int,UINT8)>;
using devcb_read8 = std::function<UINT8(int)>;
using devcb_read_line = std::function<int()>;
using devcb_write_line = std::function<void(int)>;
