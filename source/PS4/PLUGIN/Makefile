TARGET        := OrbisControl_game.prx
LIBS          := -lSceLibcInternal -lGoldHEN_Hook -lkernel -lSceSysmodule

TOOLCHAIN     := $(OO_PS4_TOOLCHAIN)
PROJDIR       := source
INTDIR        := build

CFILES        := $(wildcard $(PROJDIR)/*.c)
OBJS          := $(patsubst $(PROJDIR)/%.c, $(INTDIR)/%.o, $(CFILES))

CFLAGS        := --target=x86_64-pc-freebsd12-elf -fPIC -funwind-tables -c \
                 $(EXTRAFLAGS) -isysroot $(TOOLCHAIN) -isystem $(TOOLCHAIN)/include \
                 -std=c11

LDFLAGS       := -m elf_x86_64 -pie --script $(TOOLCHAIN)/link.x --eh-frame-hdr \
                 -L$(TOOLCHAIN)/lib $(LIBS) $(TOOLCHAIN)/lib/crtlib.o

UNAME_S       := $(shell uname -s)

ifeq ($(UNAME_S), Linux)
    CC         := clang
    LD         := ld.lld
    CDIR       := linux
endif

_unused       := $(shell mkdir -p $(INTDIR))

$(TARGET): $(OBJS)
	$(LD) $(OBJS) -o $(INTDIR)/$(PROJDIR).elf $(LDFLAGS)
	$(TOOLCHAIN)/bin/$(CDIR)/create-fself -in=$(INTDIR)/$(PROJDIR).elf \
	-out=$(INTDIR)/$(PROJDIR).oelf --lib=$(TARGET) --paid 0x3800000000000011

$(INTDIR)/%.o: $(PROJDIR)/%.c
	$(CC) $(CFLAGS) -o $@ $<

.PHONY: all clean

.DEFAULT_GOAL := all

all: $(TARGET)

clean:
	rm -f $(TARGET) $(INTDIR)/$(PROJDIR).elf $(INTDIR)/$(PROJDIR).oelf $(OBJS)
