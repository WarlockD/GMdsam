#!/usr/bin/python

"""
Checks that the sum of all bytes passed to the program modulo 0xff
is zero. Used to ensure that our ROM dumps are correct.

Note that each ROM checksum starts with the accumulator set to the
number of the ROM (1-4 inclusive).
"""

import sys
import argparse

def rlc(i):
    "Rotate 8-bit integer i left one bit"
    return ((i << 1)&0xff) | ((i >> 7)&0x01)

def check_rom(data,rom_num):
    # initialize sum with rom number
    s = int(rom_num)
    for n in data:
        s = rlc(s)
        s = s ^ ord(n[0])
    s = s % 0xff
    if s == 0:
        print("Checksum OK")
    else:
        print("Checksum {0:x} (expected 0)".format(s))
    return s == 0

p = argparse.ArgumentParser()
p.add_argument('-r','--rn',help='ROM number',default=0)
p.add_argument('path',metavar='ROM.bin',type=str,nargs=1,help="path to the ROM")
args=p.parse_args()

rn = args.rn
f = open(args.path[0],"rb")
data = f.read()
f.close()

rv = True

if len(data) == 2048:
    rv = check_rom(data,rn)
elif len(data) == 8192:
    rv = rv and check_rom(data[0:2048],1)
    rv = rv and check_rom(data[2048:4096],2)
    rv = rv and check_rom(data[4096:6144],3)
    rv = rv and check_rom(data[6144:8192],4)
else:
    print "Incorrect ROM size (must be exactly 2K or 8K)"

sys.exit(rv)
