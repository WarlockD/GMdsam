#ifndef NVR_H
#define NVR_H

#include <stdint.h>

// Emulate an ER1400 chip in the VT100
class NVR
{
    // The non-volatile ram represents a 14x100 bit array.
public:
    NVR();
    void set_latch(uint8_t latch);
    bool data();
    void clock(bool rising);

    // persistence
    void load(char* path);
    void save(char* path);
private:
    uint32_t address_reg; // 20 bits
    uint16_t data_reg; // 14 bits
    uint16_t contents[100];
    uint8_t latch_last;
    bool out; // value of the output line
};

#endif // NVR_H
