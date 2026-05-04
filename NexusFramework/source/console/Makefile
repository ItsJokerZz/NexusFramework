
.PHONY: all

VERSION      := 0.91
BUILD_DIR    := build
BUILD_FILE   := $(BUILD_DIR)/.number
VERSION_FILE := $(BUILD_DIR)/.version
BUILD        := $(shell expr $$(cat $(BUILD_FILE) 2>/dev/null || echo 0) + 1)


export VERSION BUILD BUILD_DIR

define call_make
	@$(MAKE) -f $(1).mk $(2) VERSION=$(VERSION) BUILD=$(BUILD)
endef

all: incrementBuild ps4 ps5

incrementBuild:
	@mkdir -p $(BUILD_DIR)
	@touch $(VERSION_FILE) $(BUILD_FILE)
	@prev_ver=$$(cat $(VERSION_FILE) 2>/dev/null || echo $(VERSION)); \
	prev_build=$$(cat $(BUILD_FILE) 2>/dev/null || echo 0); \
	if [ "$(VERSION)" != "$$prev_ver" ]; then \
		echo "$(VERSION)" > $(VERSION_FILE); \
		echo 1 > $(BUILD_FILE); \
	else \
		echo $$((prev_build+1)) > $(BUILD_FILE); \
	fi
	@rm -f $(BUILD_DIR)/system_utils.o $(BUILD_DIR)/api_commands.o

ps4:
	$(call call_make,ps4)

ps5:
	$(call call_make,ps5)

test-ps4:
	$(call call_make,ps4,test)
test-ps5:
	$(call call_make,ps5,test)

test: test-ps4 test-ps5

clean-ps4:
	$(call call_make,ps4,clean)
clean-ps5:
	$(call call_make,ps5,clean)

clean: clean-ps4 clean-ps5

master-ps4:
	$(call call_make,ps4,master)
master-ps5:
	$(call call_make,ps5,master)

master: clean incrementBuild all
