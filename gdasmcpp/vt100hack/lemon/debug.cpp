#include <Windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _DEBUG
void ErrorMsg(const char *filename, int lineno, const char *format, ...) {
	char buffer[1000];
	va_list ap;
	va_start(ap, format);
	auto count = vsnprintf_s(buffer, 1000, format, ap);
	va_end(ap);
	buffer[count++] = '\n'; buffer[count++] = 0;
	::OutputDebugStringA(buffer);
	fprintf(stderr, buffer);
}

#endif