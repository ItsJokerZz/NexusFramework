# Library metadata.
TARGET      := OrbisControl.prx

# Libraries linked into the ELF.
LIBS := -lc -lc++ -lkernel -lGoldHEN_Hook -lSceSystemService \
		-lSceSysmodule -lSceNet -lSceHttp -lSceSysUtil

# Additional compile flags.
EXTRAFLAGS   := -Wall -Wno-int-to-pointer-cast -Werror -Wno-for-loop-analysis \
               -fcolor-diagnostics -Iinclude -Wall -D__ORBIS__ -D__PS4__ \
               -Wno-unused-function -Wno-misleading-indentation -Wno-invalid-noreturn

# Root vars.
TOOLCHAIN    := $(OO_PS4_TOOLCHAIN)
PROJDIR      := source
INTDIR       := build

# Define objects to build.
CFILES       := $(wildcard $(PROJDIR)/*.c)
CPPFILES     := $(wildcard $(PROJDIR)/*.cpp)
ASMFILES     := $(wildcard $(PROJDIR)/*.s)

OBJS         := \
$(patsubst $(PROJDIR)/%.c, $(INTDIR)/%.o, $(CFILES)) \
$(patsubst $(PROJDIR)/%.cpp, $(INTDIR)/%.o, $(CPPFILES)) \
$(patsubst $(PROJDIR)/%.s, $(INTDIR)/%.o, $(ASMFILES)) \

# Define final C/C++ flags.
CFLAGS       := --target=x86_64-pc-freebsd12-elf -fPIC -funwind-tables -c \
               $(EXTRAFLAGS) -isysroot $(TOOLCHAIN) -isystem $(TOOLCHAIN)/include \
			   -Iinclude -std=c11 -D_DEFAULT_SOURCE -DFMT_HEADER_ONLY 

CXXFLAGS     := $(CFLAGS) -isystem $(TOOLCHAIN)/include/c++/v1 \
			   -std=c++11 -Iinclude -DFMT_HEADER_ONLY

LDFLAGS      := -m elf_x86_64 -pie --script $(TOOLCHAIN)/link.x --eh-frame-hdr \
               -L$(TOOLCHAIN)/lib $(LIBS) $(TOOLCHAIN)/lib/crtlib.o

# Create the intermediate directory incase it doesn't already exist.
_unused      := $(shell mkdir -p $(INTDIR))

# Check for Linux vs macOS and account for clang/ld path.
UNAME_S      := $(shell uname -s)

ifeq ($(UNAME_S),Linux)
	CC       := clang
	CCX      := clang++
	LD       := ld.lld
	CDIR     := linux
	ASMLR    := as
endif

ifeq ($(UNAME_S),Darwin)
	CC       := /usr/local/opt/llvm/bin/clang
	CCX      := /usr/local/opt/llvm/bin/clang++
	LD       := /usr/local/opt/llvm/bin/ld.lld
	CDIR     := macos
	ASMLR    := as
endif

# Define build rules.
$(TARGET): $(INTDIR) $(OBJS)
	$(LD) $(OBJS) -o $(INTDIR)/$(PROJDIR).elf $(LDFLAGS) -e entry
	$(TOOLCHAIN)/bin/$(CDIR)/create-fself -in=$(INTDIR)/$(PROJDIR).elf \
	-out=$(INTDIR)/$(PROJDIR).oelf --lib=$(TARGET) --paid 0x3800000000000011

$(INTDIR)/%.o: $(PROJDIR)/%.s
	$(ASMLR) -o $@ $<

$(INTDIR)/%.o: $(PROJDIR)/%.c
	$(CC) $(CFLAGS) -o $@ $<

$(INTDIR)/%.o: $(PROJDIR)/%.cpp
	$(CCX) $(CXXFLAGS) -o $@ $<

$(INTDIR)/%.o: $(COMMONDIR)/%.cpp
	$(CCX) $(CXXFLAGS) -o $@ $<

# Define phony targets.
.PHONY: clean
.DEFAULT_GOAL := all

all: $(TARGET) $(TARGETSTUB)

clean:
	rm -f $(TARGET) $(TARGETSTUB) $(INTDIR)/$(PROJDIR).elf $(INTDIR)/$(PROJDIR).oelf $(OBJS)
