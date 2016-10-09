TARGET=haxrom.bin
AS=/usr/local/bin/asm8080
ORIGINAL_ASM=basic.d80
CHECKSUM=./checksum.py

all: haxrom.bin

tests: testasm checksums

checksums: 23-061E2.bin 23-032E2.bin 23-033E2.bin 23-034E2.bin
	$(CHECKSUM) -r 1 <23-061E2.bin
	$(CHECKSUM) -r 2 <23-032E2.bin
	$(CHECKSUM) -r 3 <23-033E2.bin
	$(CHECKSUM) -r 4 <23-034E2.bin

%.bin: %.d80
	$(AS) -l$*.lst -o$* $<
	truncate --size=8K $*.bin

test.d80: basic.d80
	cp basic.d80 test.d80

testasm: test.bin
	cmp -l test.bin basic.bin
