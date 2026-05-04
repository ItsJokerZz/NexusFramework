.PHONY: all

HOST        ?= ps4
PORT        ?= 9021

BUILD_DIR   := build/ps4
OUTPUT_DIR  := output

ifdef PS4_PAYLOAD_SDK
	include $(PS4_PAYLOAD_SDK)/toolchain/orbis.mk
else
	$(error PS4_PAYLOAD_SDK is undefined)
endif

SOURCE      := $(shell find source -type f -name '*.cpp' ! -name '*_ps5.cpp')
HEADERS     := $(shell find headers -type f -name '*.hpp' ! -name '*_ps5.hpp')
OBJECTS     := $(patsubst source/%.cpp,$(BUILD_DIR)/%.o,$(SOURCE))
INCLUDES    := $(addprefix -I, $(shell find headers -type d ! -name '*_ps5*'))

STUBS       := -lSceLibcInternal -lkernel_sys -lSceSystemService \
			   -lSceUserService -lSceSysCore -lSceNetCtl

CFLAGS      := -Wall -Werror -g -std=c++2b -Wno-unused-variable -Wno-misleading-indentation -O2
			   
CXXFLAGS    := $(CFLAGS) $(INCLUDES) -D VERSION=\"$(VERSION)\" -D BUILD=$(BUILD)

ELF         := $(OUTPUT_DIR)/nexus-ps4.elf

$(ELF): $(OBJECTS)
	@mkdir -p $(dir $@)
	$(CXX) -o $@ $^ $(STUBS) -Wl,--wrap=ptrace
	$(STRIP) --strip-unneeded --strip-debug $@

$(BUILD_DIR)/%.o: source/%.cpp $(HEADERS)
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -c $< -o $@

test: $(ELF)
	$(PS4_DEPLOY) -h $(HOST) -p $(PORT) $^

clean:
	rm -rf $(BUILD_DIR) $(ELF)

all: $(ELF)

master: clean all
