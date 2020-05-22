// Copyright 2020 Wason Technology, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.


using RobotRaconteurWeb;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RobotRaconteurLocalTapRecorder
{
    public class LocalTapRecorder : IDisposable
    {

        string tap_path;
        string save_path;
        bool log_record_only;
        string tap_name;

        FileSystemWatcher watcher;
        public LocalTapRecorder(string log_save_path, bool log_record_only, string tap_name = null)
        {
            this.save_path = log_save_path;
            this.log_record_only = log_record_only;
            this.tap_name = tap_name;

            string all_tap_path;
            string log_tap_path;
            GetTapPaths(out all_tap_path, out log_tap_path);
            tap_path = log_record_only ? log_tap_path : all_tap_path;
        }

        public void Start()
        {

            foreach(var f in Directory.EnumerateFiles(tap_path,"*.sock"))
            {
                RunTapConnection(f);
            }

            InitWatcher();
        }
        public void InitWatcher()
        {

            watcher = new FileSystemWatcher();
            
            watcher.NotifyFilter = NotifyFilters.CreationTime
            | NotifyFilters.LastWrite
            | NotifyFilters.FileName;
            watcher.Path = tap_path;

            watcher.Filter = "*.sock";

            watcher.Changed += OnChanged;
            watcher.Created += OnChanged;

            watcher.EnableRaisingEvents = true;
            
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            RunTapConnection(e.FullPath);   
        }

        private static void GetTapPaths(out string all_tap, out string log_tap)
        {
            string run_path = GetUserRunPath();
            all_tap = Path.Join(run_path, "tap", "all");
            log_tap = Path.Join(run_path, "tap", "log");

            if (!Directory.Exists(all_tap))
            {
                Directory.CreateDirectory(all_tap);
            }

            if (!Directory.Exists(log_tap))
            {
                Directory.CreateDirectory(log_tap);
            }
        }

        private static int check_mkdir_res(int res)
        {
            if (Mono.Unix.Native.Syscall.GetLastError() == Mono.Unix.Native.Errno.EEXIST)
            {
                return 0;
            }
            return res;
        }
        private static string GetUserRunPath()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var p = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

                    var p1 = Path.Combine(p, "RobotRaconteur", "run");
                    if (!Directory.Exists(p1))
                    {
                        Directory.CreateDirectory(p1);
                    }
                    return p1;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    uint u = Mono.Unix.Native.Syscall.getuid();

                    string path;
                    if (u == 0)
                    {
                        path = "/var/run/robotraconteur/root/";
                        if (check_mkdir_res(Mono.Unix.Native.Syscall.mkdir(path, Mono.Unix.Native.FilePermissions.S_IRUSR
                            | Mono.Unix.Native.FilePermissions.S_IWUSR | Mono.Unix.Native.FilePermissions.S_IXUSR)) < 0)
                        {
                            throw new SystemResourceException("Could not create root run directory");
                        }
                    }
                    else
                    {
                        string path1 = Environment.GetEnvironmentVariable("TMPDIR");
                        if (path1 == null)
                        {
                            throw new SystemResourceException("Could not determine TMPDIR");
                        }

                        path = Path.GetDirectoryName(path1.TrimEnd(Path.DirectorySeparatorChar));
                        path = Path.Combine(path, "C");
                        if (!Directory.Exists(path))
                        {
                            throw new SystemResourceException("Could not determine user cache dir");
                        }

                        path = Path.Combine(path, "robotraconteur");
                        if (check_mkdir_res(Mono.Unix.Native.Syscall.mkdir(path, Mono.Unix.Native.FilePermissions.S_IRUSR
                            | Mono.Unix.Native.FilePermissions.S_IWUSR | Mono.Unix.Native.FilePermissions.S_IXUSR)) < 0)
                        {
                            throw new SystemResourceException("Could not create user run directory");
                        }
                    }
                    return path;
                }
                else
                {
                    uint u = Mono.Unix.Native.Syscall.getuid();

                    string path;
                    if (u == 0)
                    {
                        path = "/var/run/robotraconteur/root/";
                        if (check_mkdir_res(Mono.Unix.Native.Syscall.mkdir(path, Mono.Unix.Native.FilePermissions.S_IRUSR
                            | Mono.Unix.Native.FilePermissions.S_IWUSR | Mono.Unix.Native.FilePermissions.S_IXUSR)) < 0)
                        {
                            throw new SystemResourceException("Could not create root run directory");
                        }
                    }
                    else
                    {
                        path = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");

                        if (path == null)
                        {
                            path = String.Format("/var/run/user/{0}/", u);
                        }

                        path = Path.Combine(path, "robotraconteur");
                        if (check_mkdir_res(Mono.Unix.Native.Syscall.mkdir(path, Mono.Unix.Native.FilePermissions.S_IRUSR
                            | Mono.Unix.Native.FilePermissions.S_IWUSR | Mono.Unix.Native.FilePermissions.S_IXUSR)) < 0)
                        {
                            throw new SystemResourceException("Could not create user run directory");
                        }
                    }
                    return path;
                }
            }
            catch (Exception ee)
            {
                throw new SystemResourceException("Could not activate system for local transport: " + ee.Message);
            }
        }

        public void Dispose()
        {
            watcher?.Dispose();
        }

        List<string> running_taps = new List<string>();

        public void RunTapConnection(string socket_fname)
        {
            string name = Path.GetFileNameWithoutExtension(socket_fname);
            if (tap_name != null)
            {
                if (name != tap_name)
                {
                    return;
                }
            }

            lock (this)
            {               

                if (running_taps.Contains(name)) 
                    return;
                Console.WriteLine($"Starting tap: {name}");
                string log_fname = Path.GetFileNameWithoutExtension(socket_fname) + "-" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss") + ".robtap";

                var t = new LocalTapRecorderConnection();
                try
                {
                    running_taps.Add(name);
                    Task ta = t.Run(socket_fname, log_fname).ContinueWith(
                        delegate(Task ta2)
                        {
                            Console.WriteLine($"Stopping tap: {name}");
                            t.Dispose();
                            if (ta2.IsFaulted)
                            {
                                var e = ta2.Exception;
                            }
                            lock(this)
                            {
                                running_taps.Remove(name);
                            }
                        }
                        );
                }
                catch (Exception)
                {
                    running_taps.Remove(name);
                    throw;
                }   

            }
            
        }
    }

    class LocalTapRecorderConnection : IDisposable
    {
        Stream sock;
        Stream file;
        public LocalTapRecorderConnection()
        {
            
        }

        public void Dispose()
        {
            sock?.Dispose();
            file?.Dispose();
        }

        public async Task Run(string socket_fname, string log_fname)
        {
            var sock1 = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await sock1.ConnectAsync(new UnixDomainSocketEndPoint(socket_fname));
            using (sock = new NetworkStream(sock1, true))
            using (file = new FileStream(log_fname, FileMode.CreateNew))
            {
                var buffer = new byte[1024 * 1024];
                while (true)
                {
                    int bytes_read = await sock.ReadAsync(buffer, 0, buffer.Length);
                    if (bytes_read == 0)
                        return;
                    await file.WriteAsync(buffer, 0, bytes_read);
                }
            }           
        }
    }
}
