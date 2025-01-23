LIBB64_DIR := /mnt/c/Users/Dakota/Downloads/libb64-1.2.1
LIBB64_INCLUDE_DIR := $(LIBB64_DIR)/include
LIBB64_SRC_DIR := $(LIBB64_DIR)/src

TOOLCHAIN     := $(OO_PS4_TOOLCHAIN)
PROJDIR       := source
INTDIR        := build

VERSION       := 0.54
TARGET        := OrbisControl.prx

VERSION_FILE  := $(INTDIR)/.version
BUILD_FILE    := $(INTDIR)/.number

LIBS          := -lc -lc++ -lkernel -lGoldHEN_Hook    \
                 -lSceSystemService -lSceSysmodule    \
                 -lSceUserService -lSceNet -lSceHttp  \
                 -lSceSysUtil -lSceSysmodule -lScePad \

CPPFILES      := $(wildcard $(PROJDIR)/*.cpp)
OBJS          := $(patsubst $(PROJDIR)/%.cpp, $(INTDIR)/%.o, $(CPPFILES))

# Add libb64 .c files to the OBJS list
LIBB64_C_FILES := $(wildcard $(LIBB64_SRC_DIR)/*.c)
LIBB64_OBJS     := $(patsubst $(LIBB64_SRC_DIR)/%.c, $(INTDIR)/%.o, $(LIBB64_C_FILES))

OBJS += $(LIBB64_OBJS)

CFLAGS        := --target=x86_64-pc-freebsd12-elf -fPIC -funwind-tables -c \
                 $(EXTRAFLAGS) -isysroot $(TOOLCHAIN) -isystem $(TOOLCHAIN)/include \
                 -Iinclude -I$(LIBB64_INCLUDE_DIR) -std=c11 -D_DEFAULT_SOURCE -DFMT_HEADER_ONLY

CXXFLAGS      := $(CFLAGS) -isystem $(TOOLCHAIN)/include/c++/v1 \
                 -std=c++11 -Iinclude -I$(LIBB64_INCLUDE_DIR) -DFMT_HEADER_ONLY

LDFLAGS       := -m elf_x86_64 -pie --script $(TOOLCHAIN)/link.x --eh-frame-hdr \
                 -L$(TOOLCHAIN)/lib $(LIBS) $(TOOLCHAIN)/lib/crtlib.o --wrap=_init \

$(TARGET): $(INTDIR) $(OBJS) incrementBuild
	$(LD) $(OBJS) -o $(INTDIR)/$(PROJDIR).elf $(LDFLAGS) -e _init
	$(TOOLCHAIN)/bin/$(CDIR)/create-fself -in=$(INTDIR)/$(PROJDIR).elf \
	-out=$(INTDIR)/$(PROJDIR).oelf --lib=$(TARGET) --paid 0x3800000000000011

$(INTDIR)/%.o: $(PROJDIR)/%.c
	$(CC) $(CFLAGS) -D BUILD=$(BUILD)+1 -D VERSION=$(VERSION) -o $@ $<

$(INTDIR)/%.o: $(PROJDIR)/%.cpp
	$(CCX) $(CXXFLAGS) -D BUILD=$(BUILD)+1 -D VERSION=$(VERSION) -o $@ $<

# Adding libb64 source file compilation rule
$(INTDIR)/%.o: $(LIBB64_SRC_DIR)/%.c
	$(CC) $(CFLAGS) -I$(LIBB64_INCLUDE_DIR) -D BUILD=$(BUILD)+1 -D VERSION=$(VERSION) -o $@ $<

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

.PHONY: incrementBuild clean all target stubs

.DEFAULT_GOAL := all

BUILD := $(shell cat $(BUILD_FILE))

incrementBuild:
	@mkdir -p $(INTDIR)
	@touch $(BUILD_FILE)
	@if [ -f $(VERSION_FILE) ]; then \
		last_version=$$(cat $(VERSION_FILE)); \
		if [ "$(VERSION)" != "$$last_version" ]; then \
			echo "$(VERSION)" > $(VERSION_FILE); \
			echo "1" > $(BUILD_FILE); \
		else \
			build=$$(cat $(BUILD_FILE)); \
			new_build=$$(($$build + 1)); \
			echo $$new_build > $(BUILD_FILE); \
		fi \
	else \
		echo "$(VERSION)" > $(VERSION_FILE); \
		echo "1" > $(BUILD_FILE); \
	fi


clean:
	rm -f $(INTDIR)/$(PROJDIR).oelf $(OBJS) \
	$(TARGET) $(INTDIR)/$(PROJDIR).elf

target: $(TARGET)

all: incrementBuild target

master: clean all
