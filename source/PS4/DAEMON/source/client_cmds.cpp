#include "../headers/includes.hpp"

namespace cmds
{
  namespace client
  {
    namespace connection
    {
      void connect()
      {
        send_response("true");
        connected = true;
      }

      void version()
      {
        std::string message = std::to_string(VERSION);
        send_response(message.c_str());
      }

      void unload()
      {
        if (data.sockets.daemon.server >= 0)
        {
          sceNetSocketClose(data.sockets.daemon.server);
          data.sockets.daemon.server = -1;

          pthread_cancel(data.threads.daemon.server);
        }

        if (data.sockets.daemon.client >= 0)
        {
          sceNetSocketClose(data.sockets.daemon.client);
          data.sockets.daemon.client = -1;

          pthread_cancel(data.threads.daemon.client);
        }

        unloaded = true;

        send_response("done");
      }

      void disconnect()
      {
        send_response("done");
        
        if (data.sockets.daemon.client >= 0)
        {
          sceNetSocketClose(data.sockets.daemon.client);
          data.sockets.daemon.client = -1;
        }
        connected = false;
      }

      void attach()
      {
        attached = false;

        if (is_port_open(RELAYS_PORT))
        {
          char *response = perform_get_request("attach_relay");
          if (response != NULL && strcmp(response, "done") == 0)
            attached = true;
        }

        send_response("done");
      }

    }

    namespace sys_info
    {
      void get_fw()
      {
        send_response(get_fw_version());
      }

      void sys_type()
      {
        send_response(get_console_type());
      }

      void get_temp()
      {
        std::string type_str = extract_param("type", data.buffers.daemon);

        if (type_str.empty())
          return;

        int tempValue = NULL;
        if (strcmp(type_str.c_str(), "cpu") == 0)
          tempValue = get_cpu_temperature();
        else if (strcmp(type_str.c_str(), "soc") == 0)
          tempValue = get_soc_temperature();
        else
          return;

        char message[BUFFER_SIZE];
        snprintf(message, sizeof(message), "%i", tempValue);
        send_response(message);
      }

      void get_user()
      {
        send_response(get_username());
      }

    }

    namespace sys_control
    {
      void temp_limit()
      {
        uint8_t temp = 0;

        std::string limit_str = extract_param("limit", data.buffers.daemon);

        if (!limit_str.empty())
        {
          if (sscanf(limit_str.c_str(), "%hhu", &temp) != 1)
          {
            send_error_response(INVALID_ARGS);

            return;
          }

          // if (set_temperature_limit(temp))
          send_response("done");
          // else
          //  send_error_response(UNKNOWN_ERROR);
        }
        else
          send_error_response(INVALID_ARGS);
      }

      void set_power_state()
      {
        int state = 0;

        std::string state_str = extract_param("state", data.buffers.daemon);

        if (!state_str.empty())
        {
          if (sscanf(state_str.c_str(), "%i", &state) != 1)
          {
            send_error_response(INVALID_ARGS);

            return;
          }

          set_power_state((power_state)state);

          send_response("done");
        }
        else
          send_error_response(INVALID_ARGS);
      };

      void ring_buzzer()
      {
        int type = 0;

        std::string type_str = extract_param("type", data.buffers.daemon);

        if (type_str.empty())
          return;

        type = std::stoi(type_str.c_str());

        switch (type)
        {
        case -1: // continuous
          ::ring_buzzer(6);
          break;
        case 0: // stop
          ::ring_buzzer(0);
          break;
        case 1: // single
          ::ring_buzzer(1);
          break;
        case 2: // double
          ::ring_buzzer(1);
          sceKernelUsleep(125000);
          ::ring_buzzer(1);
          break;
        case 3: // triple
          ::ring_buzzer(1);
          sceKernelUsleep(125000);
          ::ring_buzzer(1);
          sceKernelUsleep(125000);
          ::ring_buzzer(1);
          break;
        }

        send_response("done");
      }

      void notify()
      {
        std::string type_str = extract_param("type", data.buffers.daemon);
        std::string message = extract_param("msg", data.buffers.daemon);

        int type = 0;

        type = std::stoi(type_str.c_str());
       
        if (!type_str.empty())
        text_notify(type, message.c_str());
        send_response("done");
      }

    }

    namespace process
    {
      void get_proc_list()
      {
        uint64_t num = 0;
        struct proc_list_entry *procs = nullptr;

        nlohmann::json response = nlohmann::json::object();

        if (::get_proc_list(nullptr, &num) != 0 || num == 0)
          return;

        procs =
            (struct proc_list_entry *)malloc(sizeof(struct proc_list_entry) * num);
        if (!procs)
          return;

        if (::get_proc_list(procs, &num) != 0)
        {
          free(procs);
          return;
        }

        std::vector<std::pair<int, std::string>> sorted_procs;
        for (uint64_t i = 0; i < num; i++)
        {
          if (procs[i].p_comm[sizeof(procs[i].p_comm) - 1] != '\0')
            procs[i].p_comm[sizeof(procs[i].p_comm) - 1] = '\0';

          sorted_procs.push_back({procs[i].pid,
                                  procs[i].p_comm});
        }

        std::sort(sorted_procs.begin(), sorted_procs.end());

        for (const auto &proc : sorted_procs)
          response[std::to_string(proc.first)] = proc.second;

        send_response(generate_json(response).c_str());

        free(procs);
      }

      void get_proc_info()
      {
        std::unordered_map<std::string, std::string> info_map = {
            {"pid", get_app_info("pid")},
            {"region", get_app_info("region")},
            {"titleId", get_app_info("titleId")},
            {"type", get_app_info("type")},
            {"name", get_app_info("name")},
            {"exec", get_app_info("exec")},
            {"version", get_app_info("version")},
            {"minFW", get_app_info("minFW")},
            {"image", get_app_info("image")}};

        std::string return_str = extract_param("return", data.buffers.daemon);
        if (return_str.empty())
        {
          send_error_response(INVALID_ARGS);

          return;
        }

        if (return_str == "all")
        {
          std::string all_info;
          for (const auto &pair : info_map)
          {
            if (!pair.second.empty())
              all_info += pair.first + ": " + pair.second + "\n";
          }

          send_response(all_info.c_str());

          return;
        }

        auto it = info_map.find(return_str);
        if (it != info_map.end() && !it->second.empty())
        {
          if (return_str == "image")
          {
            std::string saveAs = get_app_info("titleId") + "_icon0.png";
            send_file_response(it->second.c_str(), saveAs.c_str());
          }
          else
            send_response(it->second.c_str());
        }
        else
          send_error_response(INVALID_ARGS);
      }

      void find_pid_by_name()
      {
        std::string name = extract_param("name", data.buffers.daemon);

        if (name.empty())
          return;

        nlohmann::json response =
            {{"name", find_pid_by_procName(name.c_str())}};

        send_response(generate_json(response).c_str());
      }

      void find_name_of_pid()
      {
        std::string pid_str = extract_param("pid", data.buffers.daemon);

        if (pid_str.empty())
          return;

        int pid = atoi(pid_str.c_str()); // Convert to integer

        if (pid == 0)
        {
          send_response(_DEBUGGING);

          return;
        }

        nlohmann::json response = {{"name", find_procName_of_pid(pid)}};
        send_response(generate_json(response).c_str());
      }

      void read_proc_mem()
      {
        std::string address = extract_param("address", data.buffers.daemon);
        std::string size = extract_param("size", data.buffers.daemon);

        if (address.empty())
          return;

        if (size.empty())
          return;

        char request[BUFFER_SIZE];
        snprintf(request, sizeof(request), "read_memory?address=%s&size=%s",
                 address.c_str(), size.c_str());

        send_response(perform_get_request(request));
      }

      void write_proc_mem()
      {
        std::string address = extract_param("address", data.buffers.daemon);
        std::string data = extract_param("data", ::data.buffers.daemon);

        if (address.empty())
          return;

        if (data.empty())
          return;

        char request[BUFFER_SIZE];
        snprintf(request, sizeof(request), "write_memory?address=%s&data=%s",
                 address.c_str(), data.c_str());

        send_response(perform_get_request(request));
      }

      void alloc_proc_mem()
      {
        std::string pid = get_app_info("pid");
        std::string length = extract_param("length", data.buffers.daemon);

        if (length.empty())
          return;

        char request[BUFFER_SIZE];
        snprintf(request, sizeof(request), "alloc_memory?pid=%s&length=%s",
                 pid.c_str(), length.c_str());

        send_response(perform_get_request(request));
      }

      void free_proc_mem()
      {
        std::string pid = get_app_info("pid");
        std::string address = extract_param("address", data.buffers.daemon);
        std::string length = extract_param("length", data.buffers.daemon);

        if (address.empty())
          return;
        if (length.empty())
          return;

        char request[BUFFER_SIZE];
        snprintf(request, sizeof(request), "free_memory?pid=%s&address=%s&length=%s",
                 pid.c_str(), address.c_str(), length.c_str());

        send_response(perform_get_request(request));
      }

      void stop_plugin() {}

      void start_plugin() {}

      void unload_module() {}

      void load_module()
      {
        char request[BUFFER_SIZE];

        std::string prx_path = extract_param("path", data.buffers.daemon);
        std::string exec_path = extract_param("exec", data.buffers.daemon);

        auto load_prx = [](const std::string &exec_path,
                           const std::string &prx_path) -> bool
        {
          int prx_handle =
              sys_sdk_proc_prx_load(const_cast<char *>(exec_path.c_str()),
                                    const_cast<char *>(prx_path.c_str()));
          if (prx_handle >= 0)
          {
            char notify_msg[BUFFER_SIZE];
            snprintf(notify_msg, sizeof(notify_msg), "[OCAPI] PRX Loaded: %s",
                     prx_path.c_str());
            text_notify(222, notify_msg);

            nlohmann::json response = {
                {prx_path,
                 prx_handle}};
            send_response(generate_json(response).c_str());

            return true;
          }
          else
          {
            send_error_response(UNKNOWN_ERROR);
            return false;
          }
        };

        if (is_port_open(RELAYS_PORT))
        {
          if (!exec_path.empty() || !prx_path.empty())
          {
            if (!exec_path.empty() && !prx_path.empty())
            {
              if (exec_path.empty() || prx_path.empty())
                send_error_response(INVALID_ARGS);
              else
                load_prx(exec_path, prx_path);

              return;
            }
            else if (!prx_path.empty() && exec_path.empty())
              snprintf(request, sizeof(request), "load_module?path=%s",
                       prx_path.c_str());

            send_response(perform_get_request(request));
          }
          else
            send_error_response(INVALID_ARGS);
        }
        else
        {
          if (exec_path.empty() && !prx_path.empty())
          {
            if (!attached)
              send_error_response(NOT_ATTACHED);
            else
            {
              char request[BUFFER_SIZE];
              snprintf(request, sizeof(request), "load_module?path=%s",
                       prx_path.c_str());
              send_response(perform_get_request(request));
            }
          }
          else if (!exec_path.empty() && !prx_path.empty())
            load_prx(exec_path, prx_path);
          else
            send_error_response(INVALID_ARGS);
        }
      }

    }

  }
}