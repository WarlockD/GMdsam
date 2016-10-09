#include "nvr.h"
#include <stdio.h>
#include <curses.h>

/*
 * The NVRAM is 1400 bit 'flash' device with 100 14-bit words.
 *
 * The address lines of the device are individual row and column selectors
 * into a 10x10 array of 14 bit cells. Each cell (14bits) can be erased and
 * written individually.
 *
 * The write is an expensive operation taking 20 milliseconds and
 * a 35Volt power line.
 */

typedef enum {
    STANDBY = 0b111,
    ACCEPT_ADDR = 0b001,
    ERASE = 0b101,
    ACCEPT_DATA = 0b000,
    WRITE = 0b100,
    READ = 0b110,
    SHIFT_OUT = 0b010,
    UNUSED = 0b011
} Commands;

extern WINDOW* msgWin;

uint16_t romcontents[100] = {
0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80,
0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80,
0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80,
0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e80, 0x2e00,
0x2e08, 0x2e8e, 0x2e00, 0x2e50, 0x2e30, 0x2e40, 0x2e20, 0x2e00, 0x2ee0, 0x2ee0,
0x2e51, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000
};

NVR::NVR() : address_reg(0xffff), data_reg(0), latch_last(STANDBY << 1)
{
    for (uint8_t idx = 0; idx < 100; idx++) {
        contents[idx] = romcontents[idx];
    }
}

void NVR::set_latch(uint8_t latch) {
    this->latch_last = latch;
}

uint8_t compute_addr(uint32_t address_reg) {
    address_reg = ~address_reg;
    uint8_t tens = 0;
    for (uint8_t i = 0; i < 10; i++) {
        if (address_reg & 0x01) tens = 9-i;
        address_reg >>= 1;
    }
    uint8_t ones = 0;
    for (uint8_t i = 0; i < 10; i++) {
        if (address_reg & 0x01) ones = 9-i;
        address_reg >>= 1;
    }
    return tens*10 + ones;
}

void NVR::clock(bool rising) {
    // detect falling edge of !lba7 (rising edge of lba7)
    if (!rising) return;

    uint8_t command = (latch_last>>1) & 0b111;
    uint8_t data_in = latch_last & 0x01;
    switch(command) {
    case STANDBY:
        break;
    case ACCEPT_ADDR:
        address_reg = (address_reg << 1) | data_in;
        break;
    case ERASE:
        // erasing sets all bits to ones
        contents[compute_addr(address_reg)] = 0xffff;
        break;
    case ACCEPT_DATA:
        data_reg = (data_reg << 1) | data_in;
        break;
    case WRITE:
    {
        uint8_t addr = compute_addr(address_reg);
        contents[addr] = data_reg & 0x3fff;
        //wprintw(msgWin,"NVR write %x <- %x\n",addr,data_reg);wrefresh(msgWin);
	save( (char*) /* CPP is Dumb */ "/tmp/vt100.nvr");
    }
        break;
    case READ:
    {
        uint8_t addr = compute_addr(address_reg);
        data_reg = contents[addr];
        //wprintw(msgWin,"NVR read  %x -> %x\n",addr,data_reg);wrefresh(msgWin);
    }
        break;
    case SHIFT_OUT:
        out = data_reg & 0x2000;
        data_reg <<= 1;
        break;
    case UNUSED:
        break;
    }
}

bool NVR::data() {
    return out;
}

void NVR::load(char* path) {
    FILE* f = fopen(path,"rb");
    fread(contents,sizeof(uint16_t),100,f);
    fclose(f);
}

void NVR::save(char* path) {
    FILE* f = fopen(path,"wb");
    // fwrite(contents,sizeof(uint16_t),100,f);
    int i;
    for(i=0; i<100; i++) {
	fprintf(f, "0x%04x,", contents[i]);
	if (i % 10 == 9) fprintf(f, "\n"); else fprintf(f, " ");
    }
    fclose(f);
}
