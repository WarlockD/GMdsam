TARGET=vt100sim

LIBS=-lncurses

OBJS=main.o nvr.o keyboard.o vt100sim.o \
	8080/simglb.o \
	8080/sim1a.o \
	8080/simint.o \
	pusart.o

all: $(TARGET)

$(TARGET): $(OBJS)
	g++ -o $(TARGET) $^ $(LIBS)

$(OBJS): keyboard.h nvr.h optionparser.h pusart.h vt100sim.h

clean:
	@-rm -f $(OBJS) $(TARGET)

wide:
	$(MAKE) all CXXFLAGS=-I/usr/include/ncursesw LIBS=-lncursesw
