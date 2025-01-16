TOOLCHAIN     := $(OO_PS4_TOOLCHAIN)
PROJDIR       := source
INTDIR        := build

TARGET        := OrbisControl.prx
BUILD         := $(shell cat $(INTDIR)/.number)
VERSION       := 0.29

LIBS          := -lc -lc++ -lkernel -lGoldHEN_Hook \
                 -lSceSystemService -lSceSysmodule \
                 -lSceNet -lSceHttp -lSceSysUtil

CPPFILES      := $(wildcard $(PROJDIR)/*.cpp)
OBJS          := $(patsubst $(PROJDIR)/%.cpp, $(INTDIR)/%.o, $(CPPFILES))

CFLAGS        := --target=x86_64-pc-freebsd12-elf -fPIC -funwind-tables -c \
                 $(EXTRAFLAGS) -isysroot $(TOOLCHAIN) -isystem $(TOOLCHAIN)/include \
                 -Iinclude -std=c11 -D_DEFAULT_SOURCE -DFMT_HEADER_ONLY

CXXFLAGS      := $(CFLAGS) -isystem $(TOOLCHAIN)/include/c++/v1 \
                 -std=c++11 -Iinclude -DFMT_HEADER_ONLY

LDFLAGS       := -m elf_x86_64 -pie --script $(TOOLCHAIN)/link.x --eh-frame-hdr \
                 -L$(TOOLCHAIN)/lib $(LIBS) $(TOOLCHAIN)/lib/crtlib.o --wrap=_init

$(TARGET): $(INTDIR) $(OBJS) AddToBuildNum
	$(LD) $(OBJS) -o $(INTDIR)/$(PROJDIR).elf $(LDFLAGS) -e module_start
	$(TOOLCHAIN)/bin/$(CDIR)/create-fself -in=$(INTDIR)/$(PROJDIR).elf \
	-out=$(INTDIR)/$(PROJDIR).oelf --lib=$(TARGET) --paid 0x3800000000000011

$(INTDIR)/%.o: $(PROJDIR)/%.c
	$(CC) $(CFLAGS) -D BUILD=$(BUILD) -D VERSION=$(VERSION) -o $@ $<

$(INTDIR)/%.o: $(PROJDIR)/%.cpp
	$(CCX) $(CXXFLAGS) -D BUILD=$(BUILD) -D VERSION=$(VERSION) -o $@ $<

$(INTDIR)/%.o: $(COMMONDIR)/%.cpp
	$(CCX) $(CXXFLAGS) -D BUILD=$(BUILD) -D VERSION=$(VERSION) -o $@ $<

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

.PHONY: AddToBuildNum clean all target stubs

.DEFAULT_GOAL := all

AddToBuildNum:
	@mkdir -p $(INTDIR)
	@touch $(INTDIR)/.number
	@new_build=$$(($(BUILD) + 1)); \
	echo $$new_build > $(INTDIR)/.number

clean:
	rm -f $(INTDIR)/$(PROJDIR).oelf $(OBJS) \
    $(TARGET) $(TARGETSTUB) $(INTDIR)/$(PROJDIR).elf

target: $(TARGET)

stubs: $(TARGETSTUB)

all: AddToBuildNum clean target stubs
