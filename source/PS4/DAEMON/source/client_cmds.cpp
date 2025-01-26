#include "../headers/includes.hpp"

namespace cmds
{
  namespace client
  {
    namespace connection
    {
      void version()
      {
        std::string message = std::to_string(VERSION);
        send_response(message.c_str());
      }

      void connect()
      {
        send_response("true");
        connected = true;
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
          char *response = perform_get_request("attach");
          if (response != NULL && strcmp(response, "done") == 0)
            attached = true;
        }

        send_response("done");
      }

    }

    namespace sys_info
    {
      void sys_type()
      {
        send_response(get_console_type());
      }

      void get_fw()
      {
        send_response(get_fw_version());
      }

      void get_temp()
      {
        char temp[BUFFER_SIZE] = {
            0};
        const char *start = strstr(data.buffers.daemon.data(), "type=");
        if (start)
        {
          start += 5;
          char *end = strchr(start, '&');
          if (end)
            *end = '\0';
          sscanf(start, "%s", temp);
          start = end ? strchr(end + 1, '=') : NULL;
          if (start)
            start += 1;
        }

        int tempValue = NULL;
        if (strcmp(temp, "cpu") == 0)
        {
          tempValue = get_cpu_temperature();
        }
        else if (strcmp(temp, "soc") == 0)
        {
          tempValue = get_soc_temperature();
        }
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
      void notify()
      {
        std::string msg;
        int type = 0;

        const char *type_start = strstr(data.buffers.daemon.data(), "type=");
        if (type_start)
        {
          type_start += 5;
          const char *type_end = strchr(type_start, '&');
          if (type_end)
          {
            std::string type_str(type_start, type_end);
            type = std::stoi(type_str);
            type_start = strchr(type_end + 1, '=');
            if (type_start)
              type_start += 1;
          }
        }

        if (type_start)
        {
          const char *msg_end = strchr(type_start, ' ');
          if (msg_end)
          {
            std::string encoded_msg(type_start, msg_end);
            char *decoded = decode_url(encoded_msg.c_str());
            if (decoded)
            {
              msg = std::string(decoded);
              free(
                  decoded); // Free the decoded string if it was dynamically allocated
            }
          }
        }

        text_notify(type, msg.empty() ? nullptr : msg.c_str());
        send_response("done");
      }

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
        const char *start = strstr(data.buffers.daemon.data(), "type=");
        if (start)
        {
          start += 5;
          const char *end = strchr(start, '&');
          if (end)
          {
            std::string type_str(start, end);
            type = std::stoi(type_str);
          }
        }

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

    }

    namespace process
    {
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
              snprintf(request, sizeof(request), "exec_prx?path=%s",
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
              snprintf(request, sizeof(request), "exec_prx?path=%s",
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

      void find_pid_by_name()
      {
        const char *name = strstr(data.buffers.daemon.data(), "name=");
        if (!name)
          return;

        name += 5;
        const char *end = strchr(name, ' ');
        if (!end)
          end = name + strlen(name);

        std::string proc_name(name, end);

        nlohmann::json response =
            {{"pid", find_pid_by_procName(proc_name.c_str())}};

        send_response(generate_json(response).c_str());
      }

      void find_name_of_pid()
      {
        int pid = 0;

        // Extract the pid from the input buffer
        const char *pid_str = strstr(data.buffers.daemon.data(), "pid=");
        if (!pid_str)
          return;

        pid_str += 4;        // Skip the "pid=" part
        pid = atoi(pid_str); // Convert to integer

        if (pid == 0)
        {
          nlohmann::json error_response = {{"error", "Invalid pid."}};
          send_response(generate_json(error_response).c_str());

          return;
        }

        nlohmann::json response = {{"name", find_procName_of_pid(pid)}};
        send_response(generate_json(response).c_str());
      }

      void load_plugin()
      {
        if (attached)
        {
          send_error_response(NOT_ATTACHED);
          return;
        }

        if (perform_get_request("load_plugin") != NULL)
          send_response("done");
      }

      void rw_proc_mem()
      {
        send_response("done");
      }

      void get_proc_info()
      {
        sceKernelIccIndicatorBootDone();

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

    }

  }
}