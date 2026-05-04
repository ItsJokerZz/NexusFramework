# =============================================================================
# NexusCheatFramework — PS5 Native Payload Makefile
#
# EXPERIMENTAL — Requires PS5SDK (https://github.com/PS5Dev/PS5SDK)
#
# Build:
#   export PS5_PAYLOAD_SDK=/path/to/PS5SDK
#   make
#
# Output: output/nexus-cheat-ps5.elf
# =============================================================================

HOST        ?= ps5
PORT        ?= 9021

BUILD_DIR   := build/ps5
OUTPUT_DIR  := output

ifdef PS5_PAYLOAD_SDK
    include $(PS5_PAYLOAD_SDK)/toolchain/prospero.mk
else
    $(error PS5_PAYLOAD_SDK is undefined. Set it to your PS5SDK installation path.)
endif

# Source files
SRC_DIR     := src
INC_DIR     := include

SOURCES     := $(wildcard $(SRC_DIR)/*.c)
OBJECTS     := $(patsubst $(SRC_DIR)/%.c, $(BUILD_DIR)/%.o, $(SOURCES))

TARGET      := $(OUTPUT_DIR)/nexus-cheat-ps5.elf

# Compiler flags
CFLAGS      += -I$(INC_DIR) -Wall -Wextra -Werror -O2
LDFLAGS     += -lc -lkernel

# =============================================================================
# Targets
# =============================================================================

.PHONY: all clean

all: $(BUILD_DIR) $(OUTPUT_DIR) $(TARGET)

$(BUILD_DIR):
	mkdir -p $(BUILD_DIR)

$(OUTPUT_DIR):
	mkdir -p $(OUTPUT_DIR)

$(BUILD_DIR)/%.o: $(SRC_DIR)/%.c
	$(CC) $(CFLAGS) -c $< -o $@

$(TARGET): $(OBJECTS)
	$(CC) $(LDFLAGS) -o $@ $^
	@echo "Built: $(TARGET)"

clean:
	rm -rf $(BUILD_DIR) $(OUTPUT_DIR)
	@echo "Cleaned."
