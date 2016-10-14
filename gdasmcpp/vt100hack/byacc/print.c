#include "defs.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>  // For va_start, etc.

#ifdef _DEBUG
#ifdef _WIN32
#include <Windows.h>
#endif
#endif

static char* s_buffer = 0;
static size_t s_buffer_size = 0;

static char* get_buffer(size_t size) {
	if (!s_buffer || s_buffer_size < size) {
		s_buffer_size = size * 2;
		if (s_buffer == 0) s_buffer = malloc(s_buffer_size);
		else s_buffer = realloc(s_buffer, s_buffer_size);
	}
	return s_buffer;
}
void debug_output_verbose(const char* buffer) {
	OutputDebugStringA("VERBOSE: ");
	OutputDebugStringA(buffer);
}
void debug_output_stderr(const char* buffer) {
	OutputDebugStringA("STDERR: ");
	OutputDebugStringA(buffer);
}

// fprint redirect for error checking
void byacc_fprintf(FILE* file, const char *format, ...) {
	size_t size = strlen(format) * 2 + 50; // appromate
	va_list ap;
	char* buffer;
	while (1) {
		buffer = get_buffer(size);
		va_start(ap, format);
#ifdef _WIN32
		int n = vsnprintf_s(buffer, s_buffer_size, s_buffer_size, format, ap);
#else
		int n = vsnprintf(buffer, s_buffer_size, format, ap);
#endif
		va_end(ap);
		if (n > -1 && n < size) {  // Everything worked
			break;
		}
		if (n > -1)  // Needed size returned
			size = n + 1;   // For null char
		else
			size *= 2;      // Guess at a larger size (OS specific)
	}
	assert(file);
	fputs(buffer, file);
#ifdef _DEBUG
	if (verbose_file == file) debug_output_verbose(buffer);
	if (stderr == file) debug_output_stderr(buffer);
#endif
}