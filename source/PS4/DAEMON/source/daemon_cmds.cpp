#include "../headers/includes.hpp"

namespace cmds
{
  namespace daemon
  {
    void attach_relay()
    {
      attached = true;

      text_notify(222, "[OCAPI] Attached!");
      send_response("done");
    }

    void unload_module() {}

    void load_module()
    {
      std::string path = extract_param("path", data.buffers.relay);

      if (path.empty())
      {
        log_message("No path provided for SPRX");
        return;
      }

      struct proc_info info;
      sys_sdk_proc_info(&info);
      int pid = info.pid;

      int32_t result = sceKernelLoadStartModule(path.c_str(), 0, 0, 0, NULL, NULL);
      if (result == 0x80020002)
      {
        log_message("SPRX %s not found", path.c_str());
        return;
      }
      else if (result < 0)
      {
        log_message("Error loading SPRX %s! Error code 0x%08x (%i)", path.c_str(), result, result);
        return;
      }

      char response[BUFFER_SIZE];
      snprintf(response, sizeof(response), "%i,%d,%s", pid, result, path.c_str());
      send_response(response);

      int32_t ret;
      int32_t (*module_start_ret)(size_t, const void *);
      int32_t (*module_stop_ret)(size_t, const void *);

      ret = sceKernelDlsym(result, "module_start", (void **)&module_start_ret);
      log_message("module_start Dlsym 0x%08x @ %p", ret, module_start_ret);

      ret = sceKernelDlsym(result, "module_stop", (void **)&module_stop_ret);
      log_message("module_stop Dlsym 0x%08x @ %p", ret, module_stop_ret);

      if (module_start_ret && module_stop_ret)
      {
        log_message("Starting SPRX...");
        int32_t prx_ret = module_start_ret(0, nullptr);
        log_message("module_start returned with 0x%08x", prx_ret);

        if (prx_ret || prx_ret < 0)
        {
          log_message("SPRX returned non-zero, stopping module...");
          prx_ret = module_stop_ret(0, nullptr);
          log_message("module_stop returned with 0x%08x", prx_ret);
        }
        else if (prx_ret == 0)
        {
          log_message("module_start exit successful 0x%08x", prx_ret);
        }
      }
      else
      {
        log_message("Unable to find module_start or module_stop!");
      }

      char notify_msg[BUFFER_SIZE];
      snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] SPRX Loaded:\n%s", path.c_str());
      text_notify(222, notify_msg);
    }

    void stop_plugin() {}

    void start_plugin()
    {
      std::string plugin = extract_param("plugin", data.buffers.relay);

      if (plugin.empty())
      {
        log_message("No plugin provided to load");
        return;
      }

      // Construct the full path for the plugin
      std::string path = "/data/GoldHEN/plugins/" + plugin;
      log_message("Attempting to load plugin from path: %s", path.c_str());

      // Get process information
      struct proc_info info;
      sys_sdk_proc_info(&info);
      int pid = info.pid;

      // Attempt to load the plugin
      int32_t result = sceKernelLoadStartModule(path.c_str(), 0, 0, 0, NULL, NULL);
      if (result == 0x80020002)
      {
        log_message("Plugin %s not found at path: %s", plugin.c_str(), path.c_str());
        send_response("Plugin not found");
        return;
      }
      else if (result < 0)
      {
        log_message("Error loading plugin %s! Error code 0x%08x (%d)", plugin.c_str(), result, result);
        send_response("Failed to load plugin");
        return;
      }

      // Log successful load
      log_message("Plugin %s loaded successfully with module ID: %d", plugin.c_str(), result);

      // Prepare response
      char response[BUFFER_SIZE];
      snprintf(response, sizeof(response), "%i,%d,%s", pid, result, plugin.c_str());
      send_response(response);

      // Resolve the plugin_load and plugin_unload symbols
      int32_t ret;
      int32_t (*plugin_load_ret)(void) = nullptr;
      int32_t (*plugin_unload_ret)(void) = nullptr;

      ret = sceKernelDlsym(result, "plugin_load", (void **)&plugin_load_ret);
      if (ret < 0 || !plugin_load_ret)
      {
        log_message("Failed to resolve plugin_load symbol. Error: 0x%08x", ret);
        send_response("Failed to find plugin_load");
        return;
      }

      ret = sceKernelDlsym(result, "plugin_unload", (void **)&plugin_unload_ret);
      if (ret < 0 || !plugin_unload_ret)
      {
        log_message("Failed to resolve plugin_unload symbol. Error: 0x%08x", ret);
        send_response("Failed to find plugin_unload");
        return;
      }

      // Call plugin_load
      log_message("Starting plugin...");
      int32_t prx_ret = plugin_load_ret();
      log_message("plugin_load returned with 0x%08x", prx_ret);

      // Handle errors from plugin_load
      if (prx_ret != 0)
      {
        log_message("Plugin returned non-zero, stopping module...");
        prx_ret = plugin_unload_ret();
        log_message("plugin_unload returned with 0x%08x", prx_ret);
        send_response("Plugin failed to start");
        return;
      }

      // Log success and notify
      log_message("plugin_load exited successfully with 0x%08x", prx_ret);
      char notify_msg[BUFFER_SIZE];
      snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] Plugin Loaded: %s", plugin.c_str());
      text_notify(222, notify_msg);
    }

    void read_memory()
    {
      std::string address = extract_param("address", data.buffers.relay);
      std::string size = extract_param("size", data.buffers.relay);

      if (address.empty())
      {
        log_message("No address provided");
        send_error_response(_DEBUGGING);
        return;
      }

      if (size.empty())
      {
        log_message("No size provided");
        send_error_response(_DEBUGGING);
        return;
      }

      uint64_t addressVal = std::stoull(address, nullptr, 16); // Convert hex string to uint64_t
      size_t byteSize = std::stoul(size);                      // Convert string to size_t

      if (byteSize > 1024 * 1024)
      {
        send_response("error: byteSize too large");
        return;
      }

      char *buffer = new char[byteSize];

      proc_rw args;
      args.address = addressVal;
      args.data = static_cast<void *>(buffer);
      args.length = byteSize;
      args.write_flags = 0;

      if (sys_sdk_proc_rw(&args) == 0)
      {
        // Convert the buffer data to hex string
        std::string hexStr;
        for (size_t i = 0; i < byteSize; ++i)
        {
          char hexByte[3]; // 2 digits + null terminator
          snprintf(hexByte, sizeof(hexByte), "%02x", static_cast<unsigned char>(buffer[i]));
          hexStr += hexByte;
        }

        send_response(hexStr.c_str());
      }
      else
      {
        send_response("error: failed to read memory");
      }

      delete[] buffer;
    }

    void write_memory()
    {
      std::string address = extract_param("address", data.buffers.relay);
      std::string dataToWrite = extract_param("data", data.buffers.relay);

      if (address.empty())
      {
        log_message("No address provided");
        send_error_response(_DEBUGGING);
        return;
      }

      if (dataToWrite.empty())
      {
        log_message("No data to write provided");
        send_error_response(_DEBUGGING);
        return;
      }

      log_message("Address: %s, Data: %s", address.c_str(), dataToWrite.c_str());

      uint64_t addressVal = strtoull(address.c_str(), nullptr, 16);
      size_t byteSize = dataToWrite.length() / 2;
      char *buffer = new char[byteSize + 1];
      buffer[byteSize] = '\0';

      for (size_t i = 0; i < byteSize; i++)
      {
        unsigned int byte;
        sscanf(dataToWrite.c_str() + i * 2, "%2x", &byte);
        buffer[i] = static_cast<char>(byte);
      }

      proc_rw args;
      args.address = addressVal;
      args.data = static_cast<void *>(buffer);
      args.length = byteSize;
      args.write_flags = 1;

      if (sys_sdk_proc_rw(&args) == 0)
        send_response("success: memory written");
      else
        send_response("error: failed to write memory");

      delete[] buffer;
    }

    void alloc_memory()
    {
      std::string pid = extract_param("pid", data.buffers.relay);
      std::string length = extract_param("length", data.buffers.relay);

      if (pid.empty())
      {
        log_message("No pid provided");
        send_error_response(_DEBUGGING);
        return;
      }

      if (length.empty())
      {
        log_message("No length provided");
        send_error_response(_DEBUGGING);
        return;
      }

      uint64_t pid_value = std::stoull(pid);
      uint64_t length_value = std::stoull(length);

      struct free_and_alloc_args args = {0};
      args.length = length_value; // Initialize length

      int result = sys_proc_alloc(pid_value, &args, false);
      if (result != 0)
      {
        log_message("Failed to allocate memory");
        send_error_response(_DEBUGGING);
        return;
      }

      // Respond with the allocated address as a hexadecimal string
      char hex_address[20];
      snprintf(hex_address, sizeof(hex_address), "0x%lx", args.address);
      send_response(hex_address);
    }

    void free_memory()
    {
      std::string pid = extract_param("pid", data.buffers.relay);
      std::string address = extract_param("address", data.buffers.relay);
      std::string length = extract_param("length", data.buffers.relay);

      if (pid.empty())
      {
        log_message("No pid provided");
        send_error_response(_DEBUGGING);
        return;
      }

      if (address.empty())
      {
        log_message("No address provided");
        send_error_response(_DEBUGGING);
        return;
      }

      if (length.empty())
      {
        log_message("No length provided");
        send_error_response(_DEBUGGING);
        return;
      }

      uint64_t pid_value = std::stoull(pid);
      uint64_t address_value = std::stoull(address, nullptr, 16); // Parse as hex
      uint64_t length_value = std::stoull(length);

      struct free_and_alloc_args args = {0};
      args.address = address_value; // Set address
      args.length = length_value;   // Set length

      int result = sys_proc_alloc(pid_value, &args, true);
      if (result != 0)
      {
        log_message("Failed to free memory");
        send_error_response(_DEBUGGING);
        return;
      }

      send_response("Freed memory successfully");
    }

  }
}