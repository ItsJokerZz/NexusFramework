TOOLCHAIN     := $(OO_PS4_TOOLCHAIN)
PROJDIR       := source
INTDIR        := build
TARGET        := OrbisControl_game.prx

LIBS          := -lSceLibcInternal -lGoldHEN_Hook -lkernel -lSceNet -lSceHttp

CFILES        := $(wildcard $(PROJDIR)/*.c)
OBJS          := $(patsubst $(PROJDIR)/%.c, $(INTDIR)/%.o, $(CFILES))

CFLAGS        := --target=x86_64-pc-freebsd12-elf -fPIC -funwind-tables -c $(EXTRAFLAGS) \
                 -isysroot $(TOOLCHAIN) -isystem $(TOOLCHAIN)/include -std=c11

LDFLAGS       := -m elf_x86_64 -pie --script $(TOOLCHAIN)/link.x --eh-frame-hdr \
                 -L$(TOOLCHAIN)/lib $(LIBS) $(TOOLCHAIN)/lib/crtlib.o

$(TARGET): $(OBJS)
	$(LD) $(OBJS) -o $(INTDIR)/$(PROJDIR).elf $(LDFLAGS) -e plugin_load
	$(TOOLCHAIN)/bin/$(CDIR)/create-fself -in=$(INTDIR)/$(PROJDIR).elf \
    -out=$(INTDIR)/$(PROJDIR).oelf --lib=$(TARGET) --paid 0x3800000000000011

$(INTDIR)/%.o: $(PROJDIR)/%.c
	$(CC) $(CFLAGS) -o $@ $<

_unused       := $(shell mkdir -p $(INTDIR))
UNAME_S       := $(shell uname -s)

ifeq ($(UNAME_S),Linux)
	CC := clang
	CCX := clang++
	LD := ld.lld
	CDIR := linux
	ASMLR := as
endif

ifeq ($(UNAME_S),Darwin)
	CC := /usr/local/opt/llvm/bin/clang
	CCX := /usr/local/opt/llvm/bin/clang++
	LD := /usr/local/opt/llvm/bin/ld.lld
	CDIR := macos
	ASMLR := as
endif

.PHONY: clean target stubs all

.DEFAULT_GOAL := all

clean:
	rm -f $(INTDIR)/$(PROJDIR).oelf $(OBJS) \
    $(TARGET) $(TARGETSTUB) $(INTDIR)/$(PROJDIR).elf
           
target: $(TARGET)

stubs: $(TARGETSTUB)

all: clean target stubs
